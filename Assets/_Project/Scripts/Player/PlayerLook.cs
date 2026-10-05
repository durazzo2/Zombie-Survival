using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieStealth.Player
{
    /// <summary>
    /// Mouse look: yaw turns the whole player, pitch tilts only the child camera.
    /// Locks the cursor; Escape unlocks it, left click locks it again.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] float sensitivity = 0.1f; // degrees per pixel of mouse movement
        [SerializeField] float maxPitch = 85f;

        Transform cameraTransform;
        InputAction lookAction;
        float pitch;

        void Awake()
        {
            cameraTransform = GetComponentInChildren<Camera>().transform;
            lookAction = InputSystem.actions.FindAction("Player/Look", true);
        }

        void OnEnable() => SetCursorLocked(true);
        void OnDisable() => SetCursorLocked(false);

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);

            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            // Mouse delta is already "per frame", so no Time.deltaTime here.
            Vector2 look = lookAction.ReadValue<Vector2>() * sensitivity;

            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
