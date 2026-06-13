using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WeThinks.Mcp.Editor
{
    /// <summary>
    /// Captures Unity log messages into a bounded ring buffer so the MCP server
    /// can read recent console output. Registered once on load.
    /// </summary>
    [InitializeOnLoad]
    internal static class ConsoleHandler
    {
        private struct Entry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
            public string Timestamp;
        }

        private const int Capacity = 500;
        private static readonly LinkedList<Entry> Buffer = new LinkedList<Entry>();
        private static readonly object Gate = new object();

        static ConsoleHandler()
        {
            // Use the threaded callback so logs from any thread are captured.
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
        }

        public static void Register()
        {
            CommandRegistry.Register("console.get_logs", GetLogs);
            CommandRegistry.Register("console.clear", Clear);
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            lock (Gate)
            {
                Buffer.AddLast(new Entry
                {
                    Message = message,
                    StackTrace = stackTrace,
                    Type = type,
                    Timestamp = System.DateTime.UtcNow.ToString("o")
                });

                while (Buffer.Count > Capacity)
                {
                    Buffer.RemoveFirst();
                }
            }
        }

        private static object GetLogs(CommandParams p)
        {
            string level = p.GetString("level", "all").ToLowerInvariant();
            int count = p.GetInt("count", 50);

            var snapshot = new List<Entry>();
            lock (Gate)
            {
                snapshot.AddRange(Buffer);
            }

            var logs = new List<object>();
            // Walk newest-first and keep up to `count` matching entries.
            for (int i = snapshot.Count - 1; i >= 0 && logs.Count < count; i--)
            {
                Entry e = snapshot[i];
                if (!Matches(e.Type, level))
                {
                    continue;
                }

                logs.Add(new Dictionary<string, object>
                {
                    { "message", e.Message },
                    { "level", LevelName(e.Type) },
                    { "timestamp", e.Timestamp },
                    { "stackTrace", e.Type == LogType.Error || e.Type == LogType.Exception ? e.StackTrace : null }
                });
            }

            logs.Reverse(); // Return in chronological order.
            return new Dictionary<string, object>
            {
                { "count", logs.Count },
                { "logs", logs }
            };
        }

        private static bool Matches(LogType type, string level)
        {
            switch (level)
            {
                case "error":
                    return type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
                case "warning":
                    return type == LogType.Warning;
                case "log":
                    return type == LogType.Log;
                default:
                    return true;
            }
        }

        private static string LevelName(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return "error";
                case LogType.Warning:
                    return "warning";
                default:
                    return "log";
            }
        }

        private static object Clear(CommandParams p)
        {
            lock (Gate)
            {
                Buffer.Clear();
            }

            ClearUnityConsole();
            return new Dictionary<string, object> { { "cleared", true } };
        }

        private static void ClearUnityConsole()
        {
            // Clear the Editor console via reflection (no public API exists).
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(SceneView));
            var logEntries = assembly?.GetType("UnityEditor.LogEntries");
            var clearMethod = logEntries?.GetMethod(
                "Clear",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            clearMethod?.Invoke(null, null);
        }
    }
}
