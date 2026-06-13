using System.Collections.Generic;
using UnityEditor;

namespace WeThinks.Mcp.Editor
{
    internal static class MenuHandler
    {
        // Allowlist of safe, non-destructive menu items the bridge will execute.
        private static readonly HashSet<string> Allowed = new HashSet<string>
        {
            "Assets/Refresh",
            "Assets/Reimport All",
            "Assets/Create/Folder",
            "Edit/Undo",
            "Edit/Redo",
            "Edit/Play",
            "Edit/Pause",
            "Edit/Frame Selected",
            "GameObject/Create Empty",
            "GameObject/Align With View",
            "Window/General/Console",
            "Window/General/Hierarchy",
            "Window/General/Inspector",
            "File/Save Project"
        };

        public static void Register()
        {
            CommandRegistry.Register("menu.execute", Execute);
            CommandRegistry.Register("menu.list_allowed", ListAllowed);
        }

        private static object Execute(CommandParams p)
        {
            string menuPath = p.GetString("menu_path");
            if (string.IsNullOrEmpty(menuPath))
            {
                throw new System.ArgumentException("menu_path is required");
            }

            if (!Allowed.Contains(menuPath))
            {
                throw new System.UnauthorizedAccessException(
                    $"Menu item '{menuPath}' is not in the allowlist. " +
                    "Call menu.list_allowed to see permitted items.");
            }

            bool executed = EditorApplication.ExecuteMenuItem(menuPath);
            return new Dictionary<string, object>
            {
                { "executed", executed },
                { "menuPath", menuPath }
            };
        }

        private static object ListAllowed(CommandParams p)
        {
            return new Dictionary<string, object>
            {
                { "allowed", new List<object>(Allowed) }
            };
        }
    }
}
