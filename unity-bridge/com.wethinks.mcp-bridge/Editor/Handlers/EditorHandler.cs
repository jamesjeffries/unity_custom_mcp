using System.Collections.Generic;
using UnityEditor;

namespace WeThinks.Mcp.Editor
{
    internal static class EditorHandler
    {
        public static void Register()
        {
            CommandRegistry.Register("editor.get_state", GetState);
            CommandRegistry.Register("editor.enter_play", EnterPlay);
            CommandRegistry.Register("editor.exit_play", ExitPlay);
            CommandRegistry.Register("editor.pause", Pause);
            CommandRegistry.Register("editor.step", Step);
        }

        private static object GetState(CommandParams p)
        {
            return new Dictionary<string, object>
            {
                { "unityVersion", UnityEngine.Application.unityVersion },
                { "isPlaying", EditorApplication.isPlaying },
                { "isPaused", EditorApplication.isPaused },
                { "isCompiling", EditorApplication.isCompiling },
                { "isUpdating", EditorApplication.isUpdating },
                { "activeScene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name }
            };
        }

        private static object EnterPlay(CommandParams p)
        {
            EditorApplication.isPlaying = true;
            return new Dictionary<string, object> { { "isPlaying", true } };
        }

        private static object ExitPlay(CommandParams p)
        {
            EditorApplication.isPlaying = false;
            return new Dictionary<string, object> { { "isPlaying", false } };
        }

        private static object Pause(CommandParams p)
        {
            bool paused = p.GetBool("paused", true);
            EditorApplication.isPaused = paused;
            return new Dictionary<string, object> { { "isPaused", paused } };
        }

        private static object Step(CommandParams p)
        {
            EditorApplication.Step();
            return new Dictionary<string, object> { { "stepped", true } };
        }
    }
}
