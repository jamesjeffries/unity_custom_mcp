using UnityEditor;
using UnityEngine;

namespace WeThinks.Mcp.Editor
{
    /// <summary>
    /// A small control panel under Window &gt; MCP Bridge to start/stop the
    /// bridge, view its status, and configure the port and auto-start.
    /// </summary>
    public sealed class McpWindow : EditorWindow
    {
        private int _portField;

        [MenuItem("Window/MCP Bridge")]
        public static void Open()
        {
            McpWindow window = GetWindow<McpWindow>("MCP Bridge");
            window.minSize = new Vector2(280, 180);
            window.Show();
        }

        private void OnEnable()
        {
            _portField = McpServer.Port;
            McpServer.StateChanged += Repaint;
        }

        private void OnDisable()
        {
            McpServer.StateChanged -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MCP Bridge", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            bool running = McpServer.IsRunning;
            var statusStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = running ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.85f, 0.4f, 0.4f) }
            };
            EditorGUILayout.LabelField(
                "Status:",
                running ? $"Listening on 127.0.0.1:{McpServer.Port}" : "Stopped",
                statusStyle);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(running))
            {
                _portField = EditorGUILayout.IntField("Port", _portField);
            }

            bool autoStart = EditorGUILayout.Toggle("Auto-start on load", McpServer.AutoStart);
            if (autoStart != McpServer.AutoStart)
            {
                McpServer.AutoStart = autoStart;
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!running)
                {
                    if (GUILayout.Button("Start"))
                    {
                        if (_portField != McpServer.Port)
                        {
                            McpServer.Port = _portField;
                        }

                        McpServer.Start();
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop"))
                    {
                        McpServer.Stop();
                    }

                    if (GUILayout.Button("Restart"))
                    {
                        McpServer.Restart();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Start the Python MCP server (unity-mcp) and point your MCP " +
                "client at it. The server connects to this bridge on the port " +
                "shown above (set UNITY_MCP_PORT to match if you change it).",
                MessageType.Info);
        }
    }
}
