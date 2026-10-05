using ZombieStealth.AI.BehaviorTree;
using ZombieStealth.Gameplay;

namespace ZombieStealth.AI.Zombie.Actions
{
    /// <summary>Stop and tell the GameManager the player was caught. Stays Running so the state reads CATCH.</summary>
    public class CatchPlayerAction : ActionNode
    {
        readonly ZombieMotor motor;
        readonly GameManager gameManager;

        public CatchPlayerAction(string name, ZombieMotor motor, GameManager gameManager) : base(name)
        {
            this.motor = motor;
            this.gameManager = gameManager;
        }

        protected override void OnStart()
        {
            motor.Stop();
            gameManager.PlayerCaught();
        }

        protected override NodeStatus OnUpdate() => NodeStatus.Running;
    }
}
