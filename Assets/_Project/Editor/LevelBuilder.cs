using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombieStealth.AI.Perception;
using ZombieStealth.AI.Zombie;
using ZombieStealth.Audio;
using ZombieStealth.Debugging;
using ZombieStealth.Gameplay;
using ZombieStealth.Player;
using Object = UnityEngine.Object;

namespace ZombieStealth.Editor
{
    /// <summary>Everything a level builder needs to describe one level.</summary>
    public class LevelDefinition
    {
        public string Name;
        public string ScenePath;
        public Vector2 Size;                       // x = width (west-east), y = length (south-north), metres
        public Vector3 PlayerSpawn;
        public Vector3 ExtractionPosition;
        /// <summary>One entry per zombie: where it spawns and the circle it wanders in.</summary>
        public (Vector3 spawn, Vector3 zoneCentre, float zoneRadius)[] Zombies = Array.Empty<(Vector3, Vector3, float)>();
        public Vector3[] RockPickups = Array.Empty<Vector3>();
        /// <summary>Optional coloured ground areas (x/y of the Rect = world x/z) with their footstep surface. Empty = one plain grass plane.</summary>
        public (Rect area, string material, Color color, SurfaceType surface)[] GroundPatches = Array.Empty<(Rect, string, Color, SurfaceType)>();
        public Action<Transform> PlaceCover;       // rocks, trees, bushes, props (parented under "Environment")
        public (Vector3 from, Vector3 to)[] ExtraReachabilityChecks = Array.Empty<(Vector3, Vector3)>();
        public bool FirstInBuildSettings;
    }

    /// <summary>
    /// Shared scene-building code used by TestArenaBuilder and ZombieDemoBuilder.
    /// Builds a whole level from a LevelDefinition: scene, lighting, ground, walls, cover,
    /// player, zombies, pickups, extraction, NavMesh bake and reachability validation.
    ///
    /// Everything walkable/blocking lives under "Environment", which has the NavMeshSurface.
    /// The player, zombies, pickups and extraction are outside it, so they are never baked.
    /// Colliders stay simple (trunk capsule, rock mesh, bush box); detailed visuals come from EnvironmentArt.
    /// </summary>
    public static class LevelBuilder
    {
        const string MaterialFolder = "Assets/_Project/Materials";
        const string RockPrefabPath = "Assets/_Project/Prefabs/Rock.prefab";

        // Layers used by the game (created in the project's TagManager if missing).
        public const string ObstacleLayer = "Obstacle";   // blocks zombie vision
        public const string PlayerLayer = "Player";
        public const string ZombieLayer = "Zombie";
        public const string ThrowableLayer = "Throwable";

        static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

        public static void Build(LevelDefinition level)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            materialCache.Clear();
            EnvironmentArt.ResetCache();
            EnsureLayers(ObstacleLayer, PlayerLayer, ZombieLayer, ThrowableLayer);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetUpLighting();

            var environment = new GameObject("Environment").transform;
            CreateGround(environment, level);
            CreateBoundaryWalls(environment, level.Size);
            level.PlaceCover(environment);

            CreatePlayer(level.PlayerSpawn);
            foreach (Vector3 pickup in level.RockPickups)
                CreateRockPickup(pickup);
            CreateExtractionZone(level.ExtractionPosition);

            new GameObject("GameManager").AddComponent<GameManager>();
            new GameObject("NoiseDebugView").AddComponent<NoiseDebugView>();
            new GameObject("DebugHud").AddComponent<DebugHud>();
            CreateGameAudio();

            for (int i = 0; i < level.Zombies.Length; i++)
            {
                var (spawn, zoneCentre, zoneRadius) = level.Zombies[i];
                string number = (i + 1).ToString();
                var zone = CreateWanderZone("WanderZone " + number, zoneCentre, zoneRadius);
                CreateZombie("Zombie " + number, i, spawn, zone, avoidancePriority: 40 + i * 5);
            }

            EditorSceneManager.SaveScene(scene, level.ScenePath);
            BakeNavMesh(environment.gameObject, level.ScenePath.Replace(".unity", "_NavMesh.asset"));
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings(level.ScenePath, level.FirstInBuildSettings);
            AssetDatabase.SaveAssets();

            ValidateNavMesh(level);
        }

        static void SetUpLighting()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.94f, 0.84f);              // warm late-afternoon sun
            light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            // Soft three-colour ambient + light fog: reads as "outdoors" but stays bright enough for debugging.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.63f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.63f, 0.68f, 0.72f);
        }

        // ------------------------------------------------------------------ ground & walls

        static void CreateGround(Transform parent, LevelDefinition level)
        {
            if (level.GroundPatches.Length == 0)
            {
                CreateGroundPatch(parent, new Rect(-level.Size.x / 2f, -level.Size.y / 2f, level.Size.x, level.Size.y),
                                  "Ground", new Color(0.33f, 0.42f, 0.25f), SurfaceType.Grass);
                return;
            }
            foreach (var (area, material, color, surface) in level.GroundPatches)
                CreateGroundPatch(parent, area, material, color, surface);
        }

        /// <summary>Flat walkable ground rectangle at y = 0 (patches must tile without overlapping).</summary>
        static void CreateGroundPatch(Transform parent, Rect area, string material, Color color, SurfaceType surface)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground " + material;
            ground.transform.SetParent(parent);
            ground.transform.position = new Vector3(area.center.x, 0f, area.center.y);
            ground.transform.localScale = new Vector3(area.width / 10f, 1f, area.height / 10f); // a Plane is 10 x 10 m
            ground.isStatic = true;
            ground.GetComponent<Renderer>().sharedMaterial = GetMaterial(material, color);
            ground.AddComponent<FootstepSurface>().surface = surface;
        }

        static void CreateBoundaryWalls(Transform parent, Vector2 size)
        {
            float halfX = size.x / 2f, halfZ = size.y / 2f;
            CreateWall(parent, new Vector3(0f, 1f, halfZ), new Vector3(size.x, 2f, 0.5f));
            CreateWall(parent, new Vector3(0f, 1f, -halfZ), new Vector3(size.x, 2f, 0.5f));
            CreateWall(parent, new Vector3(halfX, 1f, 0f), new Vector3(0.5f, 2f, size.y));
            CreateWall(parent, new Vector3(-halfX, 1f, 0f), new Vector3(0.5f, 2f, size.y));
        }

        public static void CreateWall(Transform parent, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.GetComponent<Renderer>().sharedMaterial = GetMaterial("Wall", new Color(0.35f, 0.3f, 0.25f));
            wall.transform.localScale = scale;
            MakeObstacle(wall, parent, position);
        }

        // ------------------------------------------------------------------ cover (gameplay obstacles)

        /// <summary>
        /// Shared setup for every solid obstacle: Obstacle layer (blocks zombie vision), static, and
        /// "Not Walkable" so it cuts a hole in the NavMesh and no NavMesh islands are baked on top of it.
        /// </summary>
        static void MakeObstacle(GameObject go, Transform parent, Vector3 position, bool notWalkable = true)
        {
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer(ObstacleLayer);
            go.isStatic = true;

            var modifier = go.AddComponent<NavMeshModifier>();
            if (notWalkable)
            {
                modifier.overrideArea = true;
                modifier.area = NavMesh.GetAreaFromName("Not Walkable");
            }
        }

        /// <summary>
        /// Boulder with a lumpy generated mesh (also used as its collider). <paramref name="size"/> is its
        /// approximate full size; about 90% of the height sticks out of the ground.
        /// Tall rocks (≈2 m+) hide a standing player, low rocks (≈1.4 m) only a crouching one.
        /// </summary>
        public static void CreateRock(Transform parent, Vector3 position, Vector3 size)
        {
            System.Random random = EnvironmentArt.RandomFor(position);
            Mesh mesh = EnvironmentArt.RockMesh(random.Next(3));

            var rock = new GameObject("Rock");
            rock.AddComponent<MeshFilter>().sharedMesh = mesh;
            rock.AddComponent<MeshRenderer>().sharedMaterial = random.Next(3) switch
            {
                0 => GetMaterial("RockDark", new Color(0.36f, 0.36f, 0.37f)),
                1 => GetMaterial("RockMoss", new Color(0.4f, 0.45f, 0.35f)),
                _ => GetMaterial("Rock", new Color(0.5f, 0.5f, 0.5f)),
            };
            rock.AddComponent<MeshCollider>().sharedMesh = mesh;
            rock.transform.localScale = size;
            rock.transform.rotation = Quaternion.Euler(0f, EnvironmentArt.Range(random, 0f, 360f), 0f);
            MakeObstacle(rock, parent, position + Vector3.up * size.y * 0.4f);
        }

        /// <summary>Tree: trunk capsule collider on the root, detailed visual model as children. variant -1 = pick from position.</summary>
        public static void CreateTree(Transform parent, Vector3 position, int variant = -1)
        {
            System.Random random = EnvironmentArt.RandomFor(position);
            if (variant < 0)
                variant = random.Next(EnvironmentArt.TreeVariants);

            var tree = new GameObject("Tree");
            MakeObstacle(tree, parent, position);
            float scale = EnvironmentArt.AddTreeModel(tree.transform, random, variant);

            // Only the trunk collides (blocks movement and vision). Foliage has no collider,
            // so it never cuts holes in the NavMesh.
            var trunk = tree.AddComponent<CapsuleCollider>();
            trunk.radius = 0.25f * scale;
            trunk.height = 4f * scale;
            trunk.center = new Vector3(0f, 2f * scale, 0f);
        }

        public static void CreateBush(Transform parent, Vector3 position)
        {
            // Tall hedge clump: walk-through (trigger) but on the Obstacle layer so it blocks zombie vision.
            var bush = new GameObject("Bush");
            var volume = bush.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(2.5f, 1.8f, 2.5f);
            volume.center = new Vector3(0f, 0.9f, 0f);
            MakeObstacle(bush, parent, position, notWalkable: false);

            // Zombies walk through bushes too, so keep the bush out of the NavMesh bake entirely.
            bush.GetComponent<NavMeshModifier>().ignoreFromBuild = true;
            EnvironmentArt.AddBushModel(bush.transform, EnvironmentArt.RandomFor(position));
        }

        /// <summary>
        /// Gentle walkable hill: a flattened sphere sunk into the ground so only its top sticks out.
        /// Edge slope ≈ atan(3.3 · height / radius) — keep radius ≥ 6 × height so it stays below ~30°.
        /// On the Obstacle layer, so a hill really does hide a crouching player behind its crest.
        /// </summary>
        public static void CreateMound(Transform parent, Vector3 centre, float radiusX, float radiusZ, float height, string material, Color color,
                                       SurfaceType surface)
        {
            var mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mound.name = "Mound";
            Object.DestroyImmediate(mound.GetComponent<SphereCollider>());
            mound.AddComponent<MeshCollider>();
            mound.GetComponent<Renderer>().sharedMaterial = GetMaterial(material, color);
            // Ellipsoid with vertical half-axis 2.6h, centre 1.6h below ground → sticks out exactly h.
            mound.transform.localScale = new Vector3(radiusX * 2f, height * 5.2f, radiusZ * 2f);
            MakeObstacle(mound, parent, new Vector3(centre.x, -height * 1.6f, centre.z), notWalkable: false);
            mound.AddComponent<FootstepSurface>().surface = surface;
        }

        /// <summary>Generic solid box prop (crates, fences, tents' collision...). Visuals are added by the caller.</summary>
        public static GameObject CreateSolidProp(Transform parent, string name, Vector3 position, float yaw, Vector3 colliderSize)
        {
            var prop = new GameObject(name);
            var box = prop.AddComponent<BoxCollider>();
            box.size = colliderSize;
            box.center = new Vector3(0f, colliderSize.y / 2f, 0f);
            prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            MakeObstacle(prop, parent, position);
            return prop;
        }

        // ------------------------------------------------------------------ decoration (no colliders, never affects NavMesh)

        public static void CreatePebble(Transform parent, Vector3 position, float size)
        {
            System.Random random = EnvironmentArt.RandomFor(position);
            var holder = new GameObject("Pebble") { isStatic = true };
            holder.transform.SetParent(parent);
            holder.transform.position = position;
            EnvironmentArt.AddMesh(holder.transform, "Stone", EnvironmentArt.RockMesh(random.Next(3)), Vector3.up * size * 0.25f,
                                   new Vector3(size, size * EnvironmentArt.Range(random, 0.5f, 0.9f), size * EnvironmentArt.Range(random, 0.8f, 1.2f)),
                                   new Vector3(0f, EnvironmentArt.Range(random, 0f, 360f), 0f), GetMaterial("RockDark", new Color(0.36f, 0.36f, 0.37f)));
        }

        public static void CreateStump(Transform parent, Vector3 position)
        {
            var holder = new GameObject("Stump") { isStatic = true };
            holder.transform.SetParent(parent);
            holder.transform.position = position;
            EnvironmentArt.AddPrimitive(holder.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.2f, 0.55f),
                                        Vector3.zero, GetMaterial("Trunk", new Color(0.36f, 0.25f, 0.15f)));
        }

        /// <summary>
        /// Dirt trail along a polyline, slightly above the ground. Its pieces are thin triggers tagged as Dirt
        /// so footsteps sound different on the trail; they're kept out of the NavMesh bake.
        /// </summary>
        public static void CreatePath(Transform parent, Vector3[] points, float width)
        {
            Material dirt = GetMaterial("Dirt", new Color(0.42f, 0.35f, 0.25f));
            var holder = new GameObject("Path") { isStatic = true };
            holder.transform.SetParent(parent);
            holder.AddComponent<FootstepSurface>().surface = SurfaceType.Dirt;
            holder.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // applies to all child pieces

            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = points[i] + Vector3.up * 0.012f;
                var joint = EnvironmentArt.AddPrimitive(holder.transform, PrimitiveType.Cylinder, p, new Vector3(width, 0.005f, width), Vector3.zero, dirt);
                joint.AddComponent<BoxCollider>().isTrigger = true;
                if (i == points.Length - 1)
                    continue;

                Vector3 next = points[i + 1] + Vector3.up * 0.012f;
                Vector3 direction = next - p;
                var segment = EnvironmentArt.AddPrimitive(holder.transform, PrimitiveType.Cube, (p + next) / 2f,
                                                          new Vector3(width, 0.01f, direction.magnitude), Vector3.zero, dirt);
                segment.transform.rotation = Quaternion.LookRotation(direction);
                segment.AddComponent<BoxCollider>().isTrigger = true;
            }
        }

        // ------------------------------------------------------------------ actors & gameplay objects

        static void CreatePlayer(Vector3 position)
        {
            var player = PlayerTestSceneBuilder.CreatePlayer(position);
            player.layer = LayerMask.NameToLayer(PlayerLayer);
            var thrower = player.AddComponent<PlayerThrower>();
            SetField(thrower, "rockPrefab", GetOrCreateRockPrefab());

            // Own footsteps: 2D (they're "at your feet"), synced to PlayerNoise's steps.
            var source = player.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var footsteps = player.AddComponent<PlayerFootstepAudio>();
            AssignStepClips(footsteps);
        }

        /// <summary>The thrown rock prefab is created once; edit Prefabs/Rock.prefab to tune it (rebuilds keep your changes).</summary>
        static Throwable GetOrCreateRockPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefabPath);
            if (existing != null)
                return existing.GetComponent<Throwable>();

            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.layer = LayerMask.NameToLayer(ThrowableLayer);
            rock.transform.localScale = Vector3.one * 0.25f;
            rock.GetComponent<Renderer>().sharedMaterial = GetMaterial("ThrownRock", new Color(0.6f, 0.58f, 0.55f));

            var body = rock.AddComponent<Rigidbody>();
            body.mass = 0.5f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // small and fast: don't tunnel through things
            rock.AddComponent<Throwable>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(rock, RockPrefabPath);
            Object.DestroyImmediate(rock);
            return prefab.GetComponent<Throwable>();
        }

        static void CreateRockPickup(Vector3 position)
        {
            var pickup = new GameObject("RockPickup");
            pickup.transform.position = position;
            var trigger = pickup.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.7f; // easy to walk into
            trigger.center = new Vector3(0f, 0.4f, 0f);
            pickup.AddComponent<RockPickup>();

            // Small glowing pile of stones so it's easy to spot.
            Material glow = GetMaterial("Pickup", new Color(1f, 0.8f, 0.2f), emission: 0.8f);
            EnvironmentArt.AddMesh(pickup.transform, "Stone", EnvironmentArt.RockMesh(0), new Vector3(0f, 0.15f, 0f), new Vector3(0.4f, 0.3f, 0.35f), Vector3.zero, glow);
            EnvironmentArt.AddMesh(pickup.transform, "Stone", EnvironmentArt.RockMesh(1), new Vector3(0.2f, 0.1f, 0.15f), new Vector3(0.25f, 0.2f, 0.25f), new Vector3(0f, 60f, 0f), glow);
            EnvironmentArt.AddMesh(pickup.transform, "Stone", EnvironmentArt.RockMesh(2), new Vector3(-0.15f, 0.1f, 0.2f), new Vector3(0.22f, 0.18f, 0.22f), new Vector3(0f, 130f, 0f), glow);
        }

        static void CreateExtractionZone(Vector3 position)
        {
            var zone = new GameObject("ExtractionZone");
            zone.transform.position = position;
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4f, 3f, 4f);
            trigger.center = new Vector3(0f, 1.5f, 0f);
            zone.AddComponent<ExtractionZone>();

            // Visuals only (no colliders): landing pad, glowing inner circle (= the trigger area),
            // ring of markers, four lamp posts and a tall beacon visible from far away.
            Color green = new Color(0.2f, 0.95f, 0.4f);
            Material glow = GetMaterial("ExtractionGlow", green, emission: 2f);
            Material pad = GetMaterial("Concrete", new Color(0.33f, 0.33f, 0.32f));
            Material metal = GetMaterial("Metal", new Color(0.25f, 0.26f, 0.28f));
            Material stripes = GetMaterial("Barrier", new Color(0.85f, 0.65f, 0.1f));

            EnvironmentArt.AddPrimitive(zone.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(7f, 0.02f, 7f), Vector3.zero, pad);
            EnvironmentArt.AddPrimitive(zone.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0f), new Vector3(4f, 0.02f, 4f), Vector3.zero, glow);

            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 3.1f;
                EnvironmentArt.AddPrimitive(zone.transform, PrimitiveType.Cube, offset + Vector3.up * 0.12f, new Vector3(0.18f, 0.24f, 0.18f), Vector3.zero, glow);
            }

            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, 45f + i * 90f, 0f) * Vector3.forward * 4.3f;
                EnvironmentArt.AddPrimitive(zone.transform, PrimitiveType.Cylinder, offset + Vector3.up * 1.2f, new Vector3(0.12f, 1.2f, 0.12f), Vector3.zero, metal);
                EnvironmentArt.AddPrimitive(zone.transform, PrimitiveType.Sphere, offset + Vector3.up * 2.5f, Vector3.one * 0.35f, Vector3.zero, glow);
                AddPointLight(zone.transform, offset + Vector3.up * 2.5f, green, range: 7f, intensity: 1.5f);
            }

            // Striped barriers behind the pad (north side).
            EnvironmentArt.AddPrimitive(zone.transform, PrimitiveType.Cube, new Vector3(-2.5f, 0.45f, 4.6f), new Vector3(2.2f, 0.9f, 0.25f), new Vector3(0f, 15f, 0f), stripes);
            EnvironmentArt.AddPrimitive(zone.transform, PrimitiveType.Cube, new Vector3(2.5f, 0.45f, 4.6f), new Vector3(2.2f, 0.9f, 0.25f), new Vector3(0f, -15f, 0f), stripes);

            EnvironmentArt.AddPrimitive(zone.transform, PrimitiveType.Cylinder, new Vector3(0f, 4f, 0f), new Vector3(0.3f, 4f, 0.3f), Vector3.zero, glow);
            EnvironmentArt.AddPrimitive(zone.transform, PrimitiveType.Sphere, new Vector3(0f, 8.3f, 0f), Vector3.one * 0.9f, Vector3.zero, glow);
            AddPointLight(zone.transform, new Vector3(0f, 8f, 0f), green, range: 16f, intensity: 3f);
        }

        static void AddPointLight(Transform parent, Vector3 localPosition, Color color, float range, float intensity)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.transform.SetParent(parent, false);
            light.transform.localPosition = localPosition;
        }

        static WanderZone CreateWanderZone(string name, Vector3 centre, float radius)
        {
            var zone = new GameObject(name).AddComponent<WanderZone>();
            zone.transform.position = centre;
            SetField(zone, "radius", radius);
            return zone;
        }

        /// <summary>
        /// Every zombie is the same set of components — same Behavior Tree, its own memory/perception.
        /// The collider/agent size is identical for all; only the visual model varies a little.
        /// Slightly different avoidance priorities let agents settle who steps aside when they meet.
        /// </summary>
        static void CreateZombie(string name, int index, Vector3 position, WanderZone zone, int avoidancePriority)
        {
            var zombie = new GameObject(name);
            zombie.layer = LayerMask.NameToLayer(ZombieLayer);
            zombie.transform.position = position + Vector3.up;   // pivot 1 m above the feet
            var body = zombie.AddComponent<CapsuleCollider>();   // gameplay body: 2 m tall, 0.5 m radius
            body.height = 2f;
            body.radius = 0.5f;
            EnvironmentArt.AddZombieModel(zombie.transform, index);

            var agent = zombie.AddComponent<NavMeshAgent>();
            agent.baseOffset = 1f;
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.speed = 1.6f;           // slow shamble while wandering
            agent.angularSpeed = 240f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.2f;
            agent.avoidancePriority = avoidancePriority;

            zombie.AddComponent<ZombieMotor>();
            var brain = zombie.AddComponent<ZombieBrain>();
            SetField(brain, "wanderZone", zone);
            zombie.AddComponent<ZombieVision>();
            zombie.AddComponent<ZombieHearing>();
            zombie.AddComponent<ZombieDebugView>();
            AddZombieAudio(zombie);
        }

        // ------------------------------------------------------------------ audio (feedback only)

        /// <summary>3D AudioSource + ZombieAudio, with placeholder clips assigned (swap them in the Inspector).</summary>
        static void AddZombieAudio(GameObject zombie)
        {
            var source = zombie.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;                         // fully 3D: you hear where the zombie is
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 3f;
            source.maxDistance = 35f;
            source.dopplerLevel = 0f;
            source.volume = 0.9f;

            var audio = zombie.AddComponent<ZombieAudio>();
            SetField(audio, "spotted", PlaceholderAudio.Get("Spotted"));
            SetField(audio, "chase", PlaceholderAudio.Get("Chase"));
            SetField(audio, "investigate", PlaceholderAudio.Get("Investigate"));
            SetField(audio, "search", PlaceholderAudio.Get("Search"));
            SetArray(audio, "wanderGroans", PlaceholderAudio.Get("Groan1"), PlaceholderAudio.Get("Groan2"));

            // Footsteps on a child with their own short-range 3D source (separate from the alert cues).
            var feet = new GameObject("Footsteps");
            feet.transform.SetParent(zombie.transform, false);
            feet.transform.localPosition = Vector3.down; // at the feet
            var stepSource = feet.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.spatialBlend = 1f;
            stepSource.rolloffMode = AudioRolloffMode.Linear; // clean cut-off: far zombies are silent
            stepSource.minDistance = 2f;
            stepSource.maxDistance = 14f;
            stepSource.dopplerLevel = 0f;
            AssignStepClips(feet.AddComponent<ZombieFootstepAudio>());
        }

        /// <summary>Same per-surface step sounds for the player and zombies (zombies play them lower/heavier).</summary>
        static void AssignStepClips(Object footstepComponent)
        {
            SetArray(footstepComponent, "dirtSteps", PlaceholderAudio.Get("Footstep1"), PlaceholderAudio.Get("Footstep2"), PlaceholderAudio.Get("Footstep3"));
            SetArray(footstepComponent, "grassSteps", PlaceholderAudio.Get("StepGrass1"), PlaceholderAudio.Get("StepGrass2"), PlaceholderAudio.Get("StepGrass3"));
            SetArray(footstepComponent, "forestSteps", PlaceholderAudio.Get("StepForest1"), PlaceholderAudio.Get("StepForest2"), PlaceholderAudio.Get("StepForest3"));
            SetArray(footstepComponent, "gravelSteps", PlaceholderAudio.Get("StepGravel1"), PlaceholderAudio.Get("StepGravel2"), PlaceholderAudio.Get("StepGravel3"));
        }

        /// <summary>2D game-state sounds: caught, extracted, shared chase loop.</summary>
        static void CreateGameAudio()
        {
            var go = new GameObject("GameAudio");
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;                         // 2D: same volume everywhere
            var audio = go.AddComponent<GameAudio>();
            SetField(audio, "caught", PlaceholderAudio.Get("Caught"));
            SetField(audio, "extracted", PlaceholderAudio.Get("Extracted"));
            SetField(audio, "chaseLoop", PlaceholderAudio.Get("ChaseLoop"));
        }

        // ------------------------------------------------------------------ NavMesh

        static void BakeNavMesh(GameObject environment, string navMeshAssetPath)
        {
            var surface = environment.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            // Save the baked data as an asset so the scene keeps it (same thing the Bake button does).
            AssetDatabase.DeleteAsset(navMeshAssetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navMeshAssetPath);
            EditorUtility.SetDirty(surface);
        }

        /// <summary>
        /// 1. Key paths must exist (spawn → extraction, each zombie → its zone, plus level-specific checks).
        /// 2. Each zombie must reach points spread around its own wander zone.
        /// 3. Every walkable ground point (sampled on a 3 m grid) must connect to the extraction,
        ///    otherwise there's a sealed-off pocket where an agent could get trapped.
        /// </summary>
        static void ValidateNavMesh(LevelDefinition level)
        {
            bool ok = CheckPath(level.Name, level.PlayerSpawn, level.ExtractionPosition);
            foreach (var (spawn, zoneCentre, _) in level.Zombies)
                ok &= CheckPath(level.Name, spawn, zoneCentre);
            foreach (var (from, to) in level.ExtraReachabilityChecks)
                ok &= CheckPath(level.Name, from, to);

            var path = new NavMeshPath();
            for (int i = 0; i < level.Zombies.Length; i++)
            {
                var (spawn, zoneCentre, zoneRadius) = level.Zombies[i];
                NavMesh.SamplePosition(spawn, out NavMeshHit start, 1f, NavMesh.AllAreas);
                int sampled = 0, reachable = 0;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 point = zoneCentre + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * (zoneRadius * 0.7f);
                    if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                        continue; // obstacle there, fine
                    sampled++;
                    if (NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                        reachable++;
                }
                if (reachable == sampled)
                    Debug.Log($"{level.Name}: Zombie {i + 1} reaches {reachable}/{sampled} sample points around its wander zone.");
                else
                {
                    ok = false;
                    Debug.LogError($"{level.Name}: Zombie {i + 1} reaches only {reachable}/{sampled} sample points around its wander zone.");
                }
            }

            NavMesh.SamplePosition(level.ExtractionPosition, out NavMeshHit goal, 1f, NavMesh.AllAreas);
            var pockets = new List<Vector3>();
            int groundPoints = 0;
            for (float x = -level.Size.x / 2f + 1.5f; x < level.Size.x / 2f; x += 3f)
            {
                for (float z = -level.Size.y / 2f + 1.5f; z < level.Size.y / 2f; z += 3f)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, 0f, z), out NavMeshHit hit, 0.4f, NavMesh.AllAreas))
                        continue; // obstacle here, not walkable ground
                    groundPoints++;
                    if (!NavMesh.CalculatePath(hit.position, goal.position, NavMesh.AllAreas, path)
                        || path.status != NavMeshPathStatus.PathComplete)
                        pockets.Add(hit.position);
                }
            }

            if (pockets.Count == 0)
                Debug.Log($"{level.Name}: NavMesh grid check OK — all {groundPoints} sampled ground points connect to the extraction (no trapped pockets).");
            else
            {
                ok = false;
                Debug.LogError($"{level.Name}: NavMesh grid check found {pockets.Count}/{groundPoints} points that can't reach the extraction, e.g. {pockets[0]}.");
            }

            if (ok)
                Debug.Log($"{level.Name}: built and validated → {level.ScenePath}");
        }

        static bool CheckPath(string levelName, Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            bool found = NavMesh.SamplePosition(from, out var start, 1f, NavMesh.AllAreas)
                         && NavMesh.SamplePosition(to, out var end, 1f, NavMesh.AllAreas)
                         && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path);

            if (found && path.status == NavMeshPathStatus.PathComplete)
            {
                Debug.Log($"{levelName}: NavMesh path {from} → {to} OK ({path.corners.Length} corners).");
                return true;
            }
            Debug.LogError($"{levelName}: NavMesh path {from} → {to} FAILED (status: {path.status}).");
            return false;
        }

        // ------------------------------------------------------------------ utilities

        /// <summary>Sets a private [SerializeField] on a component, like dragging it in the Inspector.</summary>
        static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (value is Object reference)
                property.objectReferenceValue = reference;
            else
                property.floatValue = (float)value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The GameManager reloads the scene on restart, which only works for scenes in Build Settings.</summary>
        static void AddToBuildSettings(string path, bool first)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(first ? 0 : scenes.Count, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Gets (or creates) a URP Lit material asset and (re)applies its colour every build, so the
        /// builder stays the single source of truth. Low smoothness = matte, natural look.
        /// </summary>
        public static Material GetMaterial(string name, Color color, float emission = 0f, float smoothness = 0.12f)
        {
            if (materialCache.TryGetValue(name, out Material cached))
                return cached;

            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(material);

            materialCache[name] = material;
            return material;
        }

        /// <summary>Adds any missing layers to the first free user slots (8-31) of the TagManager.</summary>
        static void EnsureLayers(params string[] names)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            foreach (string name in names)
            {
                if (LayerMask.NameToLayer(name) != -1)
                    continue;

                for (int i = 8; i < layers.arraySize; i++)
                {
                    var slot = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.stringValue))
                    {
                        slot.stringValue = name;
                        tagManager.ApplyModifiedPropertiesWithoutUndo();
                        break;
                    }
                }
            }
        }
    }
}
