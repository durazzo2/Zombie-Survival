using System;

namespace ZombieStealth.AI.BehaviorTree
{
    /// <summary>Leaf that asks a yes/no question: true → Success, false → Failure. Never Running.</summary>
    public class Condition : Node
    {
        readonly Func<bool> check;

        public Condition(string name, Func<bool> check) : base(name)
        {
            this.check = check;
        }

        protected override NodeStatus OnTick() => check() ? NodeStatus.Success : NodeStatus.Failure;
    }
}
