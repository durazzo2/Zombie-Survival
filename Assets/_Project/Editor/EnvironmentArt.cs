using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZombieStealth.Editor
{
    /// <summary>
    /// Visual-only models built from primitives and a few generated meshes (trees, rocks, bushes, zombie body).
    /// Nothing here has colliders or gameplay logic — LevelBuilder adds the (simple) colliders on the root objects.
    /// Generated meshes are saved under Assets/_Project/Meshes so the scene can reference them.
    /// </summary>
    public static class EnvironmentArt
    {
        const string MeshFolder = "Assets/_Project/Meshes";

        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        /// <summary>Called at the start of every build so meshes are regenerated once per build.</summary>
        public static void ResetCache() => meshCache.Clear();

        // ------------------------------------------------------------------ deterministic randomness

        /// <summary>Random generator seeded from a position: same spot → same tree/rock every build.</summary>
        public static System.Random RandomFor(Vector3 position)
        {
            unchecked
            {
                int hash = Mathf.RoundToInt(position.x * 100f) * 73856093 ^ Mathf.RoundToInt(position.z * 100f) * 19349663;
                return new System.Random(hash);
            }
        }

        public static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        // ------------------------------------------------------------------ meshes

        public static Mesh RockMesh(int variant) => GetMesh($"Rock_{variant}", () => NoisySphere(11 + variant * 37, 0.22f, 1.6f));
        public static Mesh FoliageMesh(int variant) => GetMesh($"Foliage_{variant}", () => NoisySphere(101 + variant * 53, 0.2f, 2.2f));
        public static Mesh ConeMesh() => GetMesh("Cone", () => Cone(12));

        static Mesh GetMesh(string name, Func<Mesh> build)
        {
            if (meshCache.TryGetValue(name, out Mesh cached))
                return cached;

            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Meshes");

            string path = $"{MeshFolder}/{name}.asset";
            Mesh fresh = build();
            fresh.name = name;
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                asset = fresh;
            }
            else
            {
                EditorUtility.CopySerialized(fresh, asset); // keep the asset (and its GUID), update the data
                Object.DestroyImmediate(fresh);
            }

            meshCache[name] = asset;
            return asset;
        }

        /// <summary>Unity's sphere with every vertex pushed in/out by smooth noise → a lumpy, natural shape.</summary>
        static Mesh NoisySphere(int seed, float amplitude, float frequency)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh source = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);

            Vector3[] vertices = source.vertices;
            float ox = seed * 0.37f, oy = seed * 0.73f, oz = seed * 0.19f;
            for (int i = 0; i < vertices.Length; i++)
            {
                // Noise depends only on the vertex direction, so duplicated seam vertices move together (no cracks).
                Vector3 n = vertices[i].normalized * frequency;
                float noise = (Mathf.PerlinNoise(n.x + ox, n.y + oy) + Mathf.PerlinNoise(n.y + oy, n.z + oz) + Mathf.PerlinNoise(n.z + oz, n.x + ox)) / 3f;
                vertices[i] *= 1f + amplitude * (noise * 2f - 1f) * 2f;
            }

            var mesh = new Mesh { vertices = vertices, triangles = source.triangles, uv = source.uv };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Cone with its base at y = 0 and tip at y = 1, base radius 0.5 (like the other primitives).</summary>
        static Mesh Cone(int segments)
        {
            var vertices = new List<Vector3> { new Vector3(0f, 1f, 0f), Vector3.zero }; // tip, base centre
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f));
            }

            var triangles = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % segments;
                triangles.AddRange(new[] { 0, b, a });   // side (clockwise seen from outside)
                triangles.AddRange(new[] { 1, a, b });   // bottom (clockwise seen from below)
            }

            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ building blocks

        public static GameObject AddPrimitive(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale,
                                              Vector3 localEuler, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Attach(go, parent, localPosition, localScale, localEuler, material);
            return go;
        }

        public static GameObject AddMesh(Transform parent, string name, Mesh mesh, Vector3 localPosition, Vector3 localScale,
                                         Vector3 localEuler, Material material)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            Attach(go, parent, localPosition, localScale, localEuler, material);
            return go;
        }

        static void Attach(GameObject go, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.transform.localEulerAngles = localEuler;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.isStatic = parent.gameObject.isStatic; // environment is static (batched), zombies are not
        }

        static Transform AddModelRoot(Transform root, float scale, float yaw)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(root, false);
            model.localScale = Vector3.one * scale;
            model.localEulerAngles = new Vector3(0f, yaw, 0f);
            model.gameObject.isStatic = root.gameObject.isStatic;
            return model;
        }

        // ------------------------------------------------------------------ trees

        public const int TreeVariants = 3; // 0 = broadleaf, 1 = conifer, 2 = birch

        /// <summary>Adds a tree model under <paramref name="root"/> (root sits on the ground). Returns its scale.</summary>
        public static float AddTreeModel(Transform root, System.Random random, int variant)
        {
            float scale = Range(random, 0.85f, 1.25f);
            Transform model = AddModelRoot(root, scale, Range(random, 0f, 360f));
            Material bark = LevelBuilder.GetMaterial("Trunk", new Color(0.36f, 0.25f, 0.15f));

            switch (variant)
            {
                case 1: // conifer: straight trunk + stacked cones (lowest branches above head height)
                    AddPrimitive(model, PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.4f, 1.6f, 0.4f), Vector3.zero, bark);
                    Material pine = LevelBuilder.GetMaterial("Pine", new Color(0.1f, 0.26f, 0.15f));
                    AddMesh(model, "Cone", ConeMesh(), new Vector3(0f, 2.2f, 0f), new Vector3(3.0f, 2.4f, 3.0f), Vector3.zero, pine);
                    AddMesh(model, "Cone", ConeMesh(), new Vector3(0f, 3.5f, 0f), new Vector3(2.3f, 2.2f, 2.3f), Vector3.zero, pine);
                    AddMesh(model, "Cone", ConeMesh(), new Vector3(0f, 4.8f, 0f), new Vector3(1.5f, 1.9f, 1.5f), Vector3.zero, pine);
                    break;

                case 2: // birch: tall pale trunk, small high crown
                    Material pale = LevelBuilder.GetMaterial("TrunkLight", new Color(0.78f, 0.76f, 0.68f));
                    Material light = LevelBuilder.GetMaterial("LeavesLight", new Color(0.42f, 0.58f, 0.24f));
                    AddPrimitive(model, PrimitiveType.Cylinder, new Vector3(0f, 2.5f, 0f), new Vector3(0.32f, 2.5f, 0.32f), new Vector3(0f, 0f, Range(random, -3f, 3f)), pale);
                    AddMesh(model, "Leaves", FoliageMesh(0), new Vector3(0f, 5.2f, 0f), Vector3.one * 2.0f, Vector3.zero, light);
                    AddMesh(model, "Leaves", FoliageMesh(1), new Vector3(0.6f, 4.6f, 0.2f), Vector3.one * 1.5f, new Vector3(0f, 90f, 0f), light);
                    AddMesh(model, "Leaves", FoliageMesh(2), new Vector3(-0.5f, 4.8f, -0.3f), Vector3.one * 1.6f, new Vector3(0f, 200f, 0f), light);
                    break;

                default: // broadleaf: two tapered trunk pieces, two branches, a cluster of foliage blobs
                    Material leaves = random.NextDouble() < 0.5
                        ? LevelBuilder.GetMaterial("Leaves", new Color(0.22f, 0.42f, 0.18f))
                        : LevelBuilder.GetMaterial("LeavesDark", new Color(0.13f, 0.3f, 0.13f));
                    AddPrimitive(model, PrimitiveType.Cylinder, new Vector3(0f, 1.1f, 0f), new Vector3(0.5f, 1.1f, 0.5f), Vector3.zero, bark);
                    AddPrimitive(model, PrimitiveType.Cylinder, new Vector3(0f, 2.9f, 0f), new Vector3(0.36f, 0.8f, 0.36f), new Vector3(0f, 0f, 4f), bark);
                    AddPrimitive(model, PrimitiveType.Cylinder, new Vector3(0.45f, 3.1f, 0f), new Vector3(0.12f, 0.6f, 0.12f), new Vector3(0f, 0f, -40f), bark);
                    AddPrimitive(model, PrimitiveType.Cylinder, new Vector3(-0.4f, 3.3f, 0.2f), new Vector3(0.12f, 0.55f, 0.12f), new Vector3(20f, 0f, 35f), bark);
                    AddMesh(model, "Leaves", FoliageMesh(0), new Vector3(0f, 4.4f, 0f), Vector3.one * 3.0f, Vector3.zero, leaves);
                    AddMesh(model, "Leaves", FoliageMesh(1), new Vector3(1.0f, 4.0f, 0.4f), Vector3.one * 2.2f, new Vector3(0f, 70f, 0f), leaves);
                    AddMesh(model, "Leaves", FoliageMesh(2), new Vector3(-0.9f, 4.1f, -0.5f), Vector3.one * 2.3f, new Vector3(0f, 140f, 0f), leaves);
                    AddMesh(model, "Leaves", FoliageMesh(1), new Vector3(0.2f, 5.1f, -0.2f), Vector3.one * 2.0f, new Vector3(0f, 210f, 0f), leaves);
                    AddMesh(model, "Leaves", FoliageMesh(0), new Vector3(-0.3f, 3.9f, 0.9f), Vector3.one * 1.8f, new Vector3(0f, 300f, 0f), leaves);
                    break;
            }
            return scale;
        }

        // ------------------------------------------------------------------ bushes

        /// <summary>Dense cluster of foliage blobs filling the bush's 2.5 x 1.8 x 2.5 m gameplay volume.</summary>
        public static void AddBushModel(Transform root, System.Random random)
        {
            Transform model = AddModelRoot(root, 1f, Range(random, 0f, 360f));
            Material dark = LevelBuilder.GetMaterial("Bush", new Color(0.15f, 0.33f, 0.12f));
            Material light = LevelBuilder.GetMaterial("BushLight", new Color(0.24f, 0.42f, 0.16f));

            (Vector3 position, float size)[] blobs =
            {
                (new Vector3(0f, 0.85f, 0f), 2.1f),
                (new Vector3(0.65f, 0.7f, 0.5f), 1.4f),
                (new Vector3(-0.6f, 0.75f, -0.45f), 1.5f),
                (new Vector3(0.55f, 0.8f, -0.6f), 1.3f),
                (new Vector3(-0.5f, 0.6f, 0.6f), 1.3f),
                (new Vector3(0.1f, 1.35f, 0.1f), 1.3f),
            };
            for (int i = 0; i < blobs.Length; i++)
            {
                float jitter = Range(random, 0.9f, 1.1f);
                AddMesh(model, "Leaves", FoliageMesh(i % 3), blobs[i].position,
                        new Vector3(blobs[i].size, blobs[i].size * 0.85f, blobs[i].size) * jitter,
                        new Vector3(0f, Range(random, 0f, 360f), 0f), i % 2 == 0 ? dark : light);
            }
        }

        // ------------------------------------------------------------------ zombie body

        static readonly Color[] SkinColors =
        {
            new Color(0.47f, 0.56f, 0.42f), new Color(0.52f, 0.55f, 0.47f), new Color(0.42f, 0.5f, 0.4f), new Color(0.55f, 0.58f, 0.45f),
        };
        static readonly Color[] ShirtColors =
        {
            new Color(0.25f, 0.32f, 0.45f), new Color(0.42f, 0.3f, 0.2f), new Color(0.45f, 0.18f, 0.18f), new Color(0.35f, 0.35f, 0.33f),
        };

        /// <summary>
        /// Hunched humanoid made of capsules/spheres with glowing eyes (shows facing direction).
        /// Root pivot is 1 m above the feet (that's where the NavMeshAgent puts it). Visual only —
        /// the root keeps the same 2 m x 0.5 m capsule collider for every zombie.
        /// </summary>
        public static void AddZombieModel(Transform root, int index)
        {
            int v = index % 4;
            float scale = new[] { 1f, 1.06f, 0.95f, 1.02f }[v];
            float hunch = 12f + 6f * (index % 3);

            Transform model = AddModelRoot(root, scale, 0f);
            model.localPosition = new Vector3(0f, -1f, 0f); // model pivot = feet

            Material skin = LevelBuilder.GetMaterial($"ZombieSkin{v}", SkinColors[v]);
            Material shirt = LevelBuilder.GetMaterial($"ZombieShirt{v}", ShirtColors[v]);
            Material pants = LevelBuilder.GetMaterial("ZombiePants", new Color(0.2f, 0.2f, 0.24f));
            Material eyes = LevelBuilder.GetMaterial("ZombieEyes", new Color(1f, 0.15f, 0.05f), emission: 3f);

            AddPrimitive(model, PrimitiveType.Capsule, new Vector3(-0.15f, 0.45f, 0f), new Vector3(0.24f, 0.45f, 0.24f), new Vector3(-6f, 0f, 0f), pants);
            AddPrimitive(model, PrimitiveType.Capsule, new Vector3(0.15f, 0.45f, 0.04f), new Vector3(0.24f, 0.45f, 0.24f), new Vector3(8f, 0f, 0f), pants);
            AddPrimitive(model, PrimitiveType.Capsule, new Vector3(0f, 1.22f, 0.06f), new Vector3(0.55f, 0.42f, 0.36f), new Vector3(hunch, 0f, 0f), shirt);

            float headZ = 0.2f + hunch * 0.008f;
            AddPrimitive(model, PrimitiveType.Sphere, new Vector3(0f, 1.66f, headZ), Vector3.one * 0.36f, new Vector3(0f, 0f, 8f), skin);
            AddPrimitive(model, PrimitiveType.Sphere, new Vector3(-0.075f, 1.7f, headZ + 0.15f), Vector3.one * 0.07f, Vector3.zero, eyes);
            AddPrimitive(model, PrimitiveType.Sphere, new Vector3(0.075f, 1.7f, headZ + 0.15f), Vector3.one * 0.07f, Vector3.zero, eyes);

            // Arms reaching forward, one a little lower than the other.
            AddPrimitive(model, PrimitiveType.Capsule, new Vector3(-0.34f, 1.38f, 0.32f), new Vector3(0.15f, 0.36f, 0.15f), new Vector3(70f, 0f, 0f), skin);
            AddPrimitive(model, PrimitiveType.Capsule, new Vector3(0.34f, 1.34f, 0.3f), new Vector3(0.15f, 0.36f, 0.15f), new Vector3(82f, 0f, 0f), skin);
        }
    }
}
