using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WeThinks.Mcp.Editor
{
    internal static class AssetHandler
    {
        public static void Register()
        {
            CommandRegistry.Register("asset.find", Find);
            CommandRegistry.Register("asset.get_info", GetInfo);
            CommandRegistry.Register("asset.create_folder", CreateFolder);
            CommandRegistry.Register("asset.delete", Delete);
            CommandRegistry.Register("prefab.instantiate", PrefabInstantiate);
            CommandRegistry.Register("prefab.create_from_gameobject", PrefabCreate);
            CommandRegistry.Register("prefab.apply", PrefabApply);
        }

        private static object Find(CommandParams p)
        {
            string filter = p.GetString("filter", string.Empty);
            string folder = p.GetString("folder", "Assets");

            string[] guids = AssetDatabase.FindAssets(filter, new[] { folder });
            var results = new List<object>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                results.Add(new Dictionary<string, object>
                {
                    { "path", path },
                    { "guid", guid },
                    { "type", AssetDatabase.GetMainAssetTypeAtPath(path)?.Name }
                });
            }

            return new Dictionary<string, object>
            {
                { "count", results.Count },
                { "assets", results }
            };
        }

        private static object GetInfo(CommandParams p)
        {
            string path = p.GetString("path");
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                throw new System.InvalidOperationException($"Asset not found: '{path}'");
            }

            return new Dictionary<string, object>
            {
                { "path", path },
                { "guid", AssetDatabase.AssetPathToGUID(path) },
                { "type", asset.GetType().Name },
                { "dependencies", new List<object>(AssetDatabase.GetDependencies(path, false)) }
            };
        }

        private static object CreateFolder(CommandParams p)
        {
            string path = p.GetString("path");
            AssetPathGuard.RequireUnderAssets(path);

            if (AssetDatabase.IsValidFolder(path))
            {
                return new Dictionary<string, object> { { "created", false }, { "path", path }, { "existed", true } };
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            string guid = AssetDatabase.CreateFolder(parent, folderName);
            return new Dictionary<string, object>
            {
                { "created", !string.IsNullOrEmpty(guid) },
                { "path", AssetDatabase.GUIDToAssetPath(guid) }
            };
        }

        private static object Delete(CommandParams p)
        {
            string path = p.GetString("path");
            AssetPathGuard.RequireUnderAssets(path);
            bool deleted = AssetDatabase.DeleteAsset(path);
            return new Dictionary<string, object> { { "deleted", deleted }, { "path", path } };
        }

        private static object PrefabInstantiate(CommandParams p)
        {
            string path = p.GetString("path");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new System.InvalidOperationException($"Prefab not found: '{path}'");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            float[] pos = p.GetVector3("position");
            if (pos != null)
            {
                instance.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            string parent = p.GetString("parent");
            if (!string.IsNullOrEmpty(parent))
            {
                instance.transform.SetParent(HandlerUtil.RequireGameObject(parent).transform, true);
            }

            Undo.RegisterCreatedObjectUndo(instance, "MCP Instantiate Prefab");
            return HandlerUtil.DescribeGameObject(instance);
        }

        private static object PrefabCreate(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            string path = p.GetString("path");
            AssetPathGuard.RequireUnderAssets(path);

            var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
                go, path, InteractionMode.UserAction);
            return new Dictionary<string, object>
            {
                { "created", saved != null },
                { "path", path }
            };
        }

        private static object PrefabApply(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);
            return new Dictionary<string, object>
            {
                { "applied", true },
                { "target", HandlerUtil.GetHierarchyPath(go.transform) }
            };
        }
    }
}
