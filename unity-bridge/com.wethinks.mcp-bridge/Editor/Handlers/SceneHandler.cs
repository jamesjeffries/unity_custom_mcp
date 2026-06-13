using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace WeThinks.Mcp.Editor
{
    internal static class SceneHandler
    {
        public static void Register()
        {
            CommandRegistry.Register("scene.get_active", GetActive);
            CommandRegistry.Register("scene.get_hierarchy", GetHierarchy);
            CommandRegistry.Register("scene.open", Open);
            CommandRegistry.Register("scene.save", Save);
            CommandRegistry.Register("scene.new", New);
        }

        private static object GetActive(CommandParams p)
        {
            Scene scene = SceneManager.GetActiveScene();
            return new Dictionary<string, object>
            {
                { "name", scene.name },
                { "path", scene.path },
                { "isDirty", scene.isDirty },
                { "rootCount", scene.rootCount }
            };
        }

        private static object GetHierarchy(CommandParams p)
        {
            bool includeComponents = p.GetBool("include_components");
            var roots = new List<object>();

            foreach (var root in HandlerUtil.EnumerateRootObjects())
            {
                roots.Add(DescribeNode(root.transform, includeComponents));
            }

            return new Dictionary<string, object>
            {
                { "scene", SceneManager.GetActiveScene().name },
                { "roots", roots }
            };
        }

        private static Dictionary<string, object> DescribeNode(
            UnityEngine.Transform t, bool includeComponents)
        {
            var node = new Dictionary<string, object>
            {
                { "name", t.name },
                { "path", HandlerUtil.GetHierarchyPath(t) },
                { "activeSelf", t.gameObject.activeSelf }
            };

            if (includeComponents)
            {
                var componentNames = new List<object>();
                foreach (var c in t.GetComponents<UnityEngine.Component>())
                {
                    if (c != null)
                    {
                        componentNames.Add(c.GetType().Name);
                    }
                }

                node["components"] = componentNames;
            }

            if (t.childCount > 0)
            {
                var children = new List<object>();
                for (int i = 0; i < t.childCount; i++)
                {
                    children.Add(DescribeNode(t.GetChild(i), includeComponents));
                }

                node["children"] = children;
            }

            return node;
        }

        private static object Open(CommandParams p)
        {
            string path = p.GetString("path");
            if (string.IsNullOrEmpty(path))
            {
                throw new System.ArgumentException("path is required");
            }

            OpenSceneMode mode = p.GetBool("additive")
                ? OpenSceneMode.Additive
                : OpenSceneMode.Single;
            Scene scene = EditorSceneManager.OpenScene(path, mode);
            return new Dictionary<string, object>
            {
                { "opened", scene.name },
                { "path", scene.path }
            };
        }

        private static object Save(CommandParams p)
        {
            Scene scene = SceneManager.GetActiveScene();
            string path = p.GetString("path");
            bool saved = string.IsNullOrEmpty(path)
                ? EditorSceneManager.SaveScene(scene)
                : EditorSceneManager.SaveScene(scene, path);

            return new Dictionary<string, object>
            {
                { "saved", saved },
                { "path", string.IsNullOrEmpty(path) ? scene.path : path }
            };
        }

        private static object New(CommandParams p)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            string path = p.GetString("path");
            if (!string.IsNullOrEmpty(path))
            {
                EditorSceneManager.SaveScene(scene, path);
            }

            return new Dictionary<string, object>
            {
                { "created", true },
                { "path", string.IsNullOrEmpty(path) ? scene.path : path }
            };
        }
    }
}
