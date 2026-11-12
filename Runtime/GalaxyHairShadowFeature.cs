using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace GalaxyToon
{
    /// <summary>
    /// 髪の影(前髪の影を顔に落とす)のための URP レンダラー機能。自分では何も描かず、URP に「深度と法線のプリパスが要る」と伝えるだけ。
    /// 髪のマテリアル(髪の影を落とす=ON)が法線テクスチャの A に 1 を書き、顔(髪の影を受ける)がそれを光の向きへずらして読む。
    /// 有効な GalaxyToonFace が1つも無い(キャラのいない)場面ではプリパスも要求しない(負荷 0)。
    /// 自前のテクスチャを作ってグローバルに渡す方式は D3D12 でクラッシュしたので使わない。
    /// </summary>
    public sealed class GalaxyHairShadowFeature : ScriptableRendererFeature
    {
        private RequestPass pass;

        public override void Create()
        {
            pass = new RequestPass { renderPassEvent = RenderPassEvent.BeforeRenderingOpaques };
            // 止めた(非アクティブの)機能は値を入れ直さないので、毎フレームの始めに「使わない」に戻す
            RenderPipelineManager.beginContextRendering -= ResetGlobals;
            RenderPipelineManager.beginContextRendering += ResetGlobals;
            pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        private static void ResetGlobals(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras) => Shader.SetGlobalFloat("_GalaxyHairShadowOn", 0f);

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            bool on = GalaxyToonFace.ActiveCount > 0
                      && renderingData.cameraData.cameraType != CameraType.Preview
                      && renderingData.cameraData.cameraType != CameraType.Reflection;
            Shader.SetGlobalFloat("_GalaxyHairShadowOn", on ? 1f : 0f); // ScriptableObject の生成中に静的初期化が走ると落ちるので ID は使わない
            if (on) renderer.EnqueuePass(pass);
        }

        /// <summary>プリパスを要求するだけの空のパス。</summary>
        private sealed class RequestPass : ScriptableRenderPass
        {
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) { }
        }
    }
}
