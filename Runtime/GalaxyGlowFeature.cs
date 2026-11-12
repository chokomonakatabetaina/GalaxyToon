using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GalaxyToon
{
    /// <summary>
    /// 撮影処理のような拡散グロー：明るい所だけを取り出し、半分の大きさでぼかして、元の絵にスクリーンで重ねる。エフェクト用のブルームとは別に調整できる。
    /// </summary>
    public sealed class GalaxyGlowFeature : GalaxyScreenFeatureBase
    {
        [Tooltip("これより明るい所が光る（色のいちばん明るいチャンネル）")]
        [Range(0f, 2f)] public float threshold = 0.85f;
        [Range(0f, 1f)] public float softness = 0.3f;
        [Range(0f, 3f)] public float intensity = 0.8f;
        [Tooltip("光の広がり（画素）")]
        [Range(1f, 16f)] public float spread = 4f;
        [Tooltip("キャラだけを光らせる：カメラからこの距離（m）より奥（背景）は光らせない。0 で画面全体")]
        [Min(0f)] public float characterDepth;

        private static readonly int GlowId = Shader.PropertyToID("_GalaxyGlow");
        private static readonly int Glow2Id = Shader.PropertyToID("_GalaxyGlow2");
        private static readonly int GlowTexId = Shader.PropertyToID("_GalaxyGlowTex");
        private GlowPass pass;

        public override void Create()
        {
            pass = new GlowPass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || !EnsureMaterial()) return;
            material.SetVector(GlowId, new Vector4(threshold, softness, intensity, spread));
            material.SetVector(Glow2Id, new Vector4(Mathf.Max(0f, characterDepth), 0f, 0f, 0f));
            // キャラだけに絞るときは深度を使う
            pass.ConfigureInput(characterDepth > 0f ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
            renderer.EnqueuePass(pass);
        }

        private sealed class GlowPass : ScriptableRenderPass
        {
            private readonly GalaxyGlowFeature owner;
            public GlowPass(GalaxyGlowFeature owner) => this.owner = owner;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "GalaxyGlowSmall";
                desc.clearBuffer = false;
                desc.width = Mathf.Max(1, desc.width / 2);
                desc.height = Mathf.Max(1, desc.height / 2);
                var glow = renderGraph.CreateTexture(desc);
                using (var builder = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, glow, owner.material, 1), "GalaxyGlowPrefilter", true))
                {
                    builder.SetGlobalTextureAfterPass(glow, GlowTexId);
                }

                var full = renderGraph.GetTextureDesc(source);
                full.name = "GalaxyGlowComposite";
                full.clearBuffer = false;
                var destination = renderGraph.CreateTexture(full);
                using (var builder = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, owner.material, 2), "GalaxyGlowComposite", true))
                {
                    builder.UseGlobalTexture(GlowTexId);
                }

                resources.cameraColor = destination;
            }
        }
    }
}
