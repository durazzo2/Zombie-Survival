using UnityEngine;
using ZombieStealth.AI.BehaviorTree;
using ZombieStealth.Gameplay;

namespace ZombieStealth.AI.Zombie.Actions
{
    /// <summary>
    /// Fallback behavior: walk to a random point in the wander zone, wait a moment, repeat.
    /// Never finishes on its own (always Running) — it only stops when a higher-priority branch interrupts it.
    /// </summary>
    public class WanderAction : ActionNode
    {
        const int MaxPickAttempts = 5;
        const float RetryDelay = 1f;

        readonly ZombieMotor motor;
        readonly WanderZone zone;
        readonly float speed;
        readonly float minWait;
        readonly float maxWait;

        bool waiting;
        float waitUntil;

        public WanderAction(string name, ZombieMotor motor, WanderZone zone, float speed, float minWait = 1f, float maxWait = 3f)
            : base(name)
        {
            this.motor = motor;
            this.zone = zone;
            this.speed = speed;
            this.minWait = minWait;
            this.maxWait = maxWait;
        }

        protected override void OnStart()
        {
            motor.SetSpeed(speed);
            PickNewDestination();
        }

        protected override NodeStatus OnUpdate()
        {
            if (waiting)
            {
                if (Time.time >= waitUntil)
                    PickNewDestination();
            }
            else if (motor.IsStuck)
            {
                PickNewDestination(); // recover: give up on this point, try another
            }
            else if (motor.HasArrived)
            {
                motor.Stop();
                Wait(Random.Range(minWait, maxWait));
            }

            return NodeStatus.Running;
        }

        void PickNewDestination()
        {
            waiting = false;
            for (int i = 0; i < MaxPickAttempts; i++)
            {
                // Zone gives a point on the NavMesh inside the circle; motor rejects it if the path isn't complete.
                if (zone.TryGetRandomPoint(out Vector3 point) && motor.MoveTo(point))
                    return;
            }

            motor.Stop();
            Wait(RetryDelay); // nothing reachable right now — try again shortly
        }

        void Wait(float seconds)
        {
            waiting = true;
            waitUntil = Time.time + seconds;
        }
    }
}
