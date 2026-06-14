using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WeThinks.Mcp.Editor
{
    internal static class GameObjectHandler
    {
        public static void Register()
        {
            CommandRegistry.Register("gameobject.create", Create);
            CommandRegistry.Register("gameobject.find", Find);
            CommandRegistry.Register("gameobject.get_components", GetComponents);
            CommandRegistry.Register("gameobject.add_component", AddComponent);
            CommandRegistry.Register("gameobject.set_transform", SetTransform);
            CommandRegistry.Register("gameobject.set_property", SetProperty);
            CommandRegistry.Register("gameobject.set_color", SetColor);
            CommandRegistry.Register("gameobject.set_parent", SetParent);
            CommandRegistry.Register("gameobject.set_tag", SetTag);
            CommandRegistry.Register("gameobject.set_layer", SetLayer);
            CommandRegistry.Register("gameobject.remove_component", RemoveComponent);
            CommandRegistry.Register("gameobject.delete", Delete);
        }

        private static object Create(CommandParams p)
        {
            string name = p.GetString("name", "GameObject");
            string primitive = p.GetString("primitive");

            GameObject go;
            if (string.IsNullOrEmpty(primitive))
            {
                go = new GameObject(name);
            }
            else
            {
                if (!Enum.TryParse(primitive, true, out PrimitiveType type))
                {
                    throw new ArgumentException(
                        $"Unknown primitive '{primitive}'. Use Cube, Sphere, " +
                        "Capsule, Cylinder, Plane, or Quad.");
                }

                go = GameObject.CreatePrimitive(type);
                go.name = name;
            }

            string parent = p.GetString("parent");
            if (!string.IsNullOrEmpty(parent))
            {
                go.transform.SetParent(HandlerUtil.RequireGameObject(parent).transform, true);
            }

            float[] pos = p.GetVector3("position");
            if (pos != null)
            {
                go.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            Undo.RegisterCreatedObjectUndo(go, "MCP Create GameObject");
            Selection.activeGameObject = go;
            return HandlerUtil.DescribeGameObject(go);
        }

        private static object Find(CommandParams p)
        {
            string query = p.GetString("query");
            var matches = new List<object>();

            foreach (var root in HandlerUtil.EnumerateRootObjects())
            {
                CollectMatches(root.transform, query, matches);
            }

            return new Dictionary<string, object>
            {
                { "query", query },
                { "count", matches.Count },
                { "matches", matches }
            };
        }

        private static void CollectMatches(Transform t, string query, List<object> matches)
        {
            string path = HandlerUtil.GetHierarchyPath(t);
            if (t.name == query || path == query ||
                (query != null && t.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                matches.Add(HandlerUtil.DescribeGameObject(t.gameObject));
            }

            for (int i = 0; i < t.childCount; i++)
            {
                CollectMatches(t.GetChild(i), query, matches);
            }
        }

        private static object GetComponents(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            var components = new List<object>();

            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null)
                {
                    continue;
                }

                var info = new Dictionary<string, object>
                {
                    { "type", c.GetType().Name },
                    { "fullType", c.GetType().FullName }
                };

                var so = new SerializedObject(c);
                var props = new Dictionary<string, object>();
                SerializedProperty iterator = so.GetIterator();
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        props[iterator.name] = DescribeProperty(iterator);
                    }
                    while (iterator.NextVisible(false));
                }

                info["properties"] = props;
                components.Add(info);
            }

            return new Dictionary<string, object>
            {
                { "target", HandlerUtil.GetHierarchyPath(go.transform) },
                { "components", components }
            };
        }

        private static object DescribeProperty(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue;
                case SerializedPropertyType.Boolean:
                    return prop.boolValue;
                case SerializedPropertyType.Float:
                    return prop.floatValue;
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Vector3:
                    return HandlerUtil.Vector3ToDict(prop.vector3Value);
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? prop.objectReferenceValue.name
                        : null;
                default:
                    return prop.propertyType.ToString();
            }
        }

        private static object AddComponent(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            string typeName = p.GetString("component_type");
            Type type = ResolveComponentType(typeName);
            if (type == null)
            {
                throw new ArgumentException($"Component type not found: '{typeName}'");
            }

            Component added = Undo.AddComponent(go, type);
            return new Dictionary<string, object>
            {
                { "added", added != null },
                { "type", type.Name },
                { "target", HandlerUtil.GetHierarchyPath(go.transform) }
            };
        }

        private static Type ResolveComponentType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            // Try common UnityEngine namespace first, then a full assembly scan.
            Type direct = Type.GetType($"UnityEngine.{typeName}, UnityEngine") ??
                          Type.GetType(typeName);
            if (direct != null && typeof(Component).IsAssignableFrom(direct))
            {
                return direct;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in assembly.GetTypes())
                {
                    if ((t.Name == typeName || t.FullName == typeName) &&
                        typeof(Component).IsAssignableFrom(t))
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private static object SetTransform(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            Undo.RecordObject(go.transform, "MCP Set Transform");

            float[] pos = p.GetVector3("position");
            if (pos != null)
            {
                go.transform.localPosition = new Vector3(pos[0], pos[1], pos[2]);
            }

            float[] rot = p.GetVector3("rotation");
            if (rot != null)
            {
                go.transform.localEulerAngles = new Vector3(rot[0], rot[1], rot[2]);
            }

            float[] scale = p.GetVector3("scale");
            if (scale != null)
            {
                go.transform.localScale = new Vector3(scale[0], scale[1], scale[2]);
            }

            return new Dictionary<string, object>
            {
                { "target", HandlerUtil.GetHierarchyPath(go.transform) },
                { "localPosition", HandlerUtil.Vector3ToDict(go.transform.localPosition) },
                { "localEulerAngles", HandlerUtil.Vector3ToDict(go.transform.localEulerAngles) },
                { "localScale", HandlerUtil.Vector3ToDict(go.transform.localScale) }
            };
        }

        private static object SetProperty(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            string componentType = p.GetString("component_type");
            string propertyName = p.GetString("property_name");

            Component component = null;
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == componentType)
                {
                    component = c;
                    break;
                }
            }

            if (component == null)
            {
                throw new InvalidOperationException(
                    $"Component '{componentType}' not found on '{go.name}'");
            }

            var so = new SerializedObject(component);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                throw new InvalidOperationException(
                    $"Property '{propertyName}' not found on '{componentType}'");
            }

            ApplyValue(prop, p.Raw("value"), p.GetString("value_type"));
            so.ApplyModifiedProperties();

            return new Dictionary<string, object>
            {
                { "target", HandlerUtil.GetHierarchyPath(go.transform) },
                { "component", componentType },
                { "property", propertyName }
            };
        }

        private static void ApplyValue(SerializedProperty prop, object value, string valueType)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value?.ToString();
                    break;
                case SerializedPropertyType.Vector3:
                    if (value is List<object> list && list.Count >= 3)
                    {
                        prop.vector3Value = new Vector3(
                            Convert.ToSingle(list[0]),
                            Convert.ToSingle(list[1]),
                            Convert.ToSingle(list[2]));
                    }

                    break;
                case SerializedPropertyType.Enum:
                    ApplyEnum(prop, value);
                    break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = ResolveObjectReference(value, valueType);
                    break;
                case SerializedPropertyType.LayerMask:
                    prop.intValue = ResolveLayerMask(value);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Setting properties of type {prop.propertyType} is not supported.");
            }
        }

        private static void ApplyEnum(SerializedProperty prop, object value)
        {
            if (value is string enumName)
            {
                int idx = Array.IndexOf(prop.enumNames, enumName);
                if (idx < 0)
                {
                    idx = Array.IndexOf(prop.enumDisplayNames, enumName);
                }

                if (idx < 0)
                {
                    throw new ArgumentException(
                        $"Enum value '{enumName}' is not valid. Options: " +
                        string.Join(", ", prop.enumNames));
                }

                prop.enumValueIndex = idx;
                return;
            }

            int numeric = Convert.ToInt32(value);
            if (numeric >= 0 && numeric < prop.enumNames.Length)
            {
                prop.enumValueIndex = numeric;
            }
            else
            {
                prop.intValue = numeric;
            }
        }

        private static UnityEngine.Object ResolveObjectReference(object value, string valueType)
        {
            if (value == null)
            {
                return null;
            }

            string s = value.ToString();
            if (string.IsNullOrEmpty(s) || s == "null")
            {
                return null;
            }

            // Asset reference: a project path under Assets/.
            if (s.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                Type assetType = ResolveUnityType(valueType);
                UnityEngine.Object asset = assetType != null
                    ? AssetDatabase.LoadAssetAtPath(s, assetType)
                    : null;
                UnityEngine.Object resolved = asset ?? AssetDatabase.LoadMainAssetAtPath(s);
                if (resolved == null)
                {
                    throw new InvalidOperationException($"Asset not found: '{s}'");
                }

                return resolved;
            }

            // Scene reference: a GameObject (or one of its components).
            GameObject go = HandlerUtil.FindGameObject(s);
            if (go == null)
            {
                throw new InvalidOperationException($"Object reference target not found: '{s}'");
            }

            if (!string.IsNullOrEmpty(valueType) && valueType != "GameObject")
            {
                Type ct = ResolveUnityType(valueType);
                if (ct != null && typeof(Component).IsAssignableFrom(ct))
                {
                    Component comp = go.GetComponent(ct);
                    if (comp == null)
                    {
                        throw new InvalidOperationException(
                            $"GameObject '{s}' has no '{valueType}' component.");
                    }

                    return comp;
                }
            }

            return go;
        }

        private static int ResolveLayerMask(object value)
        {
            switch (value)
            {
                case null:
                    return 0;
                case long l:
                    return (int)l;
                case double d:
                    return (int)d;
                case List<object> names:
                    int mask = 0;
                    foreach (object n in names)
                    {
                        mask |= LayerToBit(n?.ToString());
                    }

                    return mask;
                default:
                    string s = value.ToString();
                    if (int.TryParse(s, out int parsed))
                    {
                        return parsed;
                    }

                    if (s == "Everything")
                    {
                        return ~0;
                    }

                    if (s == "Nothing")
                    {
                        return 0;
                    }

                    return LayerToBit(s);
            }
        }

        private static int LayerToBit(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                return 0;
            }

            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? 1 << layer : 0;
        }

        private static Type ResolveUnityType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            Type direct = Type.GetType($"UnityEngine.{typeName}, UnityEngine") ??
                          Type.GetType(typeName);
            if (direct != null)
            {
                return direct;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == typeName || t.FullName == typeName)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private static object SetColor(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            float[] rgba = p.GetColor("color");
            if (rgba == null)
            {
                throw new ArgumentException(
                    "'color' must be an [r, g, b] or [r, g, b, a] array with " +
                    "components in the 0..1 range.");
            }

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                throw new InvalidOperationException(
                    $"GameObject '{go.name}' has no Renderer to color.");
            }

            var color = new Color(rgba[0], rgba[1], rgba[2], rgba[3]);

            // Reuse the existing shader so the material looks right under the
            // project's render pipeline (URP/HDRP/Built-in); fall back sensibly.
            Shader shader = renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null
                ? renderer.sharedMaterial.shader
                : Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");

            var material = new Material(shader) { name = go.name + " Material" };
            ApplyColor(material, color);

            const string folder = "Assets/MCP/Materials";
            EnsureFolder(folder);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{SanitizeFileName(go.name)}.mat");
            AssetDatabase.CreateAsset(material, assetPath);

            Undo.RecordObject(renderer, "MCP Set Color");
            renderer.sharedMaterial = material;
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "target", HandlerUtil.GetHierarchyPath(go.transform) },
                { "material", assetPath },
                { "color", new Dictionary<string, object>
                    {
                        { "r", color.r },
                        { "g", color.g },
                        { "b", color.b },
                        { "a", color.a }
                    }
                }
            };
        }

        private static void ApplyColor(Material material, Color color)
        {
            // URP/HDRP use _BaseColor; the built-in pipeline uses _Color.
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            material.color = color;
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

        private static string SanitizeFileName(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return string.IsNullOrEmpty(name) ? "Material" : name;
        }

        private static object SetParent(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            string parent = p.GetString("parent");
            bool keepWorld = p.GetBool("keep_world_position", true);

            Undo.SetTransformParent(
                go.transform,
                string.IsNullOrEmpty(parent) ? null : HandlerUtil.RequireGameObject(parent).transform,
                "MCP Set Parent");

            if (!keepWorld)
            {
                go.transform.localPosition = Vector3.zero;
            }

            return new Dictionary<string, object>
            {
                { "target", HandlerUtil.GetHierarchyPath(go.transform) },
                { "parent", string.IsNullOrEmpty(parent) ? null : parent }
            };
        }

        private static object SetTag(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            string tag = p.GetString("tag");
            if (string.IsNullOrEmpty(tag))
            {
                throw new ArgumentException("'tag' is required.");
            }

            if (p.GetBool("create_if_missing", true))
            {
                EnsureTagExists(tag);
            }

            Undo.RecordObject(go, "MCP Set Tag");
            go.tag = tag;
            return new Dictionary<string, object>
            {
                { "target", HandlerUtil.GetHierarchyPath(go.transform) },
                { "tag", go.tag }
            };
        }

        private static object SetLayer(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            string layerName = p.GetString("layer");
            bool includeChildren = p.GetBool("include_children", false);

            int layer;
            if (!string.IsNullOrEmpty(layerName))
            {
                layer = LayerMask.NameToLayer(layerName);
                if (layer < 0 && p.GetBool("create_if_missing", true))
                {
                    layer = EnsureLayerExists(layerName);
                }

                if (layer < 0)
                {
                    throw new ArgumentException($"Layer '{layerName}' does not exist.");
                }
            }
            else
            {
                layer = p.GetInt("layer_index", 0);
            }

            Undo.RecordObject(go, "MCP Set Layer");
            SetLayerRecursive(go, layer, includeChildren);
            return new Dictionary<string, object>
            {
                { "target", HandlerUtil.GetHierarchyPath(go.transform) },
                { "layer", layer },
                { "layerName", LayerMask.LayerToName(layer) },
                { "includeChildren", includeChildren }
            };
        }

        private static object RemoveComponent(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            string typeName = p.GetString("component_type");
            if (string.IsNullOrEmpty(typeName))
            {
                throw new ArgumentException("'component_type' is required.");
            }

            Component target = null;
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c != null &&
                    (c.GetType().Name == typeName || c.GetType().FullName == typeName))
                {
                    target = c;
                    break;
                }
            }

            if (target == null)
            {
                throw new InvalidOperationException(
                    $"Component '{typeName}' not found on '{go.name}'.");
            }

            if (target is Transform)
            {
                throw new InvalidOperationException("The Transform component cannot be removed.");
            }

            string path = HandlerUtil.GetHierarchyPath(go.transform);
            Undo.DestroyObjectImmediate(target);
            return new Dictionary<string, object>
            {
                { "removed", true },
                { "target", path },
                { "component", typeName }
            };
        }

        private static void SetLayerRecursive(GameObject go, int layer, bool recurse)
        {
            go.layer = layer;
            if (!recurse)
            {
                return;
            }

            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer, true);
            }
        }

        private static void EnsureTagExists(string tag)
        {
            foreach (string existing in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (existing == tag)
                {
                    return;
                }
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
            {
                return;
            }

            var so = new SerializedObject(assets[0]);
            SerializedProperty tagsProp = so.FindProperty("tags");
            if (tagsProp == null)
            {
                return;
            }

            int idx = tagsProp.arraySize;
            tagsProp.InsertArrayElementAtIndex(idx);
            tagsProp.GetArrayElementAtIndex(idx).stringValue = tag;
            so.ApplyModifiedProperties();
        }

        private static int EnsureLayerExists(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
            {
                return existing;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
            {
                return -1;
            }

            var so = new SerializedObject(assets[0]);
            SerializedProperty layersProp = so.FindProperty("layers");
            if (layersProp == null)
            {
                return -1;
            }

            // User-definable layers occupy slots 8..31.
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty el = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = layerName;
                    so.ApplyModifiedProperties();
                    return i;
                }
            }

            return -1;
        }

        private static object Delete(CommandParams p)
        {
            GameObject go = HandlerUtil.RequireGameObject(p.GetString("target"));
            string path = HandlerUtil.GetHierarchyPath(go.transform);
            Undo.DestroyObjectImmediate(go);
            return new Dictionary<string, object>
            {
                { "deleted", true },
                { "path", path }
            };
        }
    }
}
