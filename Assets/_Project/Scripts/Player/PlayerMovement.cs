using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieStealth.Player
{
    /// <summary>
    /// First-person movement: walk, sprint (Left Shift) and crouch toggle (C).
    /// Reads the project-wide Input System actions ("Player" map).
    /// The pivot is at the feet; crouching shrinks the CharacterController and lowers the camera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Speeds (m/s)")]
        [SerializeField] float walkSpeed = 3f;
        [SerializeField] float sprintSpeed = 5.5f;
        [SerializeField] float crouchSpeed = 1.8f;

        [Header("Crouch")]
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.0f;
        [SerializeField] float crouchTransitionSpeed = 6f; // metres of height per second
        [SerializeField] float eyeOffsetFromTop = 0.15f;   // camera sits this far below the top of the capsule

        [SerializeField] float gravity = -20f;

        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }

        CharacterController controller;
        Transform cameraTransform;
        InputAction moveAction;
        InputAction sprintAction;
        InputAction crouchAction;
        float verticalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            cameraTransform = GetComponentInChildren<Camera>().transform;

            moveAction = InputSystem.actions.FindAction("Player/Move", true);
            sprintAction = InputSystem.actions.FindAction("Player/Sprint", true);
            crouchAction = InputSystem.actions.FindAction("Player/Crouch", true);

            SetHeight(standHeight);
        }

        void Update()
        {
            if (crouchAction.WasPressedThisFrame())
            {
                // Only stand up if nothing is above our head (e.g. a low bar).
                if (!IsCrouching || HasRoomToStand())
                    IsCrouching = !IsCrouching;
            }

            UpdateHeight();
            Move();
        }

        void Move()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            IsSprinting = sprintAction.IsPressed() && !IsCrouching && input.y > 0f;

            float speed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * speed;

            // Simple gravity so the player stays on the ground.
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            velocity.y = verticalVelocity;

            controller.Move(velocity * Time.deltaTime);
        }

        void UpdateHeight()
        {
            float target = IsCrouching ? crouchHeight : standHeight;
            SetHeight(Mathf.MoveTowards(controller.height, target, crouchTransitionSpeed * Time.deltaTime));
        }

        void SetHeight(float height)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height / 2f, 0f); // keep the feet on the ground
            cameraTransform.localPosition = new Vector3(0f, height - eyeOffsetFromTop, 0f);
        }

        bool HasRoomToStand()
        {
            // Cast upward from the top of the current capsule (casts ignore our own collider since they start inside it).
            float radius = controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (controller.height - controller.radius);
            float distance = standHeight - controller.height;
            return !Physics.SphereCast(origin, radius, Vector3.up, out _, distance, ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
