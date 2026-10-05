using ZombieStealth.AI.BehaviorTree;
using ZombieStealth.AI.Perception;

namespace ZombieStealth.AI.Zombie.Actions
{
    /// <summary>
    /// Run toward the player, re-targeting every tick. Always Running — the CHASE branch ends
    /// when its CanSeePlayer condition fails (after the vision grace period), which aborts this action.
    /// </summary>
    public class ChasePlayerAction : ActionNode
    {
        readonly ZombieMotor motor;
        readonly ZombieVision vision;
        readonly float runSpeed;

        public ChasePlayerAction(string name, ZombieMotor motor, ZombieVision vision, float runSpeed) : base(name)
        {
            this.motor = motor;
            this.vision = vision;
            this.runSpeed = runSpeed;
        }

        protected override void OnStart() => motor.SetSpeed(runSpeed);

        protected override NodeStatus OnUpdate()
        {
            // If the player is somewhere unreachable, MoveTo fails and we keep the previous path.
            motor.MoveTo(vision.PlayerPosition);
            return NodeStatus.Running;
        }
    }
}
