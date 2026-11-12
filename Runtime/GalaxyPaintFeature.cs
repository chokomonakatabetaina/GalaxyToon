using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GalaxyToon
{
    /// <summary>
    /// 塗りを均す：桑原フィルタで塗りのムラ・細かい模様をならし（輪郭は残る）、明るさを決まった段に寄せる（絵画・セル画の背景のような見た目）。背景向け。
    /// </summary>
    public sealed class GalaxyPaintFeature : GalaxyScreenFeatureBase
    {
        [Range(1, 4)] public int radius = 2;
        [Tooltip("明るさの段の数（0 で段にしない。色相は変えない）")]
        [Range(0, 16)] public int levels = 6;
        [Range(0f, 1f)] public float strength = 1f;

        private static readonly int PaintId = Shader.PropertyToID("_GalaxyPaint");
        private PaintPass pass;

        public override void Create()
        {
            pass = new PaintPass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || !EnsureMaterial()) return;
            material.SetVector(PaintId, new Vector4(radius, levels, strength, 0f));
            renderer.EnqueuePass(pass);
        }

        private sealed class PaintPass : ScriptableRenderPass
        {
            private readonly GalaxyPaintFeature owner;
            public PaintPass(GalaxyPaintFeature owner) => this.owner = owner;
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) => GalaxyScreenFeatureBase.DrawScreen(renderGraph, frameData, owner.material, 3, "GalaxyPaint");
        }
    }
}
