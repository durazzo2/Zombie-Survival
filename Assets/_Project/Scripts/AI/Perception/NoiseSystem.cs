using System;
using UnityEngine;

namespace ZombieStealth.AI.Perception
{
    public enum NoiseType
    {
        Footstep,
        Distraction   // thrown rock hitting something
    }

    /// <summary>One sound in the world: where it happened and how far it carries.</summary>
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly NoiseType Type;
        public readonly float TimeStamp;

        public NoiseEvent(Vector3 position, float radius, NoiseType type, float timeStamp)
        {
            Position = position;
            Radius = radius;
            Type = type;
            TimeStamp = timeStamp;
        }
    }

    /// <summary>
    /// Global "radio" for sounds. Anything can Emit a noise; every listener (ZombieHearing,
    /// debug view) gets the event and decides for itself whether it matters.
    /// </summary>
    public static class NoiseSystem
    {
        public static event Action<NoiseEvent> NoiseMade;

        public static void Emit(Vector3 position, float radius, NoiseType type)
        {
            NoiseMade?.Invoke(new NoiseEvent(position, radius, type, Time.time));
        }
    }
}
