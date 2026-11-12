using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GalaxyToon
{
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
}
