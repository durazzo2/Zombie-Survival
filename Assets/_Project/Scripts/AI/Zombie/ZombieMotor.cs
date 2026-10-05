using UnityEngine;
using UnityEngine.AI;

namespace ZombieStealth.AI.Zombie
{
    /// <summary>
    /// Thin wrapper around the NavMeshAgent. Behavior Tree actions never touch the agent directly;
    /// they ask the motor to MoveTo / Stop and check HasArrived / IsStuck.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieMotor : MonoBehaviour
    {
        [SerializeField] float navMeshSampleDistance = 2f;
        [Tooltip("Close enough to count as arrived. ~1 m so two zombies heading to the same point can both 'arrive' (agents are 1 m wide).")]
        [SerializeField] float arriveDistance = 1f;
        [SerializeField] float stuckTimeout = 3f;     // seconds without real movement before giving up
        [SerializeField] float minMovingSpeed = 0.1f;

        NavMeshAgent agent;
        NavMeshPath path;
        float stuckTimer;
        float turnSpeed;   // degrees per second while standing still (LookAround)

        public NavMeshAgent Agent => agent;

        /// <summary>Where we are currently going (null when standing still).</summary>
        public Vector3? Destination { get; private set; }

        public bool HasArrived => Destination.HasValue && HorizontalDistanceTo(Destination.Value) <= arriveDistance;

        /// <summary>True if we have a destination but haven't really moved for a while, or the path broke.</summary>
        public bool IsStuck => Destination.HasValue
                               && (stuckTimer >= stuckTimeout || agent.pathStatus == NavMeshPathStatus.PathInvalid);

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
        }

        void Update()
        {
            if (!Destination.HasValue && turnSpeed != 0f)
                transform.Rotate(0f, turnSpeed * Time.deltaTime, 0f);

            bool tryingToMove = Destination.HasValue && !HasArrived;
            if (tryingToMove && agent.velocity.magnitude < minMovingSpeed)
                stuckTimer += Time.deltaTime;
            else
                stuckTimer = 0f;
        }

        /// <summary>
        /// Starts moving to the target. Returns false (and doesn't move) if the target isn't near
        /// the NavMesh or can't be fully reached.
        /// </summary>
        public bool MoveTo(Vector3 target)
        {
            if (!agent.isOnNavMesh)
                return false;
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                return false;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;

            agent.SetPath(path);
            Destination = hit.position;
            stuckTimer = 0f;
            turnSpeed = 0f;
            return true;
        }

        public void SetSpeed(float speed) => agent.speed = speed;

        public void Stop()
        {
            if (agent.isOnNavMesh)
                agent.ResetPath();
            Destination = null;
            stuckTimer = 0f;
            turnSpeed = 0f;
        }

        /// <summary>Stand still and slowly turn (negative = turn left). Ends on the next MoveTo or Stop.</summary>
        public void LookAround(float degreesPerSecond)
        {
            Stop();
            turnSpeed = degreesPerSecond;
        }

        float HorizontalDistanceTo(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }
    }
}
