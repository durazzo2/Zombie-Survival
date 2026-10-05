using UnityEditor;
using UnityEngine;

namespace ZombieStealth.Editor
{
    /// <summary>
    /// Tiny 30 x 30 m arena used while developing each AI feature (Phases 2–11).
    /// All the actual building is done by LevelBuilder; this file only describes the layout.
    /// Menu: Tools → Zombie Stealth → Build Test Arena
    /// </summary>
    public static class TestArenaBuilder
    {
        [MenuItem("Tools/Zombie Stealth/Build Test Arena")]
        public static void Build()
        {
            LevelBuilder.Build(new LevelDefinition
            {
                Name = "Test arena",
                ScenePath = "Assets/_Project/Scenes/TestArena.unity",
                Size = new Vector2(30f, 30f),
                PlayerSpawn = new Vector3(0f, 0f, -12f),
                ExtractionPosition = new Vector3(0f, 0f, 12.5f), // opposite end from the spawn
                Zombies = new[] { (new Vector3(3f, 0f, 6f), new Vector3(0f, 0f, 3f), 8f) },
                RockPickups = new[]
                {
                    new Vector3(-3f, 0f, -10f),
                    new Vector3(6f, 0f, -11f),
                    new Vector3(-11f, 0f, 0f),
                },
                PlaceCover = PlaceCover,
            });
        }

        // Hand-placed cover. Gaps between obstacles are kept >= 2.5 m so agents never squeeze.
        static void PlaceCover(Transform parent)
        {
            LevelBuilder.CreateRock(parent, new Vector3(-6f, 0f, -3f), new Vector3(3f, 1.8f, 2.5f));
            LevelBuilder.CreateRock(parent, new Vector3(5f, 0f, 2f), new Vector3(2.5f, 2.2f, 3f));
            LevelBuilder.CreateRock(parent, new Vector3(-2f, 0f, 7f), new Vector3(4f, 1.6f, 2f));

            LevelBuilder.CreateTree(parent, new Vector3(6f, 0f, -6f));
            LevelBuilder.CreateTree(parent, new Vector3(-9f, 0f, 4f));
            LevelBuilder.CreateTree(parent, new Vector3(8f, 0f, 9f));
            LevelBuilder.CreateTree(parent, new Vector3(1f, 0f, -2f));

            LevelBuilder.CreateBush(parent, new Vector3(-4f, 0f, -8f));
        }
    }
}
