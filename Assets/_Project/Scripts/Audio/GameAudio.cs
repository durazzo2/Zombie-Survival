using UnityEngine;
using ZombieStealth.AI.Zombie;
using ZombieStealth.Gameplay;

namespace ZombieStealth.Audio
{
    /// <summary>
    /// Global (2D) game-state audio. Observes the GameManager and all zombies; never changes them.
    ///   - one "caught" or "extracted" sound when the run ends
    ///   - one shared chase tension loop that fades in while ANY zombie is chasing and fades out when none is
    /// Every clip is optional.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class GameAudio : MonoBehaviour
    {
        [Header("Clips (any can be left empty)")]
        [SerializeField] AudioClip caught;
        [SerializeField] AudioClip extracted;
        [SerializeField] AudioClip chaseLoop;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] float resultVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] float chaseLoopVolume = 0.35f;
        [SerializeField] float chaseFadeSeconds = 1.5f;

        AudioSource oneShots;
        AudioSource loopSource;
        GameManager gameManager;
        ZombieBrain[] zombies;
        bool resultPlayed;

        public bool ChaseLoopActive { get; private set; }

        void Awake()
        {
            oneShots = GetComponent<AudioSource>();

            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.loop = true;
            loopSource.playOnAwake = false;
            loopSource.spatialBlend = 0f;
            loopSource.volume = 0f;
        }

        void Start()
        {
            gameManager = FindAnyObjectByType<GameManager>();
            zombies = FindObjectsByType<ZombieBrain>();
        }

        void Update()
        {
            if (!resultPlayed && gameManager != null && gameManager.IsGameOver)
            {
                resultPlayed = true;
                AudioClip clip = gameManager.PlayerWon ? extracted : caught;
                if (clip != null)
                    oneShots.PlayOneShot(clip, resultVolume);
            }

            ChaseLoopActive = !resultPlayed && AnyZombieChasing();
            UpdateChaseLoop();
        }

        bool AnyZombieChasing()
        {
            foreach (ZombieBrain zombie in zombies)
            {
                if (zombie.CurrentState == "CHASE" || zombie.CurrentState == "CATCH")
                    return true;
            }
            return false;
        }

        void UpdateChaseLoop()
        {
            if (chaseLoop == null)
                return;

            if (ChaseLoopActive && !loopSource.isPlaying)
            {
                loopSource.clip = chaseLoop;
                loopSource.Play();
            }

            // Unscaled time: keeps fading out while the game is frozen on CAUGHT/EXTRACTED.
            float target = ChaseLoopActive ? chaseLoopVolume : 0f;
            loopSource.volume = Mathf.MoveTowards(loopSource.volume, target, chaseLoopVolume / chaseFadeSeconds * Time.unscaledDeltaTime);

            if (!ChaseLoopActive && loopSource.isPlaying && loopSource.volume <= 0f)
                loopSource.Stop();
        }
    }
}
