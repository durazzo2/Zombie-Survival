using UnityEngine;

namespace ZombieStealth.AI.Zombie
{
    /// <summary>
    /// What the zombie remembers (its "blackboard"). Plain data — it doesn't sense or decide anything.
    /// Written by ZombieBrain from perception each tick; read by Behavior Tree conditions/actions.
    ///
    /// Rule: newer information wins. Memory holds at most one clue about the player —
    /// either where they were last seen OR the latest noise — whichever came last.
    /// </summary>
    public class ZombieMemory
    {
        /// <summary>True once the player has been seen at least once (until something forgets it).</summary>
        public bool HasLastKnownPosition { get; private set; }

        /// <summary>Where the player was the last time the zombie could see them (feet position).</summary>
        public Vector3 LastKnownPosition { get; private set; }

        /// <summary>Time.time when LastKnownPosition was last updated.</summary>
        public float LastKnownTime { get; private set; } = float.NegativeInfinity;

        public float SecondsSinceLastKnown => Time.time - LastKnownTime;

        // ---- Noise ----
        public bool HasHeardNoise { get; private set; }
        public Vector3 NoisePosition { get; private set; }
        public float NoiseTime { get; private set; } = float.NegativeInfinity;
        public float SecondsSinceNoise => Time.time - NoiseTime;

        public void RememberPlayerAt(Vector3 position)
        {
            HasLastKnownPosition = true;
            LastKnownPosition = position;
            LastKnownTime = Time.time;
            HasHeardNoise = false; // seeing the player beats any older sound
        }

        public void HearNoise(Vector3 position, float time)
        {
            HasHeardNoise = true;
            NoisePosition = position;
            NoiseTime = time;
            HasLastKnownPosition = false; // the noise is newer than the old sighting, so stop searching there
        }

        public void ForgetNoise()
        {
            HasHeardNoise = false;
        }

        /// <summary>Called when a search is finished: the zombie gives up on the player.</summary>
        public void ForgetPlayer()
        {
            HasLastKnownPosition = false;
        }
    }
}
