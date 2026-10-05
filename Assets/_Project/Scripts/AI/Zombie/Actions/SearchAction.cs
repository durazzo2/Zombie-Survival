using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieStealth.AI.BehaviorTree;

namespace ZombieStealth.AI.Zombie.Actions
{
    /// <summary>
    /// Search around the remembered last-known player position:
    ///   1. run to the last-known position
    ///   2. look around there
    ///   3. visit a few nearby reachable search points, looking around at each
    ///   4. when done (or timed out) forget the player → Success, so the tree falls back to WANDER
    ///
    /// These steps live inside one action on purpose: the tree is re-evaluated from the root every
    /// tick, so a separate "MoveTo" node in front of it would restart (and re-path to the last-known
    /// position) on every tick. If the action is aborted (e.g. CHASE takes over) memory is NOT cleared,
    /// and the next search starts fresh from the newest last-known position.
    /// </summary>
    public class SearchAction : ActionNode
    {
        public enum Step { GoToLastKnownPosition, GoToSearchPoint, LookAround }

        const float LookTurnSpeed = 90f; // degrees per second

        readonly ZombieMotor motor;
        readonly ZombieMemory memory;
        readonly float runSpeed;
        readonly float searchSpeed;
        readonly float searchRadius;
        readonly int pointCount;
        readonly float lookTime;
        readonly float timeout;

        readonly List<Vector3> searchPoints = new List<Vector3>();
        readonly NavMeshPath path = new NavMeshPath();
        int currentPoint;
        float lookEndTime;
        float giveUpTime;

        // ---- For the debug view ----
        public Step CurrentStep { get; private set; }
        public IReadOnlyList<Vector3> SearchPoints => searchPoints;
        public int CurrentPointIndex => currentPoint;

        public SearchAction(string name, ZombieMotor motor, ZombieMemory memory, float runSpeed, float searchSpeed,
                            float searchRadius, int pointCount, float lookTime, float timeout) : base(name)
        {
            this.motor = motor;
            this.memory = memory;
            this.runSpeed = runSpeed;
            this.searchSpeed = searchSpeed;
            this.searchRadius = searchRadius;
            this.pointCount = pointCount;
            this.lookTime = lookTime;
            this.timeout = timeout;
        }

        protected override void OnStart()
        {
            searchPoints.Clear();
            currentPoint = -1;
            giveUpTime = Time.time + timeout;

            CurrentStep = Step.GoToLastKnownPosition;
            motor.SetSpeed(runSpeed);
            if (!motor.MoveTo(memory.LastKnownPosition))
                StartSearchingArea(); // can't get there — search around it from here
        }

        protected override NodeStatus OnUpdate()
        {
            switch (CurrentStep)
            {
                case Step.GoToLastKnownPosition:
                    if (motor.HasArrived || motor.IsStuck)
                        StartSearchingArea();
                    break;

                case Step.GoToSearchPoint:
                    if (motor.HasArrived)
                        StartLookingAround();
                    else if (motor.IsStuck)
                        GoToNextSearchPoint(); // skip this point
                    break;

                case Step.LookAround:
                    if (Time.time >= lookEndTime)
                        GoToNextSearchPoint();
                    break;
            }

            bool allPointsVisited = currentPoint >= searchPoints.Count;
            if (allPointsVisited || Time.time >= giveUpTime)
            {
                memory.ForgetPlayer();
                return NodeStatus.Success;
            }
            return NodeStatus.Running;
        }

        void StartSearchingArea()
        {
            GenerateSearchPoints(memory.LastKnownPosition);
            motor.SetSpeed(searchSpeed);
            StartLookingAround();
        }

        void StartLookingAround()
        {
            CurrentStep = Step.LookAround;
            lookEndTime = Time.time + lookTime;
            // Alternate turning direction at each stop so it sweeps both ways.
            motor.LookAround(currentPoint % 2 == 0 ? LookTurnSpeed : -LookTurnSpeed);
        }

        void GoToNextSearchPoint()
        {
            while (++currentPoint < searchPoints.Count)
            {
                if (motor.MoveTo(searchPoints[currentPoint]))
                {
                    CurrentStep = Step.GoToSearchPoint;
                    return;
                }
            }
            // No points left: OnUpdate sees allPointsVisited and finishes.
        }

        /// <summary>
        /// Spread points around the centre (one per "slice" of the circle) so the search covers
        /// different directions. Each point must be on the NavMesh and fully reachable from the centre.
        /// </summary>
        void GenerateSearchPoints(Vector3 centre)
        {
            searchPoints.Clear();
            if (!NavMesh.SamplePosition(centre, out NavMeshHit centreHit, 2f, NavMesh.AllAreas))
                return;

            float slice = 360f / pointCount;
            float startAngle = Random.Range(0f, 360f);

            for (int i = 0; i < pointCount; i++)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    float angle = startAngle + i * slice + Random.Range(-slice / 4f, slice / 4f);
                    float distance = Random.Range(searchRadius * 0.5f, searchRadius);
                    Vector3 candidate = centreHit.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;

                    if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)
                        && NavMesh.CalculatePath(centreHit.position, hit.position, NavMesh.AllAreas, path)
                        && path.status == NavMeshPathStatus.PathComplete)
                    {
                        searchPoints.Add(hit.position);
                        break;
                    }
                }
            }
        }
    }
}
