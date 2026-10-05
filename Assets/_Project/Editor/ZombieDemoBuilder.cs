using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZombieStealth.Gameplay;

namespace ZombieStealth.Editor
{
    /// <summary>
    /// The final demo level: 100 x 120 m of mixed terrain. Spawn in the south, extraction in the north.
    ///
    ///   WEST   – forest: dense mixed trees, bushes, logs, clearings. Safest, longest, winding trail.
    ///            Zombie 6 patrols the middle of it, next to the west wall, so hugging the wall isn't a free pass.
    ///   MIDDLE – rocky: boulder clusters, a rocky ridge, a raised mound. Most direct, highest pressure
    ///            (Zombie 1 in the south half, Zombie 2 in the centre).
    ///   EAST   – meadow: open grass, gentle hills, scattered trees/bushes. Fastest, most exposed
    ///            (Zombie 3 in the middle, Zombie 5 on the northern stretch).
    ///   NORTH  – final zone: abandoned campsite (tents, crates, broken fences), mixed trees and rocks,
    ///            guarding the extraction pad (Zombie 4 patrols just south-east of the pad).
    ///
    /// Layout = hand-placed key features + seeded scatter. A small spacing system (Layout) rejects any
    /// solid obstacle closer than 2.5 m to another one, and keeps trails, spawn, extraction, pickups and
    /// zombie spots clear — so the NavMesh never gets gaps narrower than the agent. Scatter areas stay
    /// 3.5 m away from the boundary walls for the same reason. Same seed → same scene.
    /// Menu: Tools → Zombie Stealth → Build Full Demo Scene
    /// </summary>
    public static class ZombieDemoBuilder
    {
        // Coordinates: x = west(-) / east(+), z = south(-) / north(+). The map spans x ±50, z ±60.
        static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, -55f);
        static readonly Vector3 Extraction = new Vector3(0f, 0f, 54f);

        // One point on each route, used for reachability checks.
        static readonly Vector3 ForestPoint = new Vector3(-31f, 0f, -14f);
        static readonly Vector3 CentralPoint = new Vector3(-5f, 0f, -6f);
        static readonly Vector3 MeadowPoint = new Vector3(36f, 0f, -6f);
        static readonly Vector3 NorthPoint = new Vector3(-20f, 0f, 38f);

        // Zombies: (spawn, wander-zone centre, radius). Zones are ≥ 7 m apart, so they never merge.
        static readonly (Vector3 spawn, Vector3 centre, float radius)[] Zombies =
        {
            (new Vector3(-6f, 0f, -24f), new Vector3(-8f, 0f, -24f), 10f),  // 1: south-middle, touches the forest's inner edge
            (new Vector3(4f, 0f, 4f),    new Vector3(4f, 0f, 2f),    11f),  // 2: centre of the rocky route
            (new Vector3(35f, 0f, -10f), new Vector3(35f, 0f, -8f),  10f),  // 3: meadow
            (new Vector3(10f, 0f, 38f),  new Vector3(9f, 0f, 38f),    8f),  // 4: north, south-east of the extraction
            (new Vector3(33f, 0f, 20f),  new Vector3(32f, 0f, 22f),   8f),  // 5: north meadow, second stage of the east route
            (new Vector3(-42f, 0f, -2f), new Vector3(-41f, 0f, -4f),  8f),  // 6: west forest, covers the open strip along the west wall
        };

        // Trails (also kept free of obstacles). Each starts at the spawn and ends at the extraction.
        static readonly Vector3[] ForestTrail = Points((-4, -50), (-16, -48), (-30, -42), (-36, -28), (-31, -14), (-38, 0), (-33, 14), (-28, 26), (-20, 38), (-10, 48), (-3, 52));
        static readonly Vector3[] CentralTrail = Points((0, -50), (3, -40), (-3, -30), (1, -18), (-5, -6), (2, 6), (-2, 18), (3, 28), (-1, 40), (0, 50));
        static readonly Vector3[] MeadowTrail = Points((4, -50), (18, -47), (32, -38), (39, -22), (36, -6), (40, 10), (35, 24), (24, 38), (12, 47), (3, 52));

        static readonly Vector3[] RockPickups =
        {
            new Vector3(5f, 0f, -52f),     // spawn
            new Vector3(-33f, 0f, -35f),   // forest south
            new Vector3(-34f, 0f, 8f),     // forest north
            new Vector3(3f, 0f, -42f),     // middle, before Zombie 1
            new Vector3(-4f, 0f, -10f),    // middle, between Zombie 1 and Zombie 2
            new Vector3(-3f, 0f, 20f),     // middle, after Zombie 2
            new Vector3(30f, 0f, -34f),    // meadow, before Zombie 3
            new Vector3(41f, 0f, 14f),     // meadow, after Zombie 3 / before Zombie 5
            new Vector3(-16f, 0f, 34f),    // before the northern final zone
        };

        static readonly Color ForestGround = new Color(0.2f, 0.26f, 0.15f);
        static readonly Color RockyGround = new Color(0.42f, 0.4f, 0.33f);
        static readonly Color MeadowGround = new Color(0.45f, 0.57f, 0.27f);
        static readonly Color NorthGround = new Color(0.33f, 0.35f, 0.24f);
        static readonly Color SpawnGround = new Color(0.36f, 0.42f, 0.27f);

        [MenuItem("Tools/Zombie Stealth/Build Full Demo Scene")]
        public static void Build()
        {
            LevelBuilder.Build(new LevelDefinition
            {
                Name = "Demo scene",
                ScenePath = "Assets/_Project/Scenes/ZombieStealth.unity",
                Size = new Vector2(100f, 120f),
                PlayerSpawn = PlayerSpawn,
                ExtractionPosition = Extraction,
                Zombies = Zombies,
                RockPickups = RockPickups,
                GroundPatches = new[]
                {
                    (new Rect(-50f, -60f, 100f, 14f), "Ground_Spawn", SpawnGround, SurfaceType.Grass),    // z -60..-46
                    (new Rect(-50f, -46f, 32f, 74f), "Ground_Forest", ForestGround, SurfaceType.Forest),  // x -50..-18
                    (new Rect(-18f, -46f, 36f, 74f), "Ground_Rocky", RockyGround, SurfaceType.Gravel),    // x -18..18
                    (new Rect(18f, -46f, 32f, 74f), "Ground_Meadow", MeadowGround, SurfaceType.Grass),    // x 18..50
                    (new Rect(-50f, 28f, 100f, 32f), "Ground_North", NorthGround, SurfaceType.Dirt),      // z 28..60
                },
                PlaceCover = PlaceCover,
                ExtraReachabilityChecks = new[]
                {
                    (PlayerSpawn, ForestPoint), (ForestPoint, Extraction),
                    (PlayerSpawn, CentralPoint), (CentralPoint, Extraction),
                    (PlayerSpawn, MeadowPoint), (MeadowPoint, Extraction),
                    (PlayerSpawn, NorthPoint), (NorthPoint, Extraction),
                },
                FirstInBuildSettings = true,
            });
        }

        static void PlaceCover(Transform environment)
        {
            var layout = new Layout(seed: 2025);
            ReserveOpenSpaces(layout);

            Transform Group(string name)
            {
                var group = new GameObject(name) { isStatic = true }.transform;
                group.SetParent(environment);
                return group;
            }

            Transform paths = Group("Trails");
            LevelBuilder.CreatePath(paths, ForestTrail, 2f);
            LevelBuilder.CreatePath(paths, CentralTrail, 2f);
            LevelBuilder.CreatePath(paths, MeadowTrail, 2f);

            PlaceSpawnArea(layout, Group("Spawn Area"));
            PlaceForest(layout, Group("West Forest"));
            PlaceRockyMiddle(layout, Group("Central Rocks"));
            PlaceMeadow(layout, Group("East Meadow"));
            PlaceNorth(layout, Group("North Final Zone"));

            Debug.Log($"Demo scene layout: {layout.Placed} obstacles/props placed, {layout.Rejected} candidates rejected by spacing rules.");
        }

        /// <summary>Areas nothing may be placed on: trails, spawn, extraction, pickups, zombie spots, clearings.</summary>
        static void ReserveOpenSpaces(Layout layout)
        {
            layout.Keep(PlayerSpawn, 7f);
            layout.Keep(Extraction, 8f);
            foreach (var (spawn, centre, _) in Zombies)
            {
                layout.Keep(spawn, 1.5f);
                layout.Keep(centre, 1.5f);
            }
            foreach (Vector3 pickup in RockPickups)
                layout.Keep(pickup, 1.2f);
            foreach (Vector3 point in new[] { ForestPoint, CentralPoint, MeadowPoint, NorthPoint })
                layout.Keep(point, 1.5f);

            foreach (Vector3[] trail in new[] { ForestTrail, CentralTrail, MeadowTrail })
                layout.KeepAlong(trail, 1.4f);

            // Forest clearings: breathing room for searching zombies and a visual break.
            layout.Keep(new Vector3(-40f, 0f, -20f), 4f);
            layout.Keep(new Vector3(-25f, 0f, -2f), 3.5f);
            layout.Keep(new Vector3(-43f, 0f, 18f), 4f);
        }

        // ------------------------------------------------------------------ zones

        static void PlaceSpawnArea(Layout layout, Transform parent)
        {
            layout.Bush(parent, new Vector3(-8f, 0f, -52f));
            layout.Bush(parent, new Vector3(9f, 0f, -50f));
            layout.Rock(parent, new Vector3(-14f, 0f, -54f), new Vector3(3f, 1.8f, 2.5f));
            layout.Rock(parent, new Vector3(16f, 0f, -55f), new Vector3(3.5f, 2f, 3f));

            var area = new Rect(-46.5f, -56.5f, 93f, 9.5f);   // kept 3.5 m off the boundary walls
            layout.Scatter(area, 60, p => layout.Chance(0.5f) ? layout.Tree(parent, p, layout.Pick(0, 2)) : layout.Bush(parent, p));
        }

        /// <summary>WEST: dense mixed forest with bushes, logs, a few rocks; darker foliage; stumps and pebbles.</summary>
        static void PlaceForest(Layout layout, Transform parent)
        {
            var forest = new Rect(-46.5f, -44f, 27.5f, 70f);   // x -46.5..-19, z -44..26 (3.5 m off the wall)
            var northWest = new Rect(-46.5f, 26f, 20.5f, 20f); // x -46.5..-26, z 26..46 (forest runs into the north zone)

            foreach (Rect area in new[] { forest, northWest })
            {
                int attempts = area == forest ? 1400 : 380;
                layout.Scatter(area, attempts, p =>
                {
                    float roll = layout.Value();
                    if (roll < 0.80f) return layout.Tree(parent, p, layout.Value() < 0.47f ? 1 : layout.Value() < 0.85f ? 0 : 2);
                    if (roll < 0.93f) return layout.Bush(parent, p);
                    if (roll < 0.97f) return layout.Rock(parent, p, new Vector3(layout.Range(2f, 3.5f), layout.Range(1.6f, 2.4f), layout.Range(2f, 3.5f)));
                    return layout.Log(parent, p, layout.Range(0f, 360f));
                });
            }

            layout.Decorate(forest, 30, p => LevelBuilder.CreateStump(parent, p));
            layout.Decorate(forest, 50, p => LevelBuilder.CreatePebble(parent, p, layout.Range(0.2f, 0.45f)));
        }

        /// <summary>MIDDLE: designed boulder clusters, a ridge on the meadow side, a raised mound, small rocks.</summary>
        static void PlaceRockyMiddle(Layout layout, Transform parent)
        {
            // Big boulders (≈2–2.5 m: hide a standing player). Low ones (≈1.4–1.5 m) only hide a crouching player.
            (Vector3 position, Vector3 size)[] boulders =
            {
                (new Vector3(-9f, 0f, -41f),  new Vector3(4f, 2.2f, 3f)),
                (new Vector3(9f, 0f, -34f),   new Vector3(3.5f, 2.4f, 3.5f)),
                (new Vector3(-12f, 0f, -27f), new Vector3(3.5f, 2.0f, 3f)),
                (new Vector3(7f, 0f, -24f),   new Vector3(4.5f, 2.4f, 3.5f)),
                (new Vector3(5f, 0f, -17f),   new Vector3(3f, 2.2f, 2.5f)),
                (new Vector3(-10f, 0f, -15f), new Vector3(4f, 2.3f, 3.5f)),
                (new Vector3(8f, 0f, -11f),   new Vector3(3f, 1.5f, 3f)),     // low
                (new Vector3(-12f, 0f, -2f),  new Vector3(3.5f, 2.2f, 3.5f)),
                (new Vector3(9f, 0f, 0f),     new Vector3(4f, 2.4f, 3f)),
                (new Vector3(-8f, 0f, 10f),   new Vector3(4.5f, 2.2f, 3.5f)),
                (new Vector3(10f, 0f, 12f),   new Vector3(3f, 1.4f, 3f)),     // low
                (new Vector3(-11f, 0f, 20f),  new Vector3(3.5f, 2.4f, 3f)),
                (new Vector3(8f, 0f, 22f),    new Vector3(4f, 2.0f, 3.5f)),
                // Rocky ridge along the meadow side (gaps between them stay walkable).
                (new Vector3(16f, 0f, -42f),  new Vector3(3f, 2.2f, 2.5f)),
                (new Vector3(16.5f, 0f, -35f), new Vector3(3f, 2.4f, 2.5f)),
                (new Vector3(16f, 0f, -28f),  new Vector3(3f, 2.0f, 2.5f)),
            };
            foreach (var (position, size) in boulders)
                layout.Rock(parent, position, size);

            layout.Mound(parent, new Vector3(13f, 0f, -18f), 6f, 6.5f, 0.9f, "Mound_Rocky", RockyGround * 1.1f, SurfaceType.Gravel);

            var area = new Rect(-17f, -44f, 34f, 70f);  // x -17..17, z -44..26
            layout.Scatter(area, 180, p => layout.Rock(parent, p, new Vector3(layout.Range(1f, 2f), layout.Range(0.7f, 1.5f), layout.Range(1f, 2f))));
            layout.Scatter(area, 70, p => layout.Chance(0.35f) && layout.Tree(parent, p, layout.Pick(0, 2)));
            layout.Scatter(area, 80, p => layout.Chance(0.3f) && layout.Bush(parent, p));
            layout.Decorate(area, 90, p => LevelBuilder.CreatePebble(parent, p, layout.Range(0.2f, 0.5f)));
        }

        /// <summary>EAST: open grass with gentle hills, sparse trees, bushes and low rocks; long sight lines.</summary>
        static void PlaceMeadow(Layout layout, Transform parent)
        {
            Color hill = MeadowGround * 1.08f;
            layout.Mound(parent, new Vector3(27f, 0f, -24f), 7f, 6f, 1.0f, "Mound_Meadow", hill, SurfaceType.Grass);
            layout.Mound(parent, new Vector3(26f, 0f, 6f), 6f, 7f, 0.9f, "Mound_Meadow", hill, SurfaceType.Grass);
            layout.Mound(parent, new Vector3(44f, 0f, 22f), 6f, 6f, 0.9f, "Mound_Meadow", hill, SurfaceType.Grass);

            var area = new Rect(19f, -44f, 27.5f, 70f);   // x 19..46.5, z -44..26
            layout.Scatter(area, 40, p => layout.Chance(0.5f) && layout.Tree(parent, p, layout.Value() < 0.6f ? 0 : 2));
            layout.Scatter(area, 60, p => layout.Chance(0.4f) && layout.Bush(parent, p));
            layout.Scatter(area, 40, p => layout.Chance(0.4f) && layout.Rock(parent, p, new Vector3(layout.Range(1.5f, 2.5f), layout.Range(1.1f, 1.5f), layout.Range(1.5f, 2.5f))));
            layout.Decorate(area, 30, p => LevelBuilder.CreatePebble(parent, p, layout.Range(0.15f, 0.35f)));
        }

        /// <summary>NORTH: abandoned campsite, broken fences, mixed trees/rocks; the last decision before extraction.</summary>
        static void PlaceNorth(Layout layout, Transform parent)
        {
            // Campsite west of the extraction.
            layout.Tent(parent, new Vector3(-18f, 0f, 49f), 20f);
            layout.Tent(parent, new Vector3(-7f, 0f, 44f), -30f);
            layout.Crates(parent, new Vector3(-21f, 0f, 45f), -15f, stack: 1);
            layout.Crates(parent, new Vector3(-4f, 0f, 37f), 10f, stack: 2);
            layout.Campfire(parent, new Vector3(-15f, 0f, 52f));

            // Broken fence line across the northern band (segments crossing a trail are skipped automatically).
            for (float x = -24f; x <= -4f; x += 5f)
                layout.Fence(parent, new Vector3(x, 0f, 33f), layout.Range(-12f, 12f));
            for (float x = 18f; x <= 33f; x += 5f)
                layout.Fence(parent, new Vector3(x, 0f, 44f), layout.Range(-12f, 12f));

            layout.Mound(parent, new Vector3(30f, 0f, 50f), 6f, 5f, 0.9f, "Mound_North", NorthGround * 1.1f, SurfaceType.Grass);

            var area = new Rect(-26f, 27f, 72.5f, 29.5f); // x -26..46.5, z 27..56.5
            layout.Scatter(area, 220, p => layout.Chance(0.55f) && layout.Tree(parent, p, layout.Pick(0, 3)));
            layout.Scatter(area, 90, p => layout.Chance(0.35f) && layout.Rock(parent, p,
                layout.Chance(0.5f) ? new Vector3(layout.Range(2.5f, 4f), layout.Range(1.9f, 2.5f), layout.Range(2.5f, 3.5f))
                                    : new Vector3(layout.Range(1.5f, 2.5f), layout.Range(1.1f, 1.5f), layout.Range(1.5f, 2.5f))));
            layout.Scatter(area, 120, p => layout.Chance(0.35f) && layout.Bush(parent, p));
            layout.Decorate(area, 40, p => LevelBuilder.CreatePebble(parent, p, layout.Range(0.2f, 0.45f)));
        }

        static Vector3[] Points(params (float x, float z)[] points)
        {
            var result = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = new Vector3(points[i].x, 0f, points[i].z);
            return result;
        }

        // ------------------------------------------------------------------ spacing system

        /// <summary>
        /// Remembers the footprint (circle) of everything placed and only allows new things that keep
        /// enough distance: 2.5 m clear between two solid obstacles (agent is 1 m wide → always a safe gap),
        /// 0.3 m otherwise (bushes, hills, kept-clear areas). Seeded, so the result is reproducible.
        /// </summary>
        class Layout
        {
            const float SolidGap = 2.5f;
            const float SoftGap = 0.3f;

            readonly List<(Vector2 centre, float radius, bool solid)> footprints = new List<(Vector2, float, bool)>();
            readonly System.Random random;

            public int Placed { get; private set; }
            public int Rejected { get; private set; }

            public Layout(int seed) => random = new System.Random(seed);

            // ---- randomness
            public float Value() => (float)random.NextDouble();
            public float Range(float min, float max) => min + Value() * (max - min);
            public bool Chance(float probability) => Value() < probability;
            public int Pick(int min, int maxExclusive) => random.Next(min, maxExclusive);

            // ---- spacing
            bool IsFree(Vector3 position, float radius, bool solid)
            {
                var centre = new Vector2(position.x, position.z);
                foreach (var (otherCentre, otherRadius, otherSolid) in footprints)
                {
                    float gap = solid && otherSolid ? SolidGap : SoftGap;
                    float minDistance = radius + otherRadius + gap;
                    if ((otherCentre - centre).sqrMagnitude < minDistance * minDistance)
                        return false;
                }
                return true;
            }

            bool TryReserve(Vector3 position, float radius, bool solid)
            {
                if (!IsFree(position, radius, solid))
                {
                    Rejected++;
                    return false;
                }
                footprints.Add((new Vector2(position.x, position.z), radius, solid));
                Placed++;
                return true;
            }

            public void Keep(Vector3 position, float radius) => footprints.Add((new Vector2(position.x, position.z), radius, false));

            public void KeepAlong(Vector3[] trail, float radius)
            {
                for (int i = 0; i < trail.Length - 1; i++)
                {
                    float length = Vector3.Distance(trail[i], trail[i + 1]);
                    for (float d = 0f; d < length; d += 1.5f)
                        Keep(Vector3.Lerp(trail[i], trail[i + 1], d / length), radius);
                }
                Keep(trail[trail.Length - 1], radius);
            }

            /// <summary>Tries <paramref name="attempts"/> random points in the area; place returns whether it placed something.</summary>
            public void Scatter(Rect area, int attempts, System.Func<Vector3, bool> place)
            {
                for (int i = 0; i < attempts; i++)
                    place(new Vector3(Range(area.xMin, area.xMax), 0f, Range(area.yMin, area.yMax)));
            }

            /// <summary>Decoration without collision: only avoids solid obstacles' footprints, reserves nothing.</summary>
            public void Decorate(Rect area, int count, System.Action<Vector3> place)
            {
                for (int i = 0; i < count; i++)
                {
                    var position = new Vector3(Range(area.xMin, area.xMax), 0f, Range(area.yMin, area.yMax));
                    if (IsFree(position, 0.3f, solid: false))
                        place(position);
                }
            }

            // ---- placing (each returns true if it was placed)
            public bool Tree(Transform parent, Vector3 p, int variant)
            {
                if (!TryReserve(p, 0.55f, solid: true)) return false;
                LevelBuilder.CreateTree(parent, p, variant);
                return true;
            }

            public bool Rock(Transform parent, Vector3 p, Vector3 size)
            {
                // +15%: the lumpy mesh can bulge a little past its nominal size.
                if (!TryReserve(p, Mathf.Max(size.x, size.z) * 0.5f * 1.15f, solid: true)) return false;
                LevelBuilder.CreateRock(parent, p, size);
                return true;
            }

            public bool Bush(Transform parent, Vector3 p)
            {
                if (!TryReserve(p, 1.35f, solid: false)) return false;
                LevelBuilder.CreateBush(parent, p);
                return true;
            }

            public bool Mound(Transform parent, Vector3 centre, float radiusX, float radiusZ, float height, string material, Color color,
                              SurfaceType surface)
            {
                // Visible footprint of the sunk ellipsoid is ~0.79 of its radius.
                if (!TryReserve(centre, Mathf.Max(radiusX, radiusZ) * 0.79f, solid: false)) return false;
                LevelBuilder.CreateMound(parent, centre, radiusX, radiusZ, height, material, color, surface);
                return true;
            }

            public bool Log(Transform parent, Vector3 p, float yaw)
            {
                if (!TryReserve(p, 1.7f, solid: true)) return false;
                var log = LevelBuilder.CreateSolidProp(parent, "Log", p, yaw, new Vector3(0.5f, 0.45f, 3.2f));
                EnvironmentArt.AddPrimitive(log.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.45f, 1.6f, 0.45f),
                                            new Vector3(90f, 0f, 0f), LevelBuilder.GetMaterial("Trunk", new Color(0.36f, 0.25f, 0.15f)));
                return true;
            }

            public bool Tent(Transform parent, Vector3 p, float yaw)
            {
                if (!TryReserve(p, 2.2f, solid: true)) return false;
                var tent = LevelBuilder.CreateSolidProp(parent, "Tent", p, yaw, new Vector3(2.8f, 1.4f, 3.2f));
                // A cube rotated 45° and half sunk into the ground reads as a ridge tent.
                EnvironmentArt.AddPrimitive(tent.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(2f, 2f, 3.2f),
                                            new Vector3(0f, 0f, 45f), LevelBuilder.GetMaterial("Tent", new Color(0.36f, 0.38f, 0.22f)));
                return true;
            }

            public bool Crates(Transform parent, Vector3 p, float yaw, int stack)
            {
                if (!TryReserve(p, 1.0f, solid: true)) return false;
                var crates = LevelBuilder.CreateSolidProp(parent, "Crates", p, yaw, new Vector3(1.1f, stack, 1.1f));
                Material wood = LevelBuilder.GetMaterial("Wood", new Color(0.45f, 0.33f, 0.2f));
                for (int i = 0; i < stack; i++)
                    EnvironmentArt.AddPrimitive(crates.transform, PrimitiveType.Cube, new Vector3(0f, 0.5f + i, 0f), Vector3.one, new Vector3(0f, i * 12f, 0f), wood);
                return true;
            }

            public bool Fence(Transform parent, Vector3 p, float yaw)
            {
                if (!TryReserve(p, 1.7f, solid: true)) return false;
                var fence = LevelBuilder.CreateSolidProp(parent, "Fence", p, yaw, new Vector3(3f, 1.2f, 0.25f));
                Material wood = LevelBuilder.GetMaterial("Wood", new Color(0.45f, 0.33f, 0.2f));
                EnvironmentArt.AddPrimitive(fence.transform, PrimitiveType.Cube, new Vector3(-1.4f, 0.6f, 0f), new Vector3(0.15f, 1.2f, 0.15f), Vector3.zero, wood);
                EnvironmentArt.AddPrimitive(fence.transform, PrimitiveType.Cube, new Vector3(1.4f, 0.6f, 0f), new Vector3(0.15f, 1.2f, 0.15f), Vector3.zero, wood);
                EnvironmentArt.AddPrimitive(fence.transform, PrimitiveType.Cube, new Vector3(0f, 0.9f, 0f), new Vector3(3f, 0.15f, 0.08f), new Vector3(0f, 0f, 3f), wood);
                EnvironmentArt.AddPrimitive(fence.transform, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(3f, 0.15f, 0.08f), new Vector3(0f, 0f, -4f), wood);
                return true;
            }

            /// <summary>Decorative campfire (no collider) with a small warm light.</summary>
            public void Campfire(Transform parent, Vector3 p)
            {
                Keep(p, 1.2f);
                var fire = new GameObject("Campfire") { isStatic = true };
                fire.transform.SetParent(parent);
                fire.transform.position = p;
                for (int i = 0; i < 8; i++)
                    LevelBuilder.CreatePebble(fire.transform, p + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 0.6f, 0.25f);
                Material ash = LevelBuilder.GetMaterial("Ash", new Color(0.12f, 0.11f, 0.1f));
                EnvironmentArt.AddPrimitive(fire.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(1f, 0.02f, 1f), Vector3.zero, ash);
                EnvironmentArt.AddPrimitive(fire.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.45f, 0.12f), new Vector3(90f, 30f, 0f), ash);
                EnvironmentArt.AddPrimitive(fire.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.45f, 0.12f), new Vector3(90f, -40f, 0f), ash);

                var light = new GameObject("Fire Light").AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.55f, 0.2f);
                light.range = 5f;
                light.intensity = 1.5f;
                light.transform.SetParent(fire.transform, false);
                light.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            }
        }
    }
}
