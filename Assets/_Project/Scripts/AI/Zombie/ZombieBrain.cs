using UnityEngine;
using ZombieStealth.AI.BehaviorTree;
using ZombieStealth.AI.Perception;
using ZombieStealth.AI.Zombie.Actions;
using ZombieStealth.Gameplay;

namespace ZombieStealth.AI.Zombie
{
    /// <summary>
    /// The zombie's "brain": builds the Behavior Tree once, then every tick:
    ///   1. senses   — reads perception (ZombieVision, ZombieHearing)
    ///   2. remembers — writes what it perceived into ZombieMemory
    ///   3. decides  — ticks the Behavior Tree from the root
    /// </summary>
    [RequireComponent(typeof(ZombieMotor), typeof(ZombieVision), typeof(ZombieHearing))]
    public class ZombieBrain : MonoBehaviour
    {
        [SerializeField] WanderZone wanderZone;
        [SerializeField] float walkSpeed = 1.6f;
        [SerializeField] float runSpeed = 4.2f;
        [SerializeField] float catchDistance = 1.5f;

        [Header("Investigate")]
        [SerializeField] float investigateSpeed = 2.5f;
        [SerializeField] float investigateLookTime = 2.5f;

        [Header("Search")]
        [SerializeField] float searchSpeed = 3f;
        [SerializeField] float searchRadius = 6f;
        [SerializeField] int searchPointCount = 3;
        [SerializeField] float searchLookTime = 1.2f;   // seconds of looking around at each stop
        [SerializeField] float searchTimeout = 12f;     // max seconds for a whole search

        [Header("Ticking")]
        [SerializeField] float tickInterval = 0.1f;   // 10 decisions per second
        [Tooltip("0 = different every run. Any other value makes wandering repeat the same way (handy for debugging).")]
        [SerializeField] int randomSeed;

        Node root;
        ZombieVision vision;
        ZombieHearing hearing;
        float nextTickTime;

        public ZombieMemory Memory { get; } = new ZombieMemory();

        /// <summary>Action instances exposed so the debug view can show their progress.</summary>
        public SearchAction Search { get; private set; }
        public InvestigateAction Investigate { get; private set; }

        /// <summary>Name of the top-level branch currently running, e.g. "WANDER".</summary>
        public string CurrentState { get; private set; } = "-";

        /// <summary>Chain of running nodes from the root, e.g. "ROOT > WANDER".</summary>
        public string ActivePath { get; private set; } = "-";

        void Awake()
        {
            if (randomSeed != 0)
                Random.InitState(randomSeed);

            vision = GetComponent<ZombieVision>();
            hearing = GetComponent<ZombieHearing>();
            root = BuildTree(GetComponent<ZombieMotor>(), vision, FindAnyObjectByType<GameManager>());
        }

        /// <summary>
        /// Children of ROOT are in priority order: the first branch that doesn't fail wins this tick.
        /// </summary>
        Node BuildTree(ZombieMotor motor, ZombieVision vision, GameManager gameManager)
        {
            // Each branch gets its own condition node, so the tree stays a real tree.
            Condition CanSeePlayer() => new Condition("CanSeePlayer", () => vision.CanSeePlayer);

            Investigate = new InvestigateAction("InvestigateNoise", motor, Memory, investigateSpeed, investigateLookTime);
            Search = new SearchAction("SearchLastKnownPosition", motor, Memory,
                runSpeed: runSpeed, searchSpeed: searchSpeed, searchRadius: searchRadius,
                pointCount: searchPointCount, lookTime: searchLookTime, timeout: searchTimeout);

            return new Selector("ROOT",
                new Sequence("CATCH",
                    CanSeePlayer(),
                    new Condition("PlayerInCatchRange", () => vision.DistanceToPlayer <= catchDistance),
                    new CatchPlayerAction("CatchPlayer", motor, gameManager)),
                new Sequence("CHASE",
                    CanSeePlayer(),
                    new ChasePlayerAction("ChasePlayer", motor, vision, runSpeed)),
                // Reached only when CATCH and CHASE failed, i.e. the player is NOT visible.
                new Sequence("INVESTIGATE",
                    new Condition("HasHeardNoise", () => Memory.HasHeardNoise),
                    Investigate),
                new Sequence("SEARCH",
                    new Condition("HasLastKnownPosition", () => Memory.HasLastKnownPosition),
                    Search),
                new WanderAction("WANDER", motor, wanderZone, walkSpeed));
        }

        void Update()
        {
            if (Time.time < nextTickTime)
                return;
            nextTickTime = Time.time + tickInterval;

            UpdateMemory();
            root.Tick();
            UpdateDebugInfo();
        }

        void UpdateMemory()
        {
            // Always take the heard noise (so an old one can't linger and be used later).
            bool heardNoise = hearing.TryTakeHeardNoise(out NoiseEvent noise);

            // Seeing beats hearing. CanSeePlayer includes the 0.4 s grace period, so the remembered
            // position keeps following the player a little after they break line of sight — it ends
            // up where they actually went behind cover. After that it stays put.
            if (vision.CanSeePlayer)
                Memory.RememberPlayerAt(vision.PlayerPosition);
            else if (heardNoise)
                Memory.HearNoise(noise.Position, noise.TimeStamp); // newer info: replaces the last-known position
        }

        void UpdateDebugInfo()
        {
            // Follow the Running child down from the root.
            ActivePath = root.Name;
            CurrentState = "-";
            Node node = root;
            while (node is Composite composite && FindRunningChild(composite) is Node child)
            {
                if (node == root)
                    CurrentState = child.Name;
                ActivePath += " > " + child.Name;
                node = child;
            }
        }

        static Node FindRunningChild(Composite composite)
        {
            foreach (Node child in composite.ChildNodes)
            {
                if (child.IsRunning)
                    return child;
            }
            return null;
        }
    }
}
