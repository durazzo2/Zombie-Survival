using UnityEngine;
using ZombieStealth.AI.Perception;

namespace ZombieStealth.Gameplay
{
    /// <summary>
    /// A thrown rock. On its first hard impact it emits one Distraction noise through the NoiseSystem
    /// (it knows nothing about zombies), then it's just a physics object until it disappears.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class Throwable : MonoBehaviour
    {
        [SerializeField] float noiseRadius = 15f;
        [Tooltip("Impacts slower than this (m/s) are ignored, e.g. gentle rolling.")]
        [SerializeField] float minImpactSpeed = 2f;
        [SerializeField] float lifetime = 9f;

        bool hasMadeNoise;

        public void Launch(Vector3 velocity, Collider thrower)
        {
            Physics.IgnoreCollision(GetComponent<Collider>(), thrower); // don't hit the player who threw it
            GetComponent<Rigidbody>().linearVelocity = velocity;
            Destroy(gameObject, lifetime);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (hasMadeNoise || collision.relativeVelocity.magnitude < minImpactSpeed)
                return;

            hasMadeNoise = true; // only the first real impact counts, not every bounce
            NoiseSystem.Emit(collision.GetContact(0).point, noiseRadius, NoiseType.Distraction);
        }
    }
}
