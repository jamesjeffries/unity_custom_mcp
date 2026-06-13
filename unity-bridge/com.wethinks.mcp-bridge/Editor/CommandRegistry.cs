using System;
using System.Collections.Generic;

namespace WeThinks.Mcp.Editor
{
    /// <summary>A command handler: takes typed params, returns a JSON-able result.</summary>
    public delegate object CommandHandler(CommandParams parameters);

    /// <summary>
    /// Maps command names (e.g. "gameobject.create") to their handlers. Handlers
    /// always run on the main thread (the registry itself is only read there).
    /// </summary>
    public static class CommandRegistry
    {
        private static readonly Dictionary<string, CommandHandler> Handlers =
            new Dictionary<string, CommandHandler>(StringComparer.Ordinal);

        private static bool _initialized;

        public static void Register(string command, CommandHandler handler)
        {
            Handlers[command] = handler;
        }

        public static IEnumerable<string> Commands => Handlers.Keys;

        /// <summary>
        /// Registers all built-in handlers exactly once. Called lazily on the
        /// main thread before the first dispatch.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            Register("ping", _ => new Dictionary<string, object>
            {
                { "pong", true },
                { "unityVersion", UnityEngine.Application.unityVersion }
            });

            SceneHandler.Register();
            GameObjectHandler.Register();
            AssetHandler.Register();
            ScriptHandler.Register();
            ConsoleHandler.Register();
            EditorHandler.Register();
            MenuHandler.Register();
        }

        /// <summary>
        /// Executes a command on the main thread. Returns a response dictionary
        /// with success/data/error already shaped for the wire protocol.
        /// </summary>
        public static Dictionary<string, object> Dispatch(string id, string command, Dictionary<string, object> rawParams)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(command) || !Handlers.TryGetValue(command, out var handler))
            {
                return Error(id, $"Unknown command: '{command}'");
            }

            try
            {
                object data = handler(new CommandParams(rawParams));
                return new Dictionary<string, object>
                {
                    { "id", id },
                    { "success", true },
                    { "data", data }
                };
            }
            catch (Exception ex)
            {
                return Error(id, ex.Message);
            }
        }

        public static Dictionary<string, object> Error(string id, string message)
        {
            return new Dictionary<string, object>
            {
                { "id", id },
                { "success", false },
                { "error", message }
            };
        }
    }
}
