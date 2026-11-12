using UnityEngine;

namespace GalaxyToon
{
    /// <summary>
    /// 音に反応する演出：鳴っている音を低音・中音・高音に分けて大きさを測り、シェーダーのグローバル値 _GalaxyAudio(x=低音 y=中音 z=高音 w=全体)に入れる。
    /// マテリアルの「音で発光を強める量」「音でリムを強める量」「音で輪郭線を太くする量」と帯域で、光る・縁が光る・線が太くなる。
    /// 音の元(source)を空にすると、聞こえている音全体(AudioListener)を測る。
    /// </summary>
    [AddComponentMenu("GalaxyToon/Galaxy Audio React")]
    public sealed class GalaxyAudioReact : MonoBehaviour
    {
        [Tooltip("測る音の元(空なら聞こえている音全体)")]
        public AudioSource source;

        [Tooltip("大きさの倍率(曲の音量に合わせる)")]
        [Range(0.1f, 50f)] public float gain = 8f;

        [Tooltip("大きくなるときの速さ(1 秒あたり)")]
        [Range(1f, 60f)] public float attack = 30f;

        [Tooltip("小さくなるときの速さ(1 秒あたり)")]
        [Range(0.5f, 30f)] public float release = 6f;

        [Tooltip("低音と中音の境目(Hz)")]
        public float lowMax = 250f;

        [Tooltip("中音と高音の境目(Hz)")]
        public float midMax = 2000f;

        private static readonly int AudioId = Shader.PropertyToID("_GalaxyAudio");
        private readonly float[] spectrum = new float[512];
        private Vector4 level;

        /// <summary>いまの大きさ(x=低音 y=中音 z=高音 w=全体)。</summary>
        public Vector4 Level => level;

        /// <summary>
        /// 音の強さの分布(GetSpectrumData の結果)から、低音・中音・高音・全体の大きさ(0〜1)を出す。
        /// 高い帯ほど 1 区間の値が小さいので重みを足し、平方根で小さい音も見えるようにする。
        /// </summary>
        public static Vector4 Bands(float[] spectrum, float sampleRate, float gain, float lowMax, float midMax)
        {
            // 1 つの区間の幅(Hz) = 出力の周波数の半分 ÷ 区間の数
            float binHz = sampleRate * 0.5f / spectrum.Length;
            float low = 0f, mid = 0f, high = 0f;
            for (int i = 1; i < spectrum.Length; i++)
            {
                float hz = i * binHz;
                float v = spectrum[i];
                if (hz < lowMax) low += v;
                else if (hz < midMax) mid += v;
                else high += v;
            }

            var bands = new Vector4(
                Mathf.Clamp01(Mathf.Sqrt(low * gain)),
                Mathf.Clamp01(Mathf.Sqrt(mid * gain * 2f)),
                Mathf.Clamp01(Mathf.Sqrt(high * gain * 6f)),
                0f);
            bands.w = Mathf.Clamp01((bands.x + bands.y + bands.z) / 3f * 1.5f);
            return bands;
        }

        private void OnDisable()
        {
            level = Vector4.zero;
            Shader.SetGlobalVector(AudioId, Vector4.zero);
        }

        private void Update()
        {
            if (source != null) source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            else AudioListener.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

            var target = Bands(spectrum, AudioSettings.outputSampleRate, gain, lowMax, midMax);
            float dt = Time.deltaTime;
            for (int c = 0; c < 4; c++)
            {
                float speed = target[c] > level[c] ? attack : release;
                level[c] = Mathf.Lerp(level[c], target[c], 1f - Mathf.Exp(-speed * dt));
            }

            Shader.SetGlobalVector(AudioId, level);
        }
    }
}
