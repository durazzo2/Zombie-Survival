using UnityEngine;
using ZombieStealth.AI.Perception;
using ZombieStealth.Gameplay;

namespace ZombieStealth.Player
{
    /// <summary>
    /// Emits footstep noises while the player is actually moving. Louder and more frequent when
    /// sprinting, silent when crouching or standing still. The ground changes how far a step carries:
    /// gravel crunches (louder), soft forest floor muffles (quieter).
    /// </summary>
    [RequireComponent(typeof(PlayerMovement), typeof(CharacterController))]
    public class PlayerNoise : MonoBehaviour
    {
        public enum StepKind { Crouch, Walk, Sprint }

        /// <summary>Raised on every footstep (also crouched ones, which make no AI noise). Used for footstep audio.</summary>
        public event System.Action<StepKind, SurfaceType> Stepped;

        [Header("Noise radius (m)")]
        [SerializeField] float crouchRadius = 0f;
        [SerializeField] float walkRadius = 4f;
        [SerializeField] float sprintRadius = 12f;

        [Header("Radius multiplier per ground surface")]
        [SerializeField] float gravelMultiplier = 1.3f;
        [SerializeField] float dirtMultiplier = 1f;
        [SerializeField] float grassMultiplier = 0.9f;
        [SerializeField] float forestMultiplier = 0.8f;

        [Header("Seconds between footsteps")]
        [SerializeField] float walkInterval = 0.5f;
        [SerializeField] float sprintInterval = 0.35f;

        [SerializeField] float minMovingSpeed = 0.5f;

        PlayerMovement movement;
        CharacterController controller;
        float nextStepTime;

        void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            // Real movement, not just input (walking into a wall makes no footsteps).
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            if (velocity.magnitude < minMovingSpeed || Time.time < nextStepTime)
                return;

            nextStepTime = Time.time + (movement.IsSprinting ? sprintInterval : walkInterval);

            SurfaceType surface = FootstepSurface.Detect(transform.position);
            float radius = (movement.IsCrouching ? crouchRadius : movement.IsSprinting ? sprintRadius : walkRadius)
                           * SurfaceMultiplier(surface);

            Stepped?.Invoke(movement.IsCrouching ? StepKind.Crouch : movement.IsSprinting ? StepKind.Sprint : StepKind.Walk, surface);

            if (radius > 0f)
                NoiseSystem.Emit(transform.position, radius, NoiseType.Footstep);
        }

        float SurfaceMultiplier(SurfaceType surface) => surface switch
        {
            SurfaceType.Gravel => gravelMultiplier,
            SurfaceType.Grass => grassMultiplier,
            SurfaceType.Forest => forestMultiplier,
            _ => dirtMultiplier,
        };
    }
}
