using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace GalaxyToon
{
    /// <summary>
    /// キャラ専用のシャドウマップ：キャラ(Galaxy Toon Face のあるもの)だけを囲む狭い範囲で、高解像度の影を描く。
    /// 光の向きは、キャラ専用の固定ライト(Galaxy Toon Light)があればそれ、無ければシーンのメインライト。
    /// シーンの影(カスケード)より細かいので、前髪・指・襟の自己影がくっきり出る。キャラ専用の固定ライトにも影が付く。
    /// GalaxyToon のシェーダーは、ふだんの落ち影と合わせて(暗い方)使う。
    /// </summary>
    public sealed class GalaxyCharacterShadowFeature : ScriptableRendererFeature
    {
        [Tooltip("影の画像の大きさ(画素)")]
        public int resolution = 2048;

        [Tooltip("影の濃さ")]
        [Range(0f, 1f)] public float strength = 1f;

        [Tooltip("影の縁のぼかし(画素)")]
        [Range(0.5f, 4f)] public float softness = 1.5f;

        [Tooltip("自分の影の汚れを防ぐずらし(画素の大きさに対する倍率)")]
        [Range(0f, 4f)] public float depthBias = 1f;
        [Range(0f, 4f)] public float normalBias = 1.5f;

        internal static readonly int ShadowMapId = Shader.PropertyToID("_GalaxyCharShadowMap");
        internal static readonly int ShadowMatrixId = Shader.PropertyToID("_GalaxyCharShadowMatrix");
        internal static readonly int ShadowParamsId = Shader.PropertyToID("_GalaxyCharShadowParams");
        private static readonly int ToonLightDirId = Shader.PropertyToID("_AdvToonLightDir");

        private ShadowPass pass;

        public override void Create()
        {
            // インスペクタを触ると Create がまた呼ばれるので、前の影の画像を放してから作り直す
            pass?.Release();
            // 止めた(非アクティブの)機能は値を入れ直さないので、毎フレームの始めに「使わない」に戻す
            RenderPipelineManager.beginContextRendering -= ResetGlobals;
            RenderPipelineManager.beginContextRendering += ResetGlobals;
            // シーンの影の前に描く(URP はシーンの影のあとでカメラの設定をやり直すので、ここで変えた行列が残らない)
            pass = new ShadowPass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingShadows };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection)
            {
                // このカメラでは使わない（前のカメラの影の値を残さない）
                Shader.SetGlobalVector(ShadowParamsId, Vector4.zero);
                return;
            }
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            Shader.SetGlobalVector(ShadowParamsId, Vector4.zero);
            pass?.Release();
        }

        /// <summary>止めたときにシェーダーへ「使わない」を入れる(止めた機能は値を入れ直さないため)。</summary>
        public static void ClearGlobals() => Shader.SetGlobalVector(ShadowParamsId, Vector4.zero);

        private static void ResetGlobals(ScriptableRenderContext context, List<Camera> cameras) => ClearGlobals();

        private sealed class ShadowPass : ScriptableRenderPass
        {
            private readonly GalaxyCharacterShadowFeature owner;
            private readonly List<(Renderer renderer, Material material, int submesh, int pass)> draws = new List<(Renderer, Material, int, int)>();
            private readonly List<Renderer> scratch = new List<Renderer>();
            private float nextScan;
            private GalaxyToonFace[] faces = new GalaxyToonFace[0];

            // 影の画像はフレームをまたいで持つ(RenderGraph の一時テクスチャだと、読むパスを宣言しないかぎりこのパスのあとで使い回される)
            private RTHandle shadowHandle;

            public ShadowPass(GalaxyCharacterShadowFeature owner) => this.owner = owner;

            public void Release()
            {
                shadowHandle?.Release();
                shadowHandle = null;
            }

            private sealed class PassData
            {
                public List<(Renderer renderer, Material material, int submesh, int pass)> draws;
                public Matrix4x4 view;
                public Matrix4x4 proj;
                public Matrix4x4 shadowMatrix;
                public Vector4 lightDirection;
                public Vector4 bias;
                public Vector4 parameters;
                public Matrix4x4 cameraView;
                public Matrix4x4 cameraProjection;
                public Matrix4x4 lightProjection;
                public RTHandle handle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Collect();
                if (draws.Count == 0 || !LightDirection(frameData, out var toLight))
                {
                    Shader.SetGlobalVector(ShadowParamsId, Vector4.zero);
                    return;
                }

                // キャラを囲む球に合わせた、光の向きの正射影
                var bounds = draws[0].renderer.bounds;
                foreach (var d in draws) bounds.Encapsulate(d.renderer.bounds);
                float radius = bounds.extents.magnitude + 0.05f;
                Vector3 eye = bounds.center + toLight * (radius + 1f);
                var view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(eye, Quaternion.LookRotation(-toLight), Vector3.one).inverse;
                var proj = Matrix4x4.Ortho(-radius, radius, -radius, radius, 0.01f, radius * 2f + 2f);
                int size = Mathf.Clamp(Mathf.ClosestPowerOfTwo(owner.resolution), 256, 4096);
                float texel = radius * 2f / size;

                ShadowUtils.ShadowRTReAllocateIfNeeded(ref shadowHandle, size, size, 32, 1, 0f, "GalaxyCharacterShadowMap");
                var shadowMap = renderGraph.ImportTexture(shadowHandle);
                Shader.SetGlobalTexture(ShadowMapId, shadowHandle); // 描くパスのあとも同じ画像を指したまま
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("GalaxyCharacterShadow", out var data))
                {
                    data.draws = draws;
                    data.view = view;
                    data.proj = proj;
                    data.shadowMatrix = ShadowTransform(proj, view);
                    data.lightDirection = new Vector4(toLight.x, toLight.y, toLight.z, 0f);
                    data.bias = new Vector4(-owner.depthBias * texel, -owner.normalBias * texel, 0f, 0f);
                    data.parameters = new Vector4(owner.strength, 1f / size, owner.softness, 1f);
                    var cameraData = frameData.Get<UniversalCameraData>();
                    var resources = frameData.Get<UniversalResourceData>();
                    data.cameraView = cameraData.GetViewMatrix();
                    // UniversalCameraData.GetGPUProjectionMatrix は互換モードでないと 0 を返すので、自分で GPU 用に直す
                    data.cameraProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), !resources.isActiveTargetBackBuffer);
                    data.lightProjection = GL.GetGPUProjectionMatrix(proj, true);
                    builder.SetRenderAttachmentDepth(shadowMap, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(shadowMap, ShadowMapId);
                    data.handle = shadowHandle;
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        var cmd = context.cmd;
                        cmd.ClearRenderTarget(true, false, Color.black);
                        // 行列だけを入れる（カメラの位置などのグローバル値は変えない）。影の画像はテクスチャなので GPU 用に直した行列
                        RenderingUtils.SetViewAndProjectionMatrices(cmd, d.view, d.lightProjection, false);
                        cmd.SetGlobalVector(ShadowBiasId, d.bias);
                        cmd.SetGlobalVector(LightDirectionId, d.lightDirection);
                        cmd.SetGlobalDepthBias(1f, 2.5f);
                        foreach (var (r, m, sub, p) in d.draws)
                        {
                            if (r != null && m != null) cmd.DrawRenderer(r, m, sub, p);
                        }

                        cmd.SetGlobalDepthBias(0f, 0f);
                        // カメラの行列に戻す(シーンの影が無い設定では、URP がカメラの設定をやり直さないため)
                        RenderingUtils.SetViewAndProjectionMatrices(cmd, d.cameraView, d.cameraProjection, true);
                        cmd.SetGlobalMatrix(ShadowMatrixId, d.shadowMatrix);
                        cmd.SetGlobalVector(ShadowParamsId, d.parameters);
                    });
                }
            }

            private static readonly int ShadowBiasId = Shader.PropertyToID("_ShadowBias");
            private static readonly int LightDirectionId = Shader.PropertyToID("_LightDirection");

            /// <summary>Galaxy Toon Face のあるキャラの、GalaxyToon のマテリアルで影を落とす所を集める(1 秒ごとに探し直す)。</summary>
            private void Collect()
            {
                if (Time.unscaledTime >= nextScan || !Application.isPlaying)
                {
                    nextScan = Time.unscaledTime + 1f;
                    faces = Object.FindObjectsByType<GalaxyToonFace>(FindObjectsSortMode.None);
                }

                draws.Clear();
                foreach (var face in faces)
                {
                    if (face == null || !face.isActiveAndEnabled) continue;
                    var root = face.transform;
                    scratch.Clear();
                    root.GetComponentsInChildren(false, scratch);
                    foreach (var r in scratch)
                    {
                        if (!r.enabled || r.shadowCastingMode == ShadowCastingMode.Off || !(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
                        var materials = r.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            var m = materials[i];
                            if (m == null || m.shader == null || !m.shader.name.StartsWith("GalaxyShader/")) continue;
                            int p = m.FindPass("ShadowCaster");
                            if (p >= 0 && m.GetShaderPassEnabled("ShadowCaster")) draws.Add((r, m, i, p));
                        }
                    }
                }
            }

            /// <summary>光へ向かう向き。キャラ専用の固定ライトがあればそれ、無ければメインライト。</summary>
            private static bool LightDirection(ContextContainer frameData, out Vector3 toLight)
            {
                Vector4 toon = Shader.GetGlobalVector(ToonLightDirId);
                if (((Vector3)toon).sqrMagnitude > 1e-4f)
                {
                    toLight = ((Vector3)toon).normalized;
                    return true;
                }

                var lights = frameData.Get<UniversalLightData>();
                if (lights.mainLightIndex >= 0 && lights.mainLightIndex < lights.visibleLights.Length)
                {
                    toLight = -(Vector3)lights.visibleLights[lights.mainLightIndex].localToWorldMatrix.GetColumn(2);
                    toLight.Normalize();
                    return true;
                }

                toLight = Vector3.up;
                return false;
            }

            /// <summary>ワールド → 影の画像(0〜1)の行列。URP のシャドウマップと同じ作り方(深度の向きが逆の GPU では z を反転)。</summary>
            private static Matrix4x4 ShadowTransform(Matrix4x4 proj, Matrix4x4 view)
            {
                if (SystemInfo.usesReversedZBuffer)
                {
                    proj.m20 = -proj.m20;
                    proj.m21 = -proj.m21;
                    proj.m22 = -proj.m22;
                    proj.m23 = -proj.m23;
                }

                var scaleBias = Matrix4x4.identity;
                scaleBias.m00 = 0.5f;
                scaleBias.m11 = 0.5f;
                scaleBias.m22 = 0.5f;
                scaleBias.m03 = 0.5f;
                scaleBias.m23 = 0.5f;
                scaleBias.m13 = 0.5f;
                return scaleBias * proj * view;
            }
        }
    }
}
