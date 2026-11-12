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
}
