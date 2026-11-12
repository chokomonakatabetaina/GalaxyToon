using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GalaxyToon
{
    /// <summary>画面の効果の共通部分：シェーダー Hidden/GalaxyToon/Screen のマテリアルを持ち、画面の色を読んで別のテクスチャへ描き、それを画面の色にする。</summary>
    public abstract class GalaxyScreenFeatureBase : ScriptableRendererFeature
    {
        internal Material material;

        protected bool EnsureMaterial()
        {
            if (material != null) return true;
            var shader = Shader.Find("Hidden/GalaxyToon/Screen");
            if (shader == null) return false;
            material = CoreUtils.CreateEngineMaterial(shader);
            return material != null;
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        /// <summary>画面の色を、マテリアルのパスで描き直す（描いたものを次からの画面の色にする）。</summary>
        internal static void DrawScreen(RenderGraph renderGraph, ContextContainer frameData, Material material, int pass, string name, bool readsDepthNormals = false)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var source = resources.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = name;
            desc.clearBuffer = false;
            var destination = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, pass), name, true))
            {
                // 画面の線は深度と法線のテクスチャを読む(読むと宣言して、RenderGraph に寿命を正しく数えさせる)
                if (readsDepthNormals)
                {
                    if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture);
                    if (resources.cameraNormalsTexture.IsValid()) builder.UseTexture(resources.cameraNormalsTexture);
                }
            }

            resources.cameraColor = destination;
        }
    }

    /// <summary>
    /// 画面の線：深度と法線の差で、外形の線（手前の物の外側に置くので、小さいキャラが痩せない）と内側の線（指・重なり・部位の境目）を描く。
    /// 線は塗りの色を暗くして作る（決まった色で塗らない）。メッシュを押し出す輪郭線と一緒に使える。不透明物のあと（半透明の前）に描く。
    /// </summary>
    public sealed class GalaxyScreenLineFeature : GalaxyScreenFeatureBase
    {
        [Range(0.5f, 4f)] public float thickness = 1.2f;
        [Tooltip("外形の線：となりがこの割合だけ手前なら線（深度の差 ÷ 奥行き）")]
        [Range(0.005f, 0.5f)] public float depthThreshold = 0.06f;
        [Tooltip("内側の線：法線の差（1 − 内積）がこれより大きいと線")]
        [Range(0.05f, 1f)] public float normalThreshold = 0.35f;
        [Range(0f, 1f)] public float strength = 0.85f;
        [Tooltip("線の所の色の倍率（小さいほど濃い線）")]
        [Range(0f, 1f)] public float darken = 0.35f;
        [Tooltip("この距離（m）より遠い所には線を描かない")]
        public float maxDistance = 30f;
        [Tooltip("物ごとの線：物（服・体・髪など。マテリアルの _ObjectId）が変わる所にも線（重なった服と体の境目など）")]
        public bool objectLines;

        private static readonly int LineId = Shader.PropertyToID("_GalaxyLine");
        private static readonly int Line2Id = Shader.PropertyToID("_GalaxyLine2");
        private static readonly int ObjectIdTexId = Shader.PropertyToID("_GalaxyObjectIdTex");
        private static ShaderTagId s_objectIdTag;
        private static bool s_objectIdTagInit;
        private static ShaderTagId ObjectIdTag
        {
            get
            {
                if (!s_objectIdTagInit)
                {
                    s_objectIdTag = new ShaderTagId("GalaxyObjectId");
                    s_objectIdTagInit = true;
                }
                return s_objectIdTag;
            }
        }
        private LinePass pass;

        public override void Create()
        {
            pass = new LinePass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || !EnsureMaterial()) return;
            material.SetVector(LineId, new Vector4(thickness, depthThreshold, normalThreshold, strength));
            material.SetVector(Line2Id, new Vector4(darken, maxDistance, objectLines ? 1f : 0f, 0f));
            renderer.EnqueuePass(pass);
        }

        private sealed class LinePass : ScriptableRenderPass
        {
            private readonly GalaxyScreenLineFeature owner;
            public LinePass(GalaxyScreenLineFeature owner) => this.owner = owner;

            private sealed class IdPassData
            {
                public RendererListHandle list;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (owner.objectLines) DrawObjectIds(renderGraph, frameData);
                GalaxyScreenFeatureBase.DrawScreen(renderGraph, frameData, owner.material, 0, "GalaxyScreenLines", true);
            }

            /// <summary>物の番号を色にしたテクスチャを作る（GalaxyToon の GalaxyObjectId のパスで、いまの深度と比べて手前の物だけ）。</summary>
            private void DrawObjectIds(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.activeDepthTexture.IsValid()) return;
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();
                var desc = renderGraph.GetTextureDesc(resources.activeColorTexture);
                desc.name = "GalaxyObjectId";
                desc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
                desc.depthBufferBits = DepthBits.None;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                // 深度と同じサンプル数にする（MSAA の深度と比べるので、数が違うと描けない）。読むときは解決した絵（普通のテクスチャ）で
                desc.msaaSamples = renderGraph.GetTextureDesc(resources.activeDepthTexture).msaaSamples;
                desc.bindTextureMS = false;
                var ids = renderGraph.CreateTexture(desc);
                using (var builder = renderGraph.AddRasterRenderPass<IdPassData>("GalaxyObjectId", out var data))
                {
                    var drawing = CreateDrawingSettings(ObjectIdTag, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
                    var filtering = new FilteringSettings(RenderQueueRange.opaque);
                    data.list = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
                    builder.UseRendererList(data.list);
                    builder.SetRenderAttachment(ids, 0);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(ids, ObjectIdTexId);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((IdPassData d, RasterGraphContext context) => context.cmd.DrawRendererList(d.list));
                }
            }
        }
    }

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
