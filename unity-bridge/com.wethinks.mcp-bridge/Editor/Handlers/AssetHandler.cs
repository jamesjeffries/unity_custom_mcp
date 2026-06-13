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
            CommandRegistry.Register("asset.import_binary", ImportBinary);
            CommandRegistry.Register("material.create_from_texture", MaterialFromTexture);
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

        /// <summary>
        /// Writes base64-encoded bytes to a sandboxed path under Assets/ and
        /// imports the result. Used by the AI texture/audio tools to land
        /// generated PNG/MP3 files in the project.
        /// </summary>
        private static object ImportBinary(CommandParams p)
        {
            string path = p.GetString("path");
            AssetPathGuard.RequireUnderAssets(path);

            string b64 = p.GetString("data_base64");
            if (string.IsNullOrEmpty(b64))
            {
                throw new System.ArgumentException("'data_base64' is required.");
            }

            byte[] bytes = System.Convert.FromBase64String(b64);
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return new Dictionary<string, object>
            {
                { "imported", true },
                { "path", path },
                { "guid", AssetDatabase.AssetPathToGUID(path) },
                { "type", AssetDatabase.GetMainAssetTypeAtPath(path)?.Name },
                { "bytes", bytes.Length }
            };
        }

        /// <summary>
        /// Creates a material that uses the given texture and optionally assigns
        /// it to a target GameObject's renderer. Works with URP (_BaseMap) and
        /// the built-in pipeline (_MainTex).
        /// </summary>
        private static object MaterialFromTexture(CommandParams p)
        {
            string texturePath = p.GetString("texture");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                throw new System.InvalidOperationException($"Texture not found: '{texturePath}'");
            }

            string name = p.GetString("name", texture.name);
            string targetName = p.GetString("target");
            Renderer renderer = null;
            if (!string.IsNullOrEmpty(targetName))
            {
                renderer = HandlerUtil.RequireGameObject(targetName).GetComponent<Renderer>();
            }

            Shader shader =
                renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null
                    ? renderer.sharedMaterial.shader
                    : (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            float[] color = p.GetColor("color");
            if (color != null)
            {
                var col = new Color(color[0], color[1], color[2], color[3]);
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", col);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", col);
                }
            }

            EnsureFolder("Assets/MCP/Materials");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"Assets/MCP/Materials/{Sanitize(name)}.mat");
            AssetDatabase.CreateAsset(material, assetPath);

            if (renderer != null)
            {
                Undo.RecordObject(renderer, "MCP Apply Texture Material");
                renderer.sharedMaterial = material;
            }

            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "material", assetPath },
                { "texture", texturePath },
                {
                    "appliedTo",
                    renderer != null ? HandlerUtil.GetHierarchyPath(renderer.transform) : null
                }
            };
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string Sanitize(string name)
        {
            foreach (char ch in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(ch, '_');
            }

            return string.IsNullOrEmpty(name) ? "Asset" : name;
        }
    }
}
