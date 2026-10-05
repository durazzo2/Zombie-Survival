namespace ZombieStealth.AI.BehaviorTree
{
    public enum NodeStatus
    {
        Success,
        Failure,
        Running
    }

    /// <summary>
    /// Base class of every Behavior Tree node.
    /// The tree is ticked from the root every AI update; each node returns Success, Failure or Running.
    /// </summary>
    public abstract class Node
    {
        public string Name { get; }

        /// <summary>Result of the most recent tick (used by composites and the debug view).</summary>
        public NodeStatus LastStatus { get; private set; } = NodeStatus.Failure;

        public bool IsRunning => LastStatus == NodeStatus.Running;

        protected Node(string name)
        {
            Name = name;
        }

        public NodeStatus Tick()
        {
            LastStatus = OnTick();
            return LastStatus;
        }

        protected abstract NodeStatus OnTick();

        /// <summary>
        /// Called when a Running node is interrupted because a higher-priority branch took over.
        /// Lets actions reset their own state. After this the node is no longer Running.
        /// Note: this happens AFTER the new branch has already started (it had to be ticked first to
        /// know that it wins), so an abort must never undo shared things like the zombie's movement.
        /// </summary>
        public virtual void Abort()
        {
            LastStatus = NodeStatus.Failure;
        }
    }
}
