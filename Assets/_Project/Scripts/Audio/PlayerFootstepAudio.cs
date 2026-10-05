using UnityEngine;
using ZombieStealth.Gameplay;
using ZombieStealth.Player;

namespace ZombieStealth.Audio
{
    /// <summary>
    /// Plays the player's footstep sounds on exactly the steps PlayerNoise takes, so what you hear matches
    /// what the zombies can hear: loud when sprinting, normal walking, very soft when crouching.
    /// PlayerNoise tells us which surface each step was on (forest, grass, gravel, dirt) and we pick that
    /// surface's clips. Feedback only — the AI noise itself is still emitted by PlayerNoise.
    /// </summary>
    [RequireComponent(typeof(PlayerNoise), typeof(AudioSource))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        [Header("Clips per surface (picked at random; Dirt is also the fallback)")]
        [SerializeField] AudioClip[] dirtSteps;
        [SerializeField] AudioClip[] grassSteps;
        [SerializeField] AudioClip[] forestSteps;
        [SerializeField] AudioClip[] gravelSteps;

        [Header("Volume per movement type")]
        [SerializeField, Range(0f, 1f)] float crouchVolume = 0.12f;
        [SerializeField, Range(0f, 1f)] float walkVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] float sprintVolume = 0.75f;
        [SerializeField] Vector2 pitchRange = new Vector2(0.9f, 1.1f);

        PlayerNoise noise;
        AudioSource source;
        AudioClip lastClip;

        /// <summary>Surface of the last step (handy to check in the Inspector / debugger).</summary>
        public SurfaceType CurrentSurface { get; private set; }

        void Awake()
        {
            noise = GetComponent<PlayerNoise>();
            source = GetComponent<AudioSource>();
        }

        void OnEnable() => noise.Stepped += OnStepped;
        void OnDisable() => noise.Stepped -= OnStepped;

        void OnStepped(PlayerNoise.StepKind kind, SurfaceType surface)
        {
            CurrentSurface = surface;
            AudioClip[] clips = ClipsFor(CurrentSurface);
            if (clips == null || clips.Length == 0)
                clips = dirtSteps;
            if (clips == null || clips.Length == 0)
                return;

            // Never the same clip twice in a row → less "machine gun" repetition.
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clips.Length > 1 && clip == lastClip)
                clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];
            lastClip = clip;
            if (clip == null)
                return;

            float volume = kind == PlayerNoise.StepKind.Sprint ? sprintVolume
                         : kind == PlayerNoise.StepKind.Crouch ? crouchVolume
                         : walkVolume;
            source.pitch = Random.Range(pitchRange.x, pitchRange.y) * (kind == PlayerNoise.StepKind.Sprint ? 1.08f : 1f);
            source.PlayOneShot(clip, volume);
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
