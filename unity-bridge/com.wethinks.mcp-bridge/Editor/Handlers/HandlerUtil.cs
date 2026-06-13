using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeThinks.Mcp.Editor
{
    /// <summary>Shared lookup/serialization helpers used by multiple handlers.</summary>
    internal static class HandlerUtil
    {
        /// <summary>
        /// Finds a GameObject by hierarchy path (preferred, e.g. "Parent/Child")
        /// or by plain name as a fallback. Returns null if not found.
        /// </summary>
        public static GameObject FindGameObject(string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return null;
            }

            // Try an exact hierarchy path first.
            GameObject byPath = GameObject.Find(target);
            if (byPath != null)
            {
                return byPath;
            }

            // Fall back to a name scan across loaded scenes (includes inactive).
            foreach (GameObject root in EnumerateRootObjects())
            {
                GameObject match = SearchByName(root.transform, target);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        public static GameObject RequireGameObject(string target)
        {
            GameObject go = FindGameObject(target);
            if (go == null)
            {
                throw new System.InvalidOperationException($"GameObject not found: '{target}'");
            }

            return go;
        }

        private static GameObject SearchByName(Transform current, string name)
        {
            if (current.gameObject.name == name)
            {
                return current.gameObject;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                GameObject found = SearchByName(current.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static IEnumerable<GameObject> EnumerateRootObjects()
        {
            var roots = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    roots.AddRange(scene.GetRootGameObjects());
                }
            }

            return roots;
        }

        public static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        public static Dictionary<string, object> Vector3ToDict(Vector3 v)
        {
            return new Dictionary<string, object>
            {
                { "x", v.x },
                { "y", v.y },
                { "z", v.z }
            };
        }

        public static Dictionary<string, object> DescribeGameObject(GameObject go)
        {
            return new Dictionary<string, object>
            {
                { "name", go.name },
                { "path", GetHierarchyPath(go.transform) },
                { "activeSelf", go.activeSelf },
                { "instanceId", go.GetInstanceID() }
            };
        }
    }
}
