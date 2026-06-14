using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace WeThinks.Mcp.Editor
{
    /// <summary>
    /// Unity Package Manager (UPM) operations: list installed packages and
    /// add/remove dependencies.
    ///
    /// Client.Add / Client.Remove are asynchronous and are driven by the
    /// Editor's update loop. Because command handlers run *on* the main thread
    /// (the dispatcher pumps them during an update tick), blocking here to wait
    /// for completion would starve the very loop that progresses the request and
    /// deadlock until the bridge times out. So add/remove are fire-and-forget:
    /// they kick off the request and return immediately, and callers poll
    /// "package.list" (which reads Packages/manifest.json straight off disk) to
    /// confirm the result once Unity has resolved it.
    /// </summary>
    internal static class PackageHandler
    {
        private const string ManifestPath = "Packages/manifest.json";

        private static AddRequest _pendingAdd;
        private static RemoveRequest _pendingRemove;

        public static void Register()
        {
            CommandRegistry.Register("package.list", List);
            CommandRegistry.Register("package.add", Add);
            CommandRegistry.Register("package.remove", Remove);
        }

        private static object List(CommandParams p)
        {
            var packages = new List<object>();
            if (File.Exists(ManifestPath))
            {
                string json = File.ReadAllText(ManifestPath);
                if (MiniJson.Deserialize(json) is Dictionary<string, object> root &&
                    root.TryGetValue("dependencies", out object depsObj) &&
                    depsObj is Dictionary<string, object> deps)
                {
                    foreach (var kv in deps)
                    {
                        packages.Add(new Dictionary<string, object>
                        {
                            { "name", kv.Key },
                            { "version", kv.Value?.ToString() }
                        });
                    }
                }
            }

            return new Dictionary<string, object>
            {
                { "count", packages.Count },
                { "packages", packages },
                { "pendingAdd", DescribePending(_pendingAdd) },
                { "pendingRemove", DescribePending(_pendingRemove) }
            };
        }

        private static object Add(CommandParams p)
        {
            string identifier = p.GetString("identifier");
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException(
                    "'identifier' is required, e.g. 'com.unity.cinemachine', " +
                    "'com.unity.inputsystem@1.7.0', or a git URL.");
            }

            _pendingAdd = Client.Add(identifier);
            return new Dictionary<string, object>
            {
                { "requested", true },
                { "identifier", identifier },
                {
                    "note",
                    "Unity is resolving the package; it may recompile and trigger " +
                    "a domain reload. Call package.list to confirm once complete."
                }
            };
        }

        private static object Remove(CommandParams p)
        {
            string identifier = p.GetString("identifier");
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentException("'identifier' is required, e.g. 'com.unity.cinemachine'.");
            }

            _pendingRemove = Client.Remove(identifier);
            return new Dictionary<string, object>
            {
                { "requested", true },
                { "identifier", identifier },
                { "note", "Unity is removing the package; call package.list to confirm." }
            };
        }

        private static object DescribePending(Request request)
        {
            if (request == null)
            {
                return null;
            }

            var info = new Dictionary<string, object>
            {
                { "status", request.Status.ToString() },
                { "isComplete", request.IsCompleted }
            };

            if (request.Status == StatusCode.Failure && request.Error != null)
            {
                info["error"] = request.Error.message;
            }

            return info;
        }
    }
}
