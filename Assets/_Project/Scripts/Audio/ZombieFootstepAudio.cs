using UnityEngine;
using UnityEngine.AI;
using ZombieStealth.Gameplay;

namespace ZombieStealth.Audio
{
    /// <summary>
    /// Heavy, positional footsteps for one zombie, timed from how fast it is actually moving:
    /// slow thuds while shambling, quick ones while chasing. Lives on a child object with its own
    /// AudioSource, so it never interferes with the zombie's alert cues. Feedback only — zombies'
    /// steps make no AI noise.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ZombieFootstepAudio : MonoBehaviour
    {
        [Header("Clips per surface (picked at random; Dirt is also the fallback)")]
        [SerializeField] AudioClip[] dirtSteps;
        [SerializeField] AudioClip[] grassSteps;
        [SerializeField] AudioClip[] forestSteps;
        [SerializeField] AudioClip[] gravelSteps;

        [SerializeField, Range(0f, 1f)] float walkVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] float runVolume = 0.75f;
        [Tooltip("Lower than the player's → sounds heavier.")]
        [SerializeField] Vector2 pitchRange = new Vector2(0.68f, 0.8f);

        [Header("Step timing")]
        [Tooltip("Metres per step. Seconds between steps = stride / speed (clamped below).")]
        [SerializeField] float stride = 0.8f;
        [SerializeField] float minStepInterval = 0.28f;
        [SerializeField] float maxStepInterval = 0.8f;
        [SerializeField] float minMovingSpeed = 0.3f;
        [Tooltip("Above this speed the zombie counts as running (louder steps).")]
        [SerializeField] float runSpeedThreshold = 3f;

        NavMeshAgent agent;
        AudioSource source;
        float stepTimer;

        void Awake()
        {
            agent = GetComponentInParent<NavMeshAgent>();
            source = GetComponent<AudioSource>();
        }

        void Update()
        {
            Vector3 velocity = agent.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            if (speed < minMovingSpeed)
            {
                stepTimer = 0f; // first step comes right after it starts moving
                return;
            }

            stepTimer -= Time.deltaTime;
            if (stepTimer > 0f)
                return;
            stepTimer = Mathf.Clamp(stride / speed, minStepInterval, maxStepInterval);

            Vector3 feet = agent.transform.position - Vector3.up * agent.baseOffset;
            AudioClip[] clips = ClipsFor(FootstepSurface.Detect(feet));
            if (clips == null || clips.Length == 0)
                clips = dirtSteps;
            if (clips == null || clips.Length == 0)
                return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
                return;
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.PlayOneShot(clip, speed >= runSpeedThreshold ? runVolume : walkVolume);
        }

        AudioClip[] ClipsFor(SurfaceType surface) => surface switch
        {
            SurfaceType.Grass => grassSteps,
            SurfaceType.Forest => forestSteps,
            SurfaceType.Gravel => gravelSteps,
            _ => dirtSteps,
        };
    }
}
