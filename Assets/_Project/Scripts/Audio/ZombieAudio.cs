using UnityEngine;
using ZombieStealth.AI.Perception;
using ZombieStealth.AI.Zombie;

namespace ZombieStealth.Audio
{
    /// <summary>
    /// 3D sound feedback for one zombie. It only OBSERVES the AI (vision + current BT state) and plays a
    /// cue when something changes — it never influences behaviour.
    ///   not seeing → seeing the player : "spotted"
    ///   state becomes CHASE            : "chase" (slightly after "spotted", so they don't mask each other)
    ///   state becomes INVESTIGATE      : "investigate"
    ///   state becomes SEARCH           : "search"
    ///   while WANDER                   : an occasional quiet groan (long random cooldown)
    /// Every clip is optional: an empty slot is simply skipped.
    /// </summary>
    [RequireComponent(typeof(ZombieBrain), typeof(ZombieVision), typeof(AudioSource))]
    public class ZombieAudio : MonoBehaviour
    {
        [Header("Clips (any can be left empty)")]
        [SerializeField] AudioClip spotted;
        [SerializeField] AudioClip chase;
        [SerializeField] AudioClip investigate;
        [SerializeField] AudioClip search;
        [SerializeField] AudioClip[] wanderGroans;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] float cueVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] float groanVolume = 0.35f;

        [Header("Anti-spam")]
        [Tooltip("Minimum seconds between two state cues of this zombie (rapid flips are skipped).")]
        [SerializeField] float minSecondsBetweenCues = 0.6f;
        [Tooltip("The 'spotted' cue won't repeat within this many seconds.")]
        [SerializeField] float spottedCooldown = 3f;
        [SerializeField] float chaseCueDelay = 0.25f;
        [SerializeField] Vector2 groanIntervalSeconds = new Vector2(8f, 20f);

        ZombieBrain brain;
        ZombieVision vision;
        AudioSource source;

        string previousState = "-";
        bool couldSeePlayer;
        float lastStateCueTime = float.NegativeInfinity;
        float lastSpottedTime = float.NegativeInfinity;
        bool chaseCuePending;
        float chaseCueTime;
        float nextGroanTime;

        /// <summary>Last cue played (for the F1 HUD).</summary>
        public string LastCue { get; private set; } = "-";

        void Awake()
        {
            brain = GetComponent<ZombieBrain>();
            vision = GetComponent<ZombieVision>();
            source = GetComponent<AudioSource>();
            nextGroanTime = Time.time + Random.Range(groanIntervalSeconds.x, groanIntervalSeconds.y);
        }

        // LateUpdate: runs after ZombieBrain.Update has made this frame's decision.
        void LateUpdate()
        {
            // 1. Spotted: the moment vision goes from "can't see" to "can see". (CanSeePlayer includes the
            //    0.4 s grace period, so flickering around a tree trunk doesn't retrigger it.)
            bool canSeePlayer = vision.CanSeePlayer;
            if (canSeePlayer && !couldSeePlayer && Time.time - lastSpottedTime >= spottedCooldown)
            {
                lastSpottedTime = Time.time;
                Play(spotted, cueVolume, "spotted");
            }
            couldSeePlayer = canSeePlayer;

            // 2. Behaviour state changed? Only then play a state cue (never every tick).
            string state = brain.CurrentState;
            if (state != previousState)
            {
                OnStateChanged(state);
                previousState = state;
            }

            if (chaseCuePending && Time.time >= chaseCueTime)
            {
                chaseCuePending = false;
                Play(chase, cueVolume, "chase");
            }

            // 3. Ambient groan, only while wandering, rarely.
            if (state == "WANDER" && Time.time >= nextGroanTime)
            {
                nextGroanTime = Time.time + Random.Range(groanIntervalSeconds.x, groanIntervalSeconds.y);
                if (wanderGroans != null && wanderGroans.Length > 0)
                    Play(wanderGroans[Random.Range(0, wanderGroans.Length)], groanVolume, "groan");
            }
        }

        void OnStateChanged(string state)
        {
            if (state != "CHASE")
                chaseCuePending = false; // chase ended before its roar played

            if (Time.time - lastStateCueTime < minSecondsBetweenCues)
                return;

            switch (state)
            {
                case "CHASE":
                    chaseCuePending = true;
                    chaseCueTime = Time.time + chaseCueDelay;
                    lastStateCueTime = Time.time;
                    break;
                case "INVESTIGATE":
                    lastStateCueTime = Time.time;
                    Play(investigate, cueVolume * 0.8f, "investigate");
                    break;
                case "SEARCH":
                    lastStateCueTime = Time.time;
                    Play(search, cueVolume * 0.9f, "search");
                    break;
                // WANDER and CATCH: no zombie cue (CATCH is covered by GameAudio's "caught" sound).
            }
        }

        void Play(AudioClip clip, float volume, string label)
        {
            if (clip == null)
            {
                LastCue = label + " (no clip)";
                return;
            }
            source.PlayOneShot(clip, volume);
            LastCue = label;
        }
    }
}
