using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace WeThinks.Mcp.Editor
{
    internal static class ScriptHandler
    {
        public static void Register()
        {
            CommandRegistry.Register("script.list", List);
            CommandRegistry.Register("script.read", Read);
            CommandRegistry.Register("script.create", Create);
            CommandRegistry.Register("script.update", Update);
            CommandRegistry.Register("script.delete", Delete);
        }

        private static object List(CommandParams p)
        {
            string folder = p.GetString("folder", "Assets");
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { folder });
            var scripts = new List<object>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".cs"))
                {
                    scripts.Add(path);
                }
            }

            return new Dictionary<string, object>
            {
                { "count", scripts.Count },
                { "scripts", scripts }
            };
        }

        private static object Read(CommandParams p)
        {
            string path = p.GetString("path");
            AssetPathGuard.RequireUnderAssets(path);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Script not found: '{path}'");
            }

            return new Dictionary<string, object>
            {
                { "path", path },
                { "contents", File.ReadAllText(path) }
            };
        }

        private static object Create(CommandParams p)
        {
            string path = p.GetString("path");
            AssetPathGuard.RequireUnderAssets(path);
            RequireCSharp(path);

            if (File.Exists(path))
            {
                throw new IOException($"Script already exists: '{path}'");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, p.GetString("contents", string.Empty));
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "created", true },
                { "path", path }
            };
        }

        private static object Update(CommandParams p)
        {
            string path = p.GetString("path");
            AssetPathGuard.RequireUnderAssets(path);
            RequireCSharp(path);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Script not found: '{path}'");
            }

            File.WriteAllText(path, p.GetString("contents", string.Empty));
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "updated", true },
                { "path", path }
            };
        }

        private static object Delete(CommandParams p)
        {
            string path = p.GetString("path");
            AssetPathGuard.RequireUnderAssets(path);
            RequireCSharp(path);

            bool deleted = AssetDatabase.DeleteAsset(path);
            return new Dictionary<string, object>
            {
                { "deleted", deleted },
                { "path", path }
            };
        }

        private static void RequireCSharp(string path)
        {
            if (!path.EndsWith(".cs"))
            {
                throw new System.ArgumentException("Script path must end with '.cs'");
            }
        }
    }
}
