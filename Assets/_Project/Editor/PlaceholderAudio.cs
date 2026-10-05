using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombieStealth.Editor
{
    /// <summary>
    /// Makes simple synthesized placeholder sounds (WAV files in Assets/_Project/Audio) so every audio cue
    /// can be heard before real sound effects exist. A file is only generated if it's missing — replace any
    /// of them with a real sound of the same name and rebuilds will keep yours.
    /// </summary>
    public static class PlaceholderAudio
    {
        const string Folder = "Assets/_Project/Audio";
        const int SampleRate = 22050;

        public static AudioClip Get(string name)
        {
            string path = $"{Folder}/{name}.wav";
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project", "Audio");

            WriteWav(path, Normalize(Synthesize(name)));
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static float[] Synthesize(string name)
        {
            int seed = 17;
            foreach (char c in name)
                seed = seed * 31 + c;                                    // stable seed per sound name
            var random = new System.Random(seed);
            float Noise() => (float)(random.NextDouble() * 2.0 - 1.0);

            if (name.StartsWith("StepGrass") || name.StartsWith("StepForest") || name.StartsWith("StepGravel"))
                return SurfaceStep(name, random);

            switch (name)
            {
                case "Spotted": // sharp two-tone alert "ping-PING"
                    return Render(0.45f, t => 0.6f * Ping(t, 0f, 1320f) + 0.7f * Ping(t, 0.11f, 1760f));

                case "Investigate": // low, questioning growl (pitch rises a little: "hm?")
                {
                    float phase = 0f, lowNoise = 0f;
                    return Render(0.9f, t =>
                    {
                        float f = 95f + 30f * (t / 0.9f) + 4f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                        phase += f / SampleRate;
                        lowNoise += 0.2f * (Noise() - lowNoise);
                        return Envelope(t, 0.9f, 0.08f, 0.3f) * (Harmonics(phase) * 0.5f + lowNoise * 0.15f);
                    });
                }

                case "Search": // two confused, falling grunts "huh... huh"
                {
                    float phase = 0f;
                    return Render(1.2f, t =>
                    {
                        float local = t < 0.55f ? t : t - 0.55f;
                        float f = 130f - 45f * (local / 0.45f);
                        phase += f / SampleRate;
                        float tremolo = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                        return local < 0.45f ? Envelope(local, 0.45f, 0.05f, 0.2f) * Harmonics(phase) * 0.55f * tremolo : 0f;
                    });
                }

                case "Chase": // aggressive distorted roar
                {
                    float phase = 0f, lowNoise = 0f;
                    return Render(1.0f, t =>
                    {
                        float f = 75f + 40f * Mathf.Sin(2f * Mathf.PI * 3f * t);
                        phase += f / SampleRate;
                        lowNoise += 0.25f * (Noise() - lowNoise);
                        float raw = Saw(phase) * 0.5f + lowNoise * 0.9f;
                        return Envelope(t, 1.0f, 0.1f, 0.5f) * (float)Math.Tanh(raw * 2.5f);
                    });
                }

                case "Groan1":
                case "Groan2": // slow, low ambient groan
                {
                    float baseFrequency = name == "Groan1" ? 65f : 82f;
                    float phase = 0f, lowNoise = 0f;
                    return Render(1.6f, t =>
                    {
                        float f = baseFrequency + 8f * Mathf.Sin(2f * Mathf.PI * 1.5f * t);
                        phase += f / SampleRate;
                        lowNoise += 0.1f * (Noise() - lowNoise);
                        return Envelope(t, 1.6f, 0.3f, 0.6f) * (Harmonics(phase) * 0.45f + lowNoise * 0.1f);
                    });
                }

                case "Caught": // harsh noise hit + falling saw
                {
                    float phase = 0f;
                    return Render(1.3f, t =>
                    {
                        float f = Mathf.Lerp(380f, 55f, Mathf.Clamp01(t / 1.0f));
                        phase += f / SampleRate;
                        float hit = t < 0.15f ? Noise() * (1f - t / 0.15f) : 0f;
                        return Envelope(t, 1.3f, 0.01f, 0.4f) * (float)Math.Tanh((Saw(phase) * 0.7f + hit) * 2f);
                    });
                }

                case "Footstep1":
                case "Footstep2":
                case "Footstep3": // soft step on dirt/grass: short filtered noise crunch + low thump
                {
                    int variant = name[name.Length - 1] - '1';
                    float smoothing = 0.18f + 0.07f * variant;   // a bit brighter/darker per variant
                    float thumpFrequency = 70f + 12f * variant;
                    float lowNoise = 0f;
                    return Render(0.2f, t =>
                    {
                        lowNoise += smoothing * (Noise() - lowNoise);
                        float crunch = lowNoise * Mathf.Exp(-t * (28f - 4f * variant));
                        float thump = Mathf.Sin(2f * Mathf.PI * thumpFrequency * t) * Mathf.Exp(-t * 40f);
                        return Mathf.Min(t / 0.004f, 1f) * (crunch * 1.4f + thump * 0.6f);
                    });
                }

                case "Extracted": // rising major arpeggio (C E G C)
                    return Render(1.4f, t => 0.5f * (Bell(t, 0f, 523.25f) + Bell(t, 0.15f, 659.25f) + Bell(t, 0.3f, 783.99f) + Bell(t, 0.45f, 1046.5f)));

                case "ChaseLoop": // seamless 2 s loop: low drone + heartbeat (whole cycles fit exactly → no click)
                    return Render(2.0f, t =>
                    {
                        float drone = 0.25f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.1f * Mathf.Sin(2f * Mathf.PI * 110f * t);
                        float beat = t % 0.5f;
                        float thump = Mathf.Exp(-beat * 18f) * Mathf.Sin(2f * Mathf.PI * 50f * beat)
                                    + (beat > 0.18f ? 0.6f * Mathf.Exp(-(beat - 0.18f) * 18f) * Mathf.Sin(2f * Mathf.PI * 50f * (beat - 0.18f)) : 0f);
                        return drone + 0.8f * thump;
                    });

                default:
                    throw new ArgumentException("Unknown placeholder sound: " + name);
            }
        }

        /// <summary>Footsteps for the other surfaces (Dirt uses Footstep1..3). Name ends with the variant 1..3.</summary>
        static float[] SurfaceStep(string name, System.Random random)
        {
            float Noise() => (float)(random.NextDouble() * 2.0 - 1.0);
            int variant = name[name.Length - 1] - '1';
            float lowNoise = 0f;

            if (name.StartsWith("StepGrass")) // soft, airy swish (bright noise = noise minus its low part)
            {
                return Render(0.22f, t =>
                {
                    float n = Noise();
                    lowNoise += 0.15f * (n - lowNoise);
                    float swish = (n - lowNoise) * Mathf.Exp(-t * (16f + 3f * variant));
                    float thump = Mathf.Sin(2f * Mathf.PI * (85f + 10f * variant) * t) * Mathf.Exp(-t * 45f);
                    return Mathf.Min(t / 0.015f, 1f) * (swish * 0.9f + thump * 0.25f);
                });
            }

            if (name.StartsWith("StepForest")) // dry leaves crunch + a few small twig snaps
            {
                var snaps = new float[3 + variant];
                for (int i = 0; i < snaps.Length; i++)
                    snaps[i] = (float)random.NextDouble() * 0.12f;
                return Render(0.25f, t =>
                {
                    lowNoise += 0.35f * (Noise() - lowNoise);
                    float crunch = lowNoise * Mathf.Exp(-t * 14f);
                    float clicks = 0f;
                    foreach (float snap in snaps)
                        if (t >= snap) clicks += Noise() * Mathf.Exp(-(t - snap) * 300f);
                    float thump = Mathf.Sin(2f * Mathf.PI * 75f * t) * Mathf.Exp(-t * 40f);
                    return Mathf.Min(t / 0.004f, 1f) * (crunch * 1.2f + clicks * 0.8f + thump * 0.4f);
                });
            }

            // Gravel: lots of tiny random-loudness grains = crunchy texture.
            float grain = 0f;
            int samplesPerGrain = 60 + 15 * variant; // ~3 ms grains
            int counter = 0;
            return Render(0.3f, t =>
            {
                if (counter++ % samplesPerGrain == 0)
                    grain = Mathf.Pow((float)random.NextDouble(), 2f);
                lowNoise += 0.5f * (Noise() - lowNoise);
                float thump = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 35f);
                return Mathf.Min(t / 0.004f, 1f) * (lowNoise * grain * 1.6f * Mathf.Exp(-t * 9f) + thump * 0.35f);
            });
        }

        // ---- tiny synth helpers (t = seconds)

        static float[] Render(float seconds, Func<float, float> sample)
        {
            var samples = new float[Mathf.RoundToInt(seconds * SampleRate)];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = sample(i / (float)SampleRate);
            return samples;
        }

        static float Ping(float t, float start, float frequency)
        {
            float local = t - start;
            if (local < 0f) return 0f;
            float envelope = Mathf.Min(local / 0.005f, 1f) * Mathf.Exp(-local * 14f);
            return envelope * (Mathf.Sin(2f * Mathf.PI * frequency * local) + 0.3f * Mathf.Sin(4f * Mathf.PI * frequency * local));
        }

        static float Bell(float t, float start, float frequency)
        {
            float local = t - start;
            if (local < 0f) return 0f;
            float envelope = Mathf.Min(local / 0.01f, 1f) * Mathf.Exp(-local * 3f);
            return envelope * (Mathf.Sin(2f * Mathf.PI * frequency * local) + 0.3f * Mathf.Sin(4f * Mathf.PI * frequency * local));
        }

        static float Harmonics(float phase) =>
            0.6f * Mathf.Sin(2f * Mathf.PI * phase) + 0.3f * Mathf.Sin(4f * Mathf.PI * phase) + 0.15f * Mathf.Sin(6f * Mathf.PI * phase);

        static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));

        static float Envelope(float t, float length, float attack, float release)
        {
            float a = Mathf.Clamp01(t / attack);
            float r = Mathf.Clamp01((length - t) / release);
            return Mathf.Min(a, r);
        }

        static float[] Normalize(float[] samples)
        {
            float peak = 0.0001f;
            foreach (float s in samples)
                peak = Mathf.Max(peak, Mathf.Abs(s));
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= 0.9f / peak;
            return samples;
        }

        /// <summary>16-bit mono PCM WAV.</summary>
        static void WriteWav(string path, float[] samples)
        {
            using var writer = new BinaryWriter(File.Create(path));
            int dataSize = samples.Length * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);                 // fmt chunk size
            writer.Write((short)1);           // PCM
            writer.Write((short)1);           // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);     // bytes per second
            writer.Write((short)2);           // block align
            writer.Write((short)16);          // bits per sample
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            foreach (float s in samples)
                writer.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
    }
}
