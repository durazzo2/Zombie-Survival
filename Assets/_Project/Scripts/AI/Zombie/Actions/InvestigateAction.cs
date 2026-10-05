using UnityEngine;
using ZombieStealth.AI.BehaviorTree;

namespace ZombieStealth.AI.Zombie.Actions
{
    /// <summary>
    /// Walk to the remembered noise, look around, then forget the noise → Success.
    /// The noise position is resolved ONCE to a reachable NavMesh point (a rock can land on top of a
    /// boulder); that same point is used for moving and for the arrival check.
    /// If a newer noise is heard on the way, re-target to it ("follow the sounds").
    /// If the noise can't be reached, forget it → Failure, so the tree falls through to the next branch.
    /// (One action with internal steps for the same reason as SearchAction.)
    /// </summary>
    public class InvestigateAction : ActionNode
    {
        public enum Step { GoToNoise, LookAround }

        const float LookTurnSpeed = 90f; // degrees per second

        readonly ZombieMotor motor;
        readonly ZombieMemory memory;
        readonly float speed;
        readonly float lookTime;

        float targetNoiseTime;   // which noise we're currently heading to
        bool unreachable;
        float lookEndTime;

        // ---- For the debug view ----
        public Step CurrentStep { get; private set; }
        /// <summary>Reachable NavMesh point the zombie is actually walking to (null if unreachable).</summary>
        public Vector3? ResolvedTarget { get; private set; }

        public InvestigateAction(string name, ZombieMotor motor, ZombieMemory memory, float speed, float lookTime) : base(name)
        {
            this.motor = motor;
            this.memory = memory;
            this.speed = speed;
            this.lookTime = lookTime;
        }

        protected override void OnStart()
        {
            motor.SetSpeed(speed);
            GoToNoise();
        }

        protected override NodeStatus OnUpdate()
        {
            if (memory.NoiseTime != targetNoiseTime)
                GoToNoise(); // a newer noise was heard

            if (unreachable)
            {
                memory.ForgetNoise();
                return NodeStatus.Failure;
            }

            switch (CurrentStep)
            {
                case Step.GoToNoise:
                    if (motor.HasArrived || motor.IsStuck) // stuck right next to it is close enough
                    {
                        CurrentStep = Step.LookAround;
                        lookEndTime = Time.time + lookTime;
                        motor.LookAround(LookTurnSpeed);
                    }
                    else if (motor.Destination == null)
                    {
                        // Safety net: our movement order was cancelled by something else.
                        // Re-issue it to the SAME resolved point (no re-snapping).
                        motor.MoveTo(ResolvedTarget.Value);
                    }
                    break;

                case Step.LookAround:
                    if (Time.time >= lookEndTime)
                    {
                        memory.ForgetNoise();
                        return NodeStatus.Success;
                    }
                    break;
            }
            return NodeStatus.Running;
        }

        void GoToNoise()
        {
            targetNoiseTime = memory.NoiseTime;
            CurrentStep = Step.GoToNoise;

            // MoveTo snaps the noise onto the NavMesh and rejects incomplete paths.
            // Its Destination IS the resolved point; HasArrived checks against that same point.
            unreachable = !motor.MoveTo(memory.NoisePosition);
            ResolvedTarget = unreachable ? null : motor.Destination;
        }
    }
}
