using UnityEngine;
using ZombieStealth.Player;

namespace ZombieStealth.AI.Perception
{
    /// <summary>
    /// Answers one question every frame: "can this zombie see the player?"
    /// Pure perception — it only exposes data; the Behavior Tree decides what to do with it.
    ///
    /// Checks, in order: distance (shorter if the player crouches) → field of view
    /// (ignored at very close range) → line of sight against the Obstacle layer.
    /// After direct sight is lost, CanSeePlayer stays true for a short grace period.
    /// </summary>
    public class ZombieVision : MonoBehaviour
    {
        public enum Result { NoPlayer, OutOfRange, OutsideFieldOfView, Blocked, Visible }

        [SerializeField] float viewDistance = 18f;
        [SerializeField, Range(1f, 360f)] float fieldOfView = 110f;
        [Tooltip("Closer than this the zombie notices the player even outside its field of view (still needs line of sight).")]
        [SerializeField] float proximityRadius = 2.5f;
        [SerializeField, Range(0f, 1f)] float crouchDistanceMultiplier = 0.6f;
        [Tooltip("How long CanSeePlayer stays true after line of sight breaks (stops flicker around thin trunks).")]
        [SerializeField] float gracePeriod = 0.4f;
        [SerializeField] float eyeHeight = 0.6f;   // above this transform's pivot
        [Tooltip("Where on the player we aim the sight line, as a fraction of their current height (0.75 = upper body).")]
        [SerializeField, Range(0f, 1f)] float playerTargetHeight = 0.75f;

        PlayerMovement player;
        CharacterController playerController;
        int obstacleMask;
        float lastDirectSightTime = float.NegativeInfinity;

        // ---- Perception data ----
        public bool CanSeePlayer => Time.time - lastDirectSightTime <= gracePeriod;
        public bool HasDirectSight => LastResult == Result.Visible;
        public bool InGracePeriod => CanSeePlayer && !HasDirectSight;
        public bool IsLineOfSightBlocked => LastResult == Result.Blocked;
        public Result LastResult { get; private set; }
        public float DistanceToPlayer { get; private set; }

        /// <summary>Player's current feet position. Only meaningful while CanSeePlayer is true.</summary>
        public Vector3 PlayerPosition => player.transform.position;

        /// <summary>Player position (feet) the last time we had direct sight; null if never seen.</summary>
        public Vector3? LastSeenPosition { get; private set; }

        // ---- Values for the debug view ----
        public float ViewDistance => viewDistance;
        public float FieldOfView => fieldOfView;
        public float ProximityRadius => proximityRadius;
        public float EffectiveViewDistance { get; private set; }
        public Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;
        public Vector3 PlayerTargetPosition { get; private set; }

        void Awake()
        {
            obstacleMask = LayerMask.GetMask("Obstacle");
            EffectiveViewDistance = viewDistance;
        }

        void Start()
        {
            player = FindAnyObjectByType<PlayerMovement>();
            if (player != null)
                playerController = player.GetComponent<CharacterController>();
        }

        void Update()
        {
            LastResult = Look();
            if (LastResult == Result.Visible)
            {
                lastDirectSightTime = Time.time;
                LastSeenPosition = player.transform.position;
            }
        }

        Result Look()
        {
            if (player == null)
                return Result.NoPlayer;

            PlayerTargetPosition = player.transform.position + Vector3.up * (playerController.height * playerTargetHeight);
            Vector3 toPlayer = PlayerTargetPosition - EyePosition;
            DistanceToPlayer = toPlayer.magnitude;

            // 1 + 2. Distance — a crouching player has to be closer to be seen.
            EffectiveViewDistance = player.IsCrouching ? viewDistance * crouchDistanceMultiplier : viewDistance;
            if (DistanceToPlayer > EffectiveViewDistance)
                return Result.OutOfRange;

            // 3 + 4. Field of view (measured flat, ignoring height), skipped when the player is very close.
            Vector3 flatDirection = new Vector3(toPlayer.x, 0f, toPlayer.z);
            bool insideFieldOfView = Vector3.Angle(transform.forward, flatDirection) <= fieldOfView / 2f;
            bool veryClose = DistanceToPlayer <= proximityRadius;
            if (!insideFieldOfView && !veryClose)
                return Result.OutsideFieldOfView;

            // 5 + 6. Line of sight: anything on the Obstacle layer in between blocks it.
            // Triggers count too, so walk-through bushes still hide the player.
            if (Physics.Linecast(EyePosition, PlayerTargetPosition, obstacleMask, QueryTriggerInteraction.Collide))
                return Result.Blocked;

            return Result.Visible;
        }
    }
}
