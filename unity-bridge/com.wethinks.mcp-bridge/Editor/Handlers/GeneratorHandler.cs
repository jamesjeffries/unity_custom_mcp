using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WeThinks.Mcp.Editor
{
    /// <summary>
    /// High-level "generator" commands that compose many primitive operations
    /// into a single round-trip: a first-person player, procedural terrain, and
    /// prefab/primitive scattering.
    /// </summary>
    internal static class GeneratorHandler
    {
        public static void Register()
        {
            CommandRegistry.Register("generator.first_person_player", FirstPersonPlayer);
            CommandRegistry.Register("generator.terrain", Terrain);
            CommandRegistry.Register("generator.scatter", Scatter);
        }

        // ------------------------------------------------------------------
        // First-person player
        // ------------------------------------------------------------------
        private static object FirstPersonPlayer(CommandParams p)
        {
            string name = p.GetString("name", "FPSPlayer");
            float[] pos = p.GetVector3("position");
            Vector3 position = pos != null
                ? new Vector3(pos[0], pos[1], pos[2])
                : new Vector3(0f, 1f, 0f);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = name;

            // The CharacterController acts as the collider, so drop the capsule's.
            var capsule = player.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                UnityEngine.Object.DestroyImmediate(capsule);
            }

            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);
            player.transform.position = position;

            // Eye-height camera child.
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camGo.AddComponent<Camera>();
            if (!SceneHasAudioListener())
            {
                camGo.AddComponent<AudioListener>();
            }

            // Attach the precompiled behaviour by reflection so the Editor
            // assembly needs no compile-time dependency on the runtime one.
            Type fpc = FindType("McpFirstPersonController");
            if (fpc == null)
            {
                UnityEngine.Object.DestroyImmediate(player);
                throw new InvalidOperationException(
                    "McpFirstPersonController not found. Reimport the MCP Bridge package " +
                    "so its Runtime assembly compiles.");
            }

            Component behaviour = player.AddComponent(fpc);
            ApplyFloatField(behaviour, "walkSpeed", p, "walk_speed");
            ApplyFloatField(behaviour, "crawlSpeed", p, "crawl_speed");
            ApplyFloatField(behaviour, "jumpHeight", p, "jump_height");
            ApplyFloatField(behaviour, "mouseSensitivity", p, "mouse_sensitivity");

            Undo.RegisterCreatedObjectUndo(player, "MCP First Person Player");
            Selection.activeGameObject = player;

            return new Dictionary<string, object>
            {
                { "player", HandlerUtil.GetHierarchyPath(player.transform) },
                { "camera", HandlerUtil.GetHierarchyPath(camGo.transform) },
                { "controller", fpc.Name },
                { "position", HandlerUtil.Vector3ToDict(position) },
                { "controls", "WASD move, mouse look, Space jump, Left Ctrl/C crawl" }
            };
        }

        // ------------------------------------------------------------------
        // Procedural terrain
        // ------------------------------------------------------------------
        private static object Terrain(CommandParams p)
        {
            string name = p.GetString("name", "Terrain");
            float width = p.GetFloat("width", 500f);
            float length = p.GetFloat("length", 500f);
            float height = p.GetFloat("height", 120f);
            int resolution = NormalizeHeightmapResolution(p.GetInt("resolution", 257));
            int seed = p.GetInt("seed", 0);
            float mountainStrength = Mathf.Clamp01(p.GetFloat("mountain_strength", 0.8f));
            float meadowFraction = Mathf.Clamp01(p.GetFloat("meadow_fraction", 0.4f));
            float[] pos = p.GetVector3("position");

            var data = new TerrainData
            {
                heightmapResolution = resolution,
                size = new Vector3(width, height, length)
            };

            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 1000f;
            float oy = (float)rng.NextDouble() * 1000f;

            float[,] heights = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
            {
                float v = (float)y / (resolution - 1);
                for (int x = 0; x < resolution; x++)
                {
                    float u = (float)x / (resolution - 1);

                    // Rolling base hills; meadows live in the low areas.
                    float hills = Fbm(u * 3f + ox, v * 3f + oy, 4, 0.5f) * 0.18f;

                    // A mountain range running across the terrain, masked to a band.
                    float ridge = RidgedFbm(u * 2f + ox, v * 2f + oy, 5, 0.5f);
                    float band = Mathf.Exp(-Mathf.Pow((v - 0.5f) / 0.18f, 2f));
                    float mountains = ridge * band * mountainStrength;

                    float h = hills + mountains;

                    // Flatten meadows away from the mountain band.
                    h = Mathf.Lerp(h, h * 0.25f, meadowFraction * (1f - band));

                    heights[y, x] = Mathf.Clamp01(h);
                }
            }

            data.SetHeights(0, 0, heights);

            const string folder = "Assets/MCP/Terrain";
            EnsureFolder(folder);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{Sanitize(name)}.asset");
            AssetDatabase.CreateAsset(data, assetPath);

            GameObject go = UnityEngine.Terrain.CreateTerrainGameObject(data);
            go.name = name;
            if (pos != null)
            {
                go.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            }

            AssetDatabase.SaveAssets();
            Undo.RegisterCreatedObjectUndo(go, "MCP Generate Terrain");
            Selection.activeGameObject = go;

            return new Dictionary<string, object>
            {
                { "terrain", HandlerUtil.GetHierarchyPath(go.transform) },
                { "terrainData", assetPath },
                {
                    "size", new Dictionary<string, object>
                    {
                        { "width", width },
                        { "length", length },
                        { "height", height }
                    }
                },
                { "heightmapResolution", resolution },
                { "seed", seed }
            };
        }

        // ------------------------------------------------------------------
        // Scatter
        // ------------------------------------------------------------------
        private static object Scatter(CommandParams p)
        {
            int count = Mathf.Max(0, p.GetInt("count", 50));
            string prefabPath = p.GetString("prefab");
            string primitive = p.GetString("primitive");
            float[] center = p.GetVector3("area_center") ?? new[] { 0f, 0f, 0f };
            float[] size = p.GetVector3("area_size") ?? new[] { 50f, 0f, 50f };
            bool alignGround = p.GetBool("align_to_ground", true);
            bool randomYaw = p.GetBool("random_yaw", true);
            float minScale = p.GetFloat("min_scale", 1f);
            float maxScale = p.GetFloat("max_scale", 1f);
            int seed = p.GetInt("seed", 0);
            string parentName = p.GetString("parent");
            int groundMask = ResolveGroundMask(p);
            bool requireGround = p.GetBool("require_ground", false);

            GameObject prefab = null;
            if (!string.IsNullOrEmpty(prefabPath))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException($"Prefab not found: '{prefabPath}'");
                }
            }
            else if (string.IsNullOrEmpty(primitive))
            {
                throw new ArgumentException("Provide either 'prefab' (asset path) or 'primitive'.");
            }

            Transform parent;
            if (!string.IsNullOrEmpty(parentName))
            {
                parent = HandlerUtil.RequireGameObject(parentName).transform;
            }
            else
            {
                var container = new GameObject(
                    $"{(prefab != null ? prefab.name : primitive)} Scatter");
                Undo.RegisterCreatedObjectUndo(container, "MCP Scatter");
                parent = container.transform;
            }

            Physics.SyncTransforms();
            var rng = new System.Random(seed);
            Vector3 c = new Vector3(center[0], center[1], center[2]);
            Vector3 s = new Vector3(size[0], size[1], size[2]);
            float rayTop = c.y + Mathf.Max(s.y, 1000f);
            float rayLength = rayTop - (c.y - 1000f);

            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                float rx = c.x + ((float)rng.NextDouble() - 0.5f) * s.x;
                float rz = c.z + ((float)rng.NextDouble() - 0.5f) * s.z;
                float ry = c.y;

                if (alignGround)
                {
                    bool hitGround = Physics.Raycast(
                        new Vector3(rx, rayTop, rz), Vector3.down, out RaycastHit hit,
                        rayLength, groundMask, QueryTriggerInteraction.Ignore);
                    if (hitGround)
                    {
                        ry = hit.point.y;
                    }
                    else if (requireGround)
                    {
                        // No surface on the target layer(s) here: skip rather
                        // than dropping the instance at the area's base height.
                        continue;
                    }
                }

                GameObject inst = prefab != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                    : GameObject.CreatePrimitive(ParsePrimitive(primitive));

                inst.transform.position = new Vector3(rx, ry, rz);
                if (randomYaw)
                {
                    inst.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                }

                float scale = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
                inst.transform.localScale *= scale;
                inst.transform.SetParent(parent, true);
                Undo.RegisterCreatedObjectUndo(inst, "MCP Scatter Instance");
                placed++;
            }

            return new Dictionary<string, object>
            {
                { "placed", placed },
                { "parent", HandlerUtil.GetHierarchyPath(parent) },
                { "source", prefab != null ? prefabPath : primitive }
            };
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private static int ResolveGroundMask(CommandParams p)
        {
            int mask = 0;
            string single = p.GetString("ground_layer");
            if (!string.IsNullOrEmpty(single))
            {
                int l = LayerMask.NameToLayer(single);
                if (l >= 0)
                {
                    mask |= 1 << l;
                }
            }

            if (p.Raw("ground_layers") is List<object> names)
            {
                foreach (object n in names)
                {
                    int l = LayerMask.NameToLayer(n?.ToString());
                    if (l >= 0)
                    {
                        mask |= 1 << l;
                    }
                }
            }

            // Default to hitting everything when no ground layer is specified.
            return mask == 0 ? ~0 : mask;
        }

        private static bool SceneHasAudioListener()
        {
            return UnityEngine.Object.FindFirstObjectByType<AudioListener>() != null;
        }

        private static Type FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType($"WeThinks.Mcp.Runtime.{typeName}");
                if (t != null)
                {
                    return t;
                }
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type t in asm.GetTypes())
                {
                    if (t.Name == typeName)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private static void ApplyFloatField(Component component, string fieldName, CommandParams p, string key)
        {
            if (!p.Has(key))
            {
                return;
            }

            var field = component.GetType().GetField(fieldName);
            if (field != null && field.FieldType == typeof(float))
            {
                field.SetValue(component, p.GetFloat(key));
            }
        }

        private static PrimitiveType ParsePrimitive(string primitive)
        {
            if (!Enum.TryParse(primitive, true, out PrimitiveType type))
            {
                throw new ArgumentException($"Unknown primitive '{primitive}'.");
            }

            return type;
        }

        private static float Fbm(float x, float y, int octaves, float persistence)
        {
            float total = 0f, amp = 1f, freq = 1f, max = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                max += amp;
                amp *= persistence;
                freq *= 2f;
            }

            return max > 0f ? total / max : 0f;
        }

        private static float RidgedFbm(float x, float y, int octaves, float persistence)
        {
            float total = 0f, amp = 1f, freq = 1f, max = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * freq, y * freq) * 2f - 1f);
                total += n * n * amp;
                max += amp;
                amp *= persistence;
                freq *= 2f;
            }

            return max > 0f ? total / max : 0f;
        }

        private static int NormalizeHeightmapResolution(int res)
        {
            int[] valid = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
            foreach (int v in valid)
            {
                if (res <= v)
                {
                    return v;
                }
            }

            return 4097;
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
