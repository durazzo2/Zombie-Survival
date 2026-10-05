using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieStealth.Player;

namespace ZombieStealth.Editor
{
    /// <summary>
    /// Phase 1: builds a throwaway scene for testing the first-person player.
    /// Menu: Tools → Zombie Stealth → Build Player Test Scene
    /// </summary>
    public static class PlayerTestSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/PlayerTest.unity";

        [MenuItem("Tools/Zombie Stealth/Build Player Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 5f); // 50 x 50 m

            CreateBox("Wall", new Vector3(0f, 1f, 8f), new Vector3(6f, 2f, 1f));
            CreateBox("Pillar", new Vector3(-5f, 1.5f, 3f), new Vector3(1f, 3f, 1f));
            // Bottom of the bar is at 1.2 m: standing (1.8 m) is blocked, crouching (1.0 m) fits under.
            CreateBox("Crouch Bar", new Vector3(5f, 1.6f, 4f), new Vector3(3f, 0.8f, 3f));

            CreatePlayer(new Vector3(0f, 0f, -5f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Player test scene saved to {ScenePath}");
        }

        static void CreateBox(string name, Vector3 position, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
        }

        internal static GameObject CreatePlayer(Vector3 position)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = position;

            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.35f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            var camera = new GameObject("Camera") { tag = "MainCamera" };
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            camera.AddComponent<Camera>().nearClipPlane = 0.05f;
            camera.AddComponent<AudioListener>();

            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerLook>();
            player.AddComponent<PlayerNoise>();
            return player;
        }
    }
}
