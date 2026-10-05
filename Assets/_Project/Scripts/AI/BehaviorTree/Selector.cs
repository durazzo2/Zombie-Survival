namespace ZombieStealth.AI.BehaviorTree
{
    /// <summary>
    /// "OR" node / priority list: tries children from first to last and returns the first
    /// result that is not Failure. Earlier children have higher priority.
    /// </summary>
    public class Selector : Composite
    {
        public Selector(string name, params Node[] children) : base(name, children) { }

        protected override NodeStatus OnTick()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                NodeStatus status = Children[i].Tick();
                if (status != NodeStatus.Failure)
                {
                    AbortRunningChildrenAfter(i); // a higher-priority branch took over
                    return status;
                }
            }
            return NodeStatus.Failure;
        }
    }
}
