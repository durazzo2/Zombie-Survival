using UnityEngine;

namespace ZombieStealth.Gameplay
{
    public enum SurfaceType
    {
        Dirt,
        Grass,
        Forest,   // leaves and twigs
        Gravel,
    }

    /// <summary>
    /// Tag on ground colliders (and trails) saying what kind of ground it is.
    /// Used for footstep sounds AND for how far footstep noise carries (PlayerNoise).
    /// </summary>
    public class FootstepSurface : MonoBehaviour
    {
        public SurfaceType surface = SurfaceType.Dirt;

        /// <summary>What's directly under these feet? Untagged ground counts as Dirt.</summary>
        public static SurfaceType Detect(Vector3 feetPosition)
        {
            // Start a little above the feet (inside the walker's own collider, which rays ignore) and look 1 m down.
            // Triggers count, because dirt trails are thin trigger strips lying on top of the ground.
            if (Physics.Raycast(feetPosition + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 1f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                && hit.collider.GetComponentInParent<FootstepSurface>() is FootstepSurface tagged)
                return tagged.surface;
            return SurfaceType.Dirt;
        }
    }
}
