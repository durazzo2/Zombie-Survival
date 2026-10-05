using System.Collections.Generic;

namespace ZombieStealth.AI.BehaviorTree
{
    /// <summary>A node with an ordered list of children (base of Selector and Sequence).</summary>
    public abstract class Composite : Node
    {
        protected readonly List<Node> Children;

        public IReadOnlyList<Node> ChildNodes => Children;

        protected Composite(string name, params Node[] children) : base(name)
        {
            Children = new List<Node>(children);
        }

        /// <summary>
        /// Children after <paramref name="index"/> were not ticked this time. If one of them was
        /// Running last tick, it has just been interrupted, so abort it.
        /// </summary>
        protected void AbortRunningChildrenAfter(int index)
        {
            for (int i = index + 1; i < Children.Count; i++)
            {
                if (Children[i].IsRunning)
                    Children[i].Abort();
            }
        }

        public override void Abort()
        {
            AbortRunningChildrenAfter(-1);
            base.Abort();
        }
    }
}
