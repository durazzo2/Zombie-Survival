namespace ZombieStealth.AI.BehaviorTree
{
    /// <summary>
    /// Leaf that does something over time (move, search, look around...).
    /// Lifecycle: OnStart once → OnUpdate every tick while Running → OnStop when it
    /// finishes (Success/Failure) or is aborted by a higher-priority branch.
    /// When aborted, OnStop runs after the new action's OnStart — so OnStop must not stop
    /// the motor (every action gives its own movement command in OnStart instead).
    /// </summary>
    public abstract class ActionNode : Node
    {
        bool started;

        protected ActionNode(string name) : base(name) { }

        protected sealed override NodeStatus OnTick()
        {
            if (!started)
            {
                started = true;
                OnStart();
            }

            NodeStatus status = OnUpdate();
            if (status != NodeStatus.Running)
                Stop();
            return status;
        }

        public override void Abort()
        {
            Stop();
            base.Abort();
        }

        void Stop()
        {
            if (!started)
                return;
            started = false;
            OnStop();
        }

        protected virtual void OnStart() { }
        protected abstract NodeStatus OnUpdate();
        protected virtual void OnStop() { }
    }
}
