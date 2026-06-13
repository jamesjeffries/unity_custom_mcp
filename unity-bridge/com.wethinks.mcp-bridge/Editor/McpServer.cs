using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace WeThinks.Mcp.Editor
{
    /// <summary>
    /// The MCP bridge TCP server. Listens on a loopback port, reads
    /// length-prefixed JSON command frames on a background thread, dispatches
    /// them onto the Unity main thread, and writes framed JSON responses back.
    ///
    /// Boots automatically on Editor load and survives domain reloads by
    /// stopping cleanly before a reload and restarting after.
    /// </summary>
    [InitializeOnLoad]
    public static class McpServer
    {
        private const int HeaderSize = 4;
        private const int MaxFrameBytes = 16 * 1024 * 1024;
        private const int MainThreadTimeoutMs = 30000;

        private const string AutoStartKey = "WeThinks.Mcp.AutoStart";
        private const string PortKey = "WeThinks.Mcp.Port";
        public const int DefaultPort = 6400;

        private static TcpListener _listener;
        private static Thread _acceptThread;
        private static volatile bool _running;
        private static readonly List<TcpClient> Clients = new List<TcpClient>();
        private static readonly object ClientsGate = new object();

        public static bool IsRunning => _running;
        public static int Port
        {
            get => EditorPrefs.GetInt(PortKey, DefaultPort);
            set => EditorPrefs.SetInt(PortKey, value);
        }

        public static bool AutoStart
        {
            get => EditorPrefs.GetBool(AutoStartKey, true);
            set => EditorPrefs.SetBool(AutoStartKey, value);
        }

        public static event Action StateChanged;

        static McpServer()
        {
            MainThreadDispatcher.EnsureInstalled();

            // Stop cleanly before a domain reload so the port is released.
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;

            if (AutoStart)
            {
                // Defer to the first update so the Editor is fully initialized.
                EditorApplication.delayCall += () =>
                {
                    if (AutoStart && !_running)
                    {
                        Start();
                    }
                };
            }
        }

        public static void Start()
        {
            if (_running)
            {
                return;
            }

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _running = true;

                _acceptThread = new Thread(AcceptLoop)
                {
                    IsBackground = true,
                    Name = "MCP-Bridge-Accept"
                };
                _acceptThread.Start();

                Debug.Log($"[MCP] Bridge listening on 127.0.0.1:{Port}");
            }
            catch (Exception ex)
            {
                _running = false;
                Debug.LogError($"[MCP] Failed to start bridge on port {Port}: {ex.Message}");
            }

            StateChanged?.Invoke();
        }

        public static void Stop()
        {
            if (!_running && _listener == null)
            {
                return;
            }

            _running = false;

            try
            {
                _listener?.Stop();
            }
            catch (Exception)
            {
                // Ignore shutdown races.
            }

            _listener = null;

            lock (ClientsGate)
            {
                foreach (TcpClient client in Clients)
                {
                    try
                    {
                        client.Close();
                    }
                    catch (Exception)
                    {
                        // Ignore.
                    }
                }

                Clients.Clear();
            }

            StateChanged?.Invoke();
        }

        public static void Restart()
        {
            Stop();
            Start();
        }

        private static void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    lock (ClientsGate)
                    {
                        Clients.Add(client);
                    }

                    var thread = new Thread(() => ClientLoop(client))
                    {
                        IsBackground = true,
                        Name = "MCP-Bridge-Client"
                    };
                    thread.Start();
                }
                catch (SocketException)
                {
                    // Listener was stopped; exit the loop.
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_running)
                    {
                        Debug.LogWarning($"[MCP] Accept error: {ex.Message}");
                    }

                    break;
                }
            }
        }

        private static void ClientLoop(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    while (_running && client.Connected)
                    {
                        byte[] header = ReadExactly(stream, HeaderSize);
                        if (header == null)
                        {
                            break;
                        }

                        int length = (header[0] << 24) | (header[1] << 16) |
                                     (header[2] << 8) | header[3];
                        if (length < 0 || length > MaxFrameBytes)
                        {
                            break;
                        }

                        byte[] body = length > 0 ? ReadExactly(stream, length) : new byte[0];
                        if (body == null)
                        {
                            break;
                        }

                        string responseJson = HandleRequest(Encoding.UTF8.GetString(body));
                        WriteFrame(stream, responseJson);
                    }
                }
            }
            catch (Exception)
            {
                // Connection dropped; clean up below.
            }
            finally
            {
                lock (ClientsGate)
                {
                    Clients.Remove(client);
                }

                try
                {
                    client.Close();
                }
                catch (Exception)
                {
                    // Ignore.
                }
            }
        }

        private static string HandleRequest(string json)
        {
            string id = null;
            try
            {
                var payload = MiniJson.Deserialize(json) as Dictionary<string, object>;
                if (payload == null)
                {
                    return MiniJson.Serialize(CommandRegistry.Error(null, "Malformed request frame"));
                }

                id = payload.TryGetValue("id", out var idVal) ? idVal?.ToString() : null;
                string command = payload.TryGetValue("command", out var cmd) ? cmd?.ToString() : null;
                var rawParams = payload.TryGetValue("params", out var pr)
                    ? pr as Dictionary<string, object>
                    : null;

                // Run the handler on the Unity main thread and block until done.
                Dictionary<string, object> response = (Dictionary<string, object>)
                    MainThreadDispatcher.Run(
                        () => CommandRegistry.Dispatch(id, command, rawParams),
                        MainThreadTimeoutMs);

                return MiniJson.Serialize(response);
            }
            catch (Exception ex)
            {
                return MiniJson.Serialize(CommandRegistry.Error(id, ex.Message));
            }
        }

        private static byte[] ReadExactly(Stream stream, int count)
        {
            var buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                {
                    return null;
                }

                offset += read;
            }

            return buffer;
        }

        private static void WriteFrame(Stream stream, string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            var header = new byte[HeaderSize];
            header[0] = (byte)((body.Length >> 24) & 0xFF);
            header[1] = (byte)((body.Length >> 16) & 0xFF);
            header[2] = (byte)((body.Length >> 8) & 0xFF);
            header[3] = (byte)(body.Length & 0xFF);

            stream.Write(header, 0, header.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }
    }
}
