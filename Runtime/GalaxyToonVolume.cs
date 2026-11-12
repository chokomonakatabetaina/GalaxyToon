using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GalaxyToon
{
    /// <summary>
    /// Volume で、GalaxyToon のキャラ全体のリム・影・色・輪郭線をまとめて調整する(場面・時間帯ごとに Volume を置いて切り替える)。
    /// 値をシェーダーへ渡すのは <see cref="GalaxyToonVolumeFeature"/>(レンダラーに足す)。マテリアルの「Volume の調整を受ける」をオフにした所は変えない。
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("GalaxyToon/Galaxy Toon")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class GalaxyToonVolume : VolumeComponent
    {
        [Tooltip("リムの強さの倍率")]
        public ClampedFloatParameter rim = new ClampedFloatParameter(1f, 0f, 3f);

        [Tooltip("影の側をさらに暗くする量")]
        public ClampedFloatParameter shadowDarken = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("影の側に掛ける色(白でそのまま)")]
        public ColorParameter shadowTint = new ColorParameter(Color.white, false, false, true);

        [Tooltip("彩度(-1 で灰色、+1 で 2 倍)")]
        public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, -1f, 1f);

        [Tooltip("明るさ(-1〜+1。倍率 - 1)")]
        public ClampedFloatParameter brightness = new ClampedFloatParameter(0f, -1f, 1f);

        [Tooltip("輪郭線の太さの倍率")]
        public ClampedFloatParameter outlineWidth = new ClampedFloatParameter(1f, 0f, 3f);
    }
}
