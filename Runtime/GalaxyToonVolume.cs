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

    /// <summary>
    /// <see cref="GalaxyToonVolume"/> の値を、カメラごとにシェーダーのグローバル値(_GalaxyVolumeA・_GalaxyVolumeB)へ入れる。描くパスは足さない。
    /// </summary>
    public sealed class GalaxyToonVolumeFeature : ScriptableRendererFeature
    {
        private static readonly int VolumeAId = Shader.PropertyToID("_GalaxyVolumeA");
        private static readonly int VolumeBId = Shader.PropertyToID("_GalaxyVolumeB");

        public override void Create()
        {
            // 止めた(非アクティブの)機能は値を入れ直さないので、毎フレームの始めに「何もしない」に戻す
            RenderPipelineManager.beginContextRendering -= ResetGlobals;
            RenderPipelineManager.beginContextRendering += ResetGlobals;
        }

        private static void ResetGlobals(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
        {
            Shader.SetGlobalVector(VolumeAId, Vector4.zero);
            Shader.SetGlobalVector(VolumeBId, Vector4.zero);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            var stack = camera != null && camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.volumeStack != null ? data.volumeStack : VolumeManager.instance.stack;
            var v = stack?.GetComponent<GalaxyToonVolume>();
            if (v == null || !v.active)
            {
                Shader.SetGlobalVector(VolumeAId, Vector4.zero);
                Shader.SetGlobalVector(VolumeBId, Vector4.zero);
                return;
            }

            Color tint = v.shadowTint.value.linear; // シェーダーはリニアで計算する
            Shader.SetGlobalVector(VolumeAId, new Vector4(v.rim.value - 1f, v.shadowDarken.value, v.saturation.value, v.brightness.value));
            Shader.SetGlobalVector(VolumeBId, new Vector4(tint.r - 1f, tint.g - 1f, tint.b - 1f, v.outlineWidth.value - 1f));
        }

        protected override void Dispose(bool disposing)
        {
            Shader.SetGlobalVector(VolumeAId, Vector4.zero);
            Shader.SetGlobalVector(VolumeBId, Vector4.zero);
        }
    }
}
