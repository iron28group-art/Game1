using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicTree.Climb
{
    /// <summary>
    /// 효과음을 파일 없이 코드로 합성한다 (부드러운 사인파·노이즈 위주의 따뜻한 소리).
    /// 저작권 걱정 없는 자체 제작 소리이고, 나중에 녹음 파일로 바꾸려면 Resources/Sfx/{이름} 에 넣으면 그쪽을 쓴다.
    /// </summary>
    public class SoundFx : MonoBehaviour
    {
        public enum Kind { Jump, Land, Bounce, Dew, Hit, Checkpoint, Climb, Ending }

        const int Rate = 44100;
        AudioSource _src;
        readonly Dictionary<Kind, AudioClip> _clips = new Dictionary<Kind, AudioClip>();
        public static bool Muted
        {
            get => PlayerPrefs.GetInt("sfx_muted", 0) == 1;
            set { PlayerPrefs.SetInt("sfx_muted", value ? 1 : 0); }
        }

        void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            foreach (Kind k in Enum.GetValues(typeof(Kind)))
            {
                var file = Resources.Load<AudioClip>("Sfx/" + k.ToString().ToLowerInvariant());
                _clips[k] = file != null ? file : Synth(k);
            }
        }

        public void Play(Kind k, float volume = 1f)
        {
            if (Muted || !_clips.TryGetValue(k, out var clip)) return;
            _src.pitch = k == Kind.Dew || k == Kind.Climb ? UnityEngine.Random.Range(0.95f, 1.08f) : 1f;
            _src.PlayOneShot(clip, volume);
        }

        // ───────────────────────── 합성 ─────────────────────────

        static AudioClip Synth(Kind k)
        {
            switch (k)
            {
                case Kind.Jump:       return Make("jump", 0.16f, t => Tone(Sweep(t, 0.16f, 330, 620), t) * Env(t, 0.005f, 0.16f) * 0.5f);
                case Kind.Land:       return Make("land", 0.12f, t => (Noise() * 0.5f + Tone(140, t) * 0.5f) * Env(t, 0.002f, 0.12f) * LowFade(t, 0.12f) * 0.45f);
                case Kind.Bounce:     return Make("bounce", 0.35f, t => Tone(Sweep(t, 0.35f, 220, 880) + 12 * Mathf.Sin(t * 60f), t) * Env(t, 0.005f, 0.35f) * 0.5f);
                case Kind.Dew:        return Make("dew", 0.3f, t => (Tone(1318.5f, t) * 0.6f + Tone(1975.5f, t) * 0.3f) * Env(t, 0.002f, 0.3f) * 0.45f);
                case Kind.Hit:        return Make("hit", 0.25f, t => (Tone(Sweep(t, 0.25f, 300, 150), t) * 0.6f + Noise() * 0.25f) * Env(t, 0.003f, 0.25f) * 0.5f);
                case Kind.Climb:      return Make("climb", 0.07f, t => Noise() * Env(t, 0.002f, 0.07f) * 0.25f);
                case Kind.Checkpoint: return Arp("checkpoint", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, 0.5f);
                default:              return Arp("ending", new[] { 392f, 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.16f, 1.2f);
            }
        }

        static readonly System.Random Rng = new System.Random(7);
        static float Noise() => (float)(Rng.NextDouble() * 2 - 1);
        static float Tone(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);
        static float Sweep(float t, float len, float a, float b) => Mathf.Lerp(a, b, t / len) * 0.5f + a * 0.5f; // 위상 누적 대신 근사
        static float Env(float t, float attack, float len) => Mathf.Clamp01(t / attack) * Mathf.Pow(1f - Mathf.Clamp01(t / len), 2f);
        static float LowFade(float t, float len) => 1f - t / len;

        static AudioClip Make(string name, float len, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>음을 차례로 겹쳐 울리는 짧은 아르페지오 (실로폰 느낌).</summary>
        static AudioClip Arp(string name, float[] notes, float step, float tail)
        {
            float len = step * notes.Length + tail;
            return Make(name, len, t =>
            {
                float s = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float lt = t - i * step;
                    if (lt < 0f) continue;
                    s += (Tone(notes[i], lt) * 0.7f + Tone(notes[i] * 2f, lt) * 0.15f) * Mathf.Exp(-lt * 4f) * Mathf.Clamp01(lt / 0.004f);
                }
                return s * 0.3f;
            });
        }
    }
}
