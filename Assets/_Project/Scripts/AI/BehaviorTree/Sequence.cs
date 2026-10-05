namespace ZombieStealth.AI.BehaviorTree
{
    /// <summary>
    /// "AND" node: runs children from first to last and stops at the first one that is not Success.
    /// Typical use: [Condition, Action, Action] — the actions only run while the condition holds.
    /// </summary>
    public class Sequence : Composite
    {
        public Sequence(string name, params Node[] children) : base(name, children) { }

        protected override NodeStatus OnTick()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                NodeStatus status = Children[i].Tick();
                if (status != NodeStatus.Success)
                {
                    AbortRunningChildrenAfter(i);
                    return status;
                }
            }
            return NodeStatus.Success;
        }
    }
}
