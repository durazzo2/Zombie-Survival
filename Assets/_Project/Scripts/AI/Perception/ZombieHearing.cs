using UnityEngine;

namespace ZombieStealth.AI.Perception
{
    /// <summary>
    /// Listens to the NoiseSystem and keeps the newest noise this zombie was close enough to hear.
    /// Pure perception — ZombieBrain takes the heard noise each tick and decides what to do with it.
    /// No occlusion: walls and rocks don't block sound.
    /// </summary>
    public class ZombieHearing : MonoBehaviour
    {
        NoiseEvent? unhandledNoise;

        /// <summary>Most recent noise heard (for the debug view).</summary>
        public NoiseEvent? LastHeardNoise { get; private set; }

        void OnEnable() => NoiseSystem.NoiseMade += OnNoiseMade;
        void OnDisable() => NoiseSystem.NoiseMade -= OnNoiseMade;

        void OnNoiseMade(NoiseEvent noise)
        {
            // Heard if we're inside the noise's radius.
            if (Vector3.Distance(transform.position, noise.Position) > noise.Radius)
                return;

            unhandledNoise = noise; // newest one wins
            LastHeardNoise = noise;
        }

        /// <summary>Returns the newest noise heard since the last call (once), if any.</summary>
        public bool TryTakeHeardNoise(out NoiseEvent noise)
        {
            noise = unhandledNoise.GetValueOrDefault();
            bool heard = unhandledNoise.HasValue;
            unhandledNoise = null;
            return heard;
        }
    }
}
