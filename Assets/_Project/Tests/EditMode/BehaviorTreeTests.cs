using NUnit.Framework;
using ZombieStealth.AI.BehaviorTree;

namespace ZombieStealth.Tests
{
    public class BehaviorTreeTests
    {
        /// <summary>Test action: returns whatever <see cref="Result"/> is and counts its lifecycle calls.</summary>
        class StubAction : ActionNode
        {
            public NodeStatus Result;
            public int Starts, Updates, Stops;

            public StubAction(string name, NodeStatus result) : base(name) { Result = result; }

            protected override void OnStart() => Starts++;
            protected override NodeStatus OnUpdate() { Updates++; return Result; }
            protected override void OnStop() => Stops++;
        }

        static StubAction Stub(NodeStatus result) => new StubAction(result.ToString(), result);

        // ---------- Condition ----------

        [Test]
        public void Condition_MapsTrueToSuccess_AndFalseToFailure()
        {
            Assert.AreEqual(NodeStatus.Success, new Condition("yes", () => true).Tick());
            Assert.AreEqual(NodeStatus.Failure, new Condition("no", () => false).Tick());
        }

        // ---------- Selector ----------

        [Test]
        public void Selector_ReturnsFirstNonFailure_AndSkipsTheRest()
        {
            var a = Stub(NodeStatus.Failure);
            var b = Stub(NodeStatus.Success);
            var c = Stub(NodeStatus.Success);

            Assert.AreEqual(NodeStatus.Success, new Selector("sel", a, b, c).Tick());
            Assert.AreEqual(1, a.Updates);
            Assert.AreEqual(1, b.Updates);
            Assert.AreEqual(0, c.Updates);
        }

        [Test]
        public void Selector_ReturnsRunning_WhenChildIsRunning()
        {
            var running = Stub(NodeStatus.Running);
            var after = Stub(NodeStatus.Success);

            Assert.AreEqual(NodeStatus.Running, new Selector("sel", Stub(NodeStatus.Failure), running, after).Tick());
            Assert.AreEqual(0, after.Updates);
        }

        [Test]
        public void Selector_ReturnsFailure_WhenAllChildrenFail()
        {
            Assert.AreEqual(NodeStatus.Failure, new Selector("sel", Stub(NodeStatus.Failure), Stub(NodeStatus.Failure)).Tick());
        }

        // ---------- Sequence ----------

        [Test]
        public void Sequence_ReturnsSuccess_WhenAllChildrenSucceed()
        {
            var a = Stub(NodeStatus.Success);
            var b = Stub(NodeStatus.Success);

            Assert.AreEqual(NodeStatus.Success, new Sequence("seq", a, b).Tick());
            Assert.AreEqual(1, b.Updates);
        }

        [Test]
        public void Sequence_StopsAtFirstFailure()
        {
            var after = Stub(NodeStatus.Success);

            Assert.AreEqual(NodeStatus.Failure, new Sequence("seq", Stub(NodeStatus.Success), Stub(NodeStatus.Failure), after).Tick());
            Assert.AreEqual(0, after.Updates);
        }

        [Test]
        public void Sequence_StopsAtRunningChild()
        {
            var after = Stub(NodeStatus.Success);

            Assert.AreEqual(NodeStatus.Running, new Sequence("seq", Stub(NodeStatus.Running), after).Tick());
            Assert.AreEqual(0, after.Updates);
        }

        // ---------- ActionNode lifecycle ----------

        [Test]
        public void Action_StartsOnce_WhileRunning_AndStopsWhenFinished()
        {
            var action = Stub(NodeStatus.Running);

            action.Tick();
            action.Tick();
            action.Tick();
            Assert.AreEqual(1, action.Starts);
            Assert.AreEqual(3, action.Updates);
            Assert.AreEqual(0, action.Stops);

            action.Result = NodeStatus.Success;
            action.Tick();
            Assert.AreEqual(1, action.Stops);

            // Ticking again after it finished starts a fresh run.
            action.Result = NodeStatus.Running;
            action.Tick();
            Assert.AreEqual(2, action.Starts);
        }

        [Test]
        public void Action_Abort_StopsOnlyIfStarted()
        {
            var action = Stub(NodeStatus.Running);

            action.Abort();
            Assert.AreEqual(0, action.Stops, "never started, so nothing to stop");

            action.Tick();
            action.Abort();
            Assert.AreEqual(1, action.Stops);
            Assert.IsFalse(action.IsRunning);
        }

        // ---------- Priority / interruption (the core idea of the zombie tree) ----------

        [Test]
        public void HigherPriorityBranch_InterruptsRunningLowerBranch()
        {
            bool seesPlayer = false;
            var chase = Stub(NodeStatus.Running);
            var wander = Stub(NodeStatus.Running);

            var root = new Selector("ROOT",
                new Sequence("CHASE", new Condition("CanSeePlayer", () => seesPlayer), chase),
                wander);

            root.Tick();
            Assert.AreEqual(1, wander.Starts, "nothing seen → wander");
            Assert.AreEqual(0, chase.Starts);

            seesPlayer = true;
            root.Tick();
            Assert.AreEqual(1, chase.Starts, "player seen → chase starts");
            Assert.AreEqual(1, wander.Stops, "wander was interrupted");
            Assert.IsFalse(wander.IsRunning);

            seesPlayer = false;
            root.Tick();
            Assert.AreEqual(1, chase.Stops, "condition failed → chase interrupted");
            Assert.AreEqual(2, wander.Starts, "wander restarts from scratch");
        }

        /// <summary>
        /// Documents an ordering rule the zombie actions depend on: when a higher-priority branch
        /// takes over, the new action's OnStart runs BEFORE the old action's OnStop. So OnStop must
        /// not undo shared state (this caused the "stuck in INVESTIGATE" bug: Wander.OnStop stopped
        /// the motor right after Investigate had started moving).
        /// </summary>
        [Test]
        public void HigherPriorityBranch_StartsBeforeInterruptedBranchStops()
        {
            var log = new System.Collections.Generic.List<string>();
            bool heardNoise = false;
            var investigate = new LoggingAction("Investigate", log);
            var wander = new LoggingAction("Wander", log);

            var root = new Selector("ROOT",
                new Sequence("INVESTIGATE", new Condition("HasHeardNoise", () => heardNoise), investigate),
                wander);

            root.Tick();
            log.Clear();

            heardNoise = true;
            root.Tick();
            CollectionAssert.AreEqual(new[] { "Investigate.OnStart", "Wander.OnStop" }, log);
        }

        class LoggingAction : ActionNode
        {
            readonly System.Collections.Generic.List<string> log;
            public LoggingAction(string name, System.Collections.Generic.List<string> log) : base(name) { this.log = log; }
            protected override void OnStart() => log.Add(Name + ".OnStart");
            protected override NodeStatus OnUpdate() => NodeStatus.Running;
            protected override void OnStop() => log.Add(Name + ".OnStop");
        }

        [Test]
        public void ZombieShapedTree_PicksBranchByPriority()
        {
            bool canSee = false, hasLastKnown = false, heardNoise = false;
            var chase = Stub(NodeStatus.Running);
            var search = Stub(NodeStatus.Running);
            var investigate = Stub(NodeStatus.Running);
            var wander = Stub(NodeStatus.Running);

            var root = new Selector("ROOT",
                new Sequence("CHASE", new Condition("CanSeePlayer", () => canSee), chase),
                new Sequence("SEARCH", new Condition("HasLastKnownPosition", () => hasLastKnown), search),
                new Sequence("INVESTIGATE", new Condition("HasHeardNoise", () => heardNoise), investigate),
                wander);

            root.Tick();
            Assert.IsTrue(wander.IsRunning);

            heardNoise = true;
            root.Tick();
            Assert.IsTrue(investigate.IsRunning);
            Assert.IsFalse(wander.IsRunning);

            hasLastKnown = true; // memory beats noise
            root.Tick();
            Assert.IsTrue(search.IsRunning);
            Assert.IsFalse(investigate.IsRunning);

            canSee = true; // sight beats everything
            root.Tick();
            Assert.IsTrue(chase.IsRunning);
            Assert.IsFalse(search.IsRunning);
        }
    }
}
