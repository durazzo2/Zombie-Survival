using UnityEngine;
using UnityEngine.InputSystem;
using ZombieStealth.Gameplay;

namespace ZombieStealth.Player
{
    /// <summary>
    /// Throws rocks with the Attack action (left mouse). Keeps a simple rock count and shows it on screen.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerThrower : MonoBehaviour
    {
        [SerializeField] Throwable rockPrefab;
        [SerializeField] int startRocks = 2;
        [SerializeField] int maxRocks = 3;

        [Header("Throw")]
        [SerializeField] float forwardSpeed = 12f;
        [SerializeField] float upwardSpeed = 3f;
        [SerializeField] float spawnDistance = 0.6f;   // in front of the camera

        Transform cameraTransform;
        CharacterController controller;
        InputAction attackAction;
        bool cursorWasLocked;

        public int RockCount { get; private set; }
        public int MaxRocks => maxRocks;

        void Awake()
        {
            cameraTransform = GetComponentInChildren<Camera>().transform;
            controller = GetComponent<CharacterController>();
            attackAction = InputSystem.actions.FindAction("Player/Attack", true);
            RockCount = startRocks;
        }

        void Update()
        {
            // PlayerLook re-locks the cursor on left click. Only throw if the cursor was ALREADY locked
            // last frame, so the click that just re-locked it doesn't also throw (independent of script order).
            if (attackAction.WasPressedThisFrame() && cursorWasLocked && RockCount > 0)
                Throw();
        }

        void LateUpdate()
        {
            cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
        }

        void Throw()
        {
            RockCount--;
            Vector3 spawnPosition = cameraTransform.position + cameraTransform.forward * spawnDistance;
            Throwable rock = Instantiate(rockPrefab, spawnPosition, Random.rotation);
            rock.Launch(cameraTransform.forward * forwardSpeed + Vector3.up * upwardSpeed, controller);
        }

        /// <summary>Called by pickups. Returns false when already full.</summary>
        public bool TryAddRock()
        {
            if (RockCount >= maxRocks)
                return false;
            RockCount++;
            return true;
        }

        GUIStyle countStyle;

        void OnGUI()
        {
            countStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(16, 12, 300, 40), $"Rocks: {RockCount} / {maxRocks}", countStyle);
        }
    }
}
