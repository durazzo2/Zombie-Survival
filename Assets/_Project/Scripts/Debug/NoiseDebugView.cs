using System.Collections.Generic;
using UnityEngine;
using ZombieStealth.AI.Perception;

namespace ZombieStealth.Debugging
{
    /// <summary>Shows every emitted noise as a fading circle of its radius for a moment.</summary>
    public class NoiseDebugView : MonoBehaviour
    {
        [SerializeField] float showDuration = 1f;

#if UNITY_EDITOR

        readonly List<NoiseEvent> recentNoises = new List<NoiseEvent>();

        void OnEnable() => NoiseSystem.NoiseMade += OnNoiseMade;
        void OnDisable() => NoiseSystem.NoiseMade -= OnNoiseMade;

        void OnNoiseMade(NoiseEvent noise)
        {
            recentNoises.RemoveAll(IsExpired);
            recentNoises.Add(noise);
        }

        bool IsExpired(NoiseEvent noise) => Time.time - noise.TimeStamp > showDuration;

        void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            recentNoises.RemoveAll(IsExpired);
            foreach (NoiseEvent noise in recentNoises)
            {
                float age = (Time.time - noise.TimeStamp) / showDuration; // 0 → 1
                Color color = noise.Type == NoiseType.Distraction ? Color.yellow : new Color(0.4f, 0.75f, 1f);
                color.a = 1f - age;

                Vector3 centre = noise.Position + Vector3.up * 0.05f;
                UnityEditor.Handles.color = color;
                UnityEditor.Handles.DrawWireDisc(centre, Vector3.up, noise.Radius);          // how far it carries
                UnityEditor.Handles.DrawWireDisc(centre, Vector3.up, noise.Radius * age);    // expanding "ripple"
            }
        }
#endif
    }
}
