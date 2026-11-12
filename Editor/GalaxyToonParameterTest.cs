using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GalaxyToon.Editor
{
    /// <summary>
    /// GalaxyShader/Toon の全機能・ライト方向・各パラメータが実際に画面ピクセルに反映されているかを
    /// オフスクリーン RenderTexture への描画差分(Before / After のピクセル変化量)で自動検証するフルテストツール。
    /// </summary>
    public static class GalaxyToonParameterTest
    {
        private const int Resolution = 128;

        public struct TestCaseResult
        {
            public string Category;
            public string ParameterName;
            public bool Passed;
            public float MaxPixelDiff;
            public float MeanPixelDiff;
            public string Note;
        }

        [MenuItem("Tools/GalaxyToon/開発用/全パラメータ・ライト反映フルテスト実行", false, 100)]
        public static void RunFullParameterTestMenu()
        {
            RunAllTests(out int passed, out int failed, out string report);
            if (failed == 0)
            {
                Debug.Log($"[GalaxyToon フルテスト] 全 {passed} 項目のパラメータ・ライト反映テストに PASS しました！\n" + report);
            }
            else
            {
                Debug.LogWarning($"[GalaxyToon フルテスト] PASS: {passed} / FAIL: {failed}\n" + report);
            }
        }

        public static List<TestCaseResult> RunAllTests(out int passedCount, out int failedCount, out string summaryReport)
        {
            var results = new List<TestCaseResult>();
            Shader shader = Shader.Find("GalaxyShader/Toon");
            if (shader == null)
            {
                passedCount = 0;
                failedCount = 1;
                summaryReport = "GalaxyShader/Toon が見つかりません。";
                return results;
            }

            // テスト用のオフスクリーン環境を構築(シーンを汚さないよう HideAndDontSave)
            var camGo = new GameObject("__GalaxyToonTestCam") { hideFlags = HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.transform.position = new Vector3(0f, 0f, -2.6f);
            cam.transform.LookAt(Vector3.zero);

            var sphereGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereGo.name = "__GalaxyToonTestSphere";
            sphereGo.hideFlags = HideFlags.HideAndDontSave;
            sphereGo.transform.position = Vector3.zero;
            sphereGo.transform.rotation = Quaternion.Euler(15f, 30f, 0f);
            var renderer = sphereGo.GetComponent<MeshRenderer>();

            // テスト対象の球とカメラだけを専用レイヤー(31)に隔離、または既存オブジェクトを一時非表示化しないために
            // カメラを遠くの座標(Y = -5000)へ配置して既存シーンのモデルと重ならないようにする
            Vector3 testOrigin = new Vector3(0f, -5000f, 0f);
            sphereGo.transform.position = testOrigin;
            cam.transform.position = testOrigin + new Vector3(0f, 0f, -2.5f);
            cam.transform.LookAt(testOrigin);

            var rt = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
            var readTex = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);

            // テスト用テクスチャ生成(グラデーションランプ、チェッカーマスク、SDFモック、ノーマルマップ)
            Texture2D rampTex = CreateMockRampTexture();
            Texture2D checkerMask = CreateCheckerTexture();
            Texture2D sdfMockTex = CreateSdfMockTexture();
            Texture2D bumpMockTex = CreateBumpMockTexture();

            // 既存のグローバル変数を退避
            Vector4 origAdvDir = Shader.GetGlobalVector("_AdvToonLightDir");
            Vector4 origAdvCol = Shader.GetGlobalVector("_AdvToonLightColor");
            float origToonOff = Shader.GetGlobalFloat("_GalaxyToonOff");
            Shader.SetGlobalFloat("_GalaxyToonOff", 0f);
            bool asyncCompile = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false; // 仮の水色表示(非同期コンパイル中)で比べると、差が 0 になって誤って FAIL になる

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            renderer.sharedMaterial = mat;

            try
            {
                void ResetTestMaterial(bool withRamp = false, bool withAnime = false)
                {
                    GalaxyToonGUI.ResetAll(mat);
                    mat.SetColor("_BaseColor", new Color(0.95f, 0.82f, 0.78f, 1f));
                    mat.SetColor("_Shade1Tint", new Color(0.65f, 0.50f, 0.55f, 1f));
                    mat.SetFloat("_Shade1Edge", 0.5f);
                    mat.SetFloat("_Shade1Soft", 0.05f);
                    mat.SetFloat("_LightSource", 0f); // キャラ専用ライト(_AdvToonLightDir)を使用
                    Shader.SetGlobalVector("_AdvToonLightDir", new Vector3(0.55f, 0.45f, -0.70f).normalized);
                    Shader.SetGlobalVector("_AdvToonLightColor", new Vector4(1f, 1f, 1f, 1f));
                    if (withRamp)
                    {
                        mat.SetTexture("_ShadowRampTex", rampTex);
                    }
                    if (withAnime)
                    {
                        mat.SetFloat("_AnimeMode", 1f);
                    }
                    GalaxyToonGUI.Apply(mat);
                }

                Color[] RenderCurrent()
                {
                    GalaxyToonGUI.Apply(mat);
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.Render(); // 2 回描く: DX12 は新しいキーワードの組み合わせの初回の描画が間に合わないことがある
                    RenderTexture.active = rt;
                    readTex.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
                    readTex.Apply();
                    RenderTexture.active = null;
                    cam.targetTexture = null;
                    return readTex.GetPixels();
                }

                void RunDiffTest(string category, string paramName, System.Action setupBefore, System.Action setupAfter, string note = "")
                {
                    setupBefore();
                    Color[] before = RenderCurrent();
                    setupAfter();
                    Color[] after = RenderCurrent();

                    float maxDiff = 0f;
                    float sumDiff = 0f;
                    for (int i = 0; i < before.Length; i++)
                    {
                        float d = Mathf.Abs(before[i].r - after[i].r)
                                + Mathf.Abs(before[i].g - after[i].g)
                                + Mathf.Abs(before[i].b - after[i].b);
                        if (d > maxDiff) maxDiff = d;
                        sumDiff += d;
                    }
                    float meanDiff = sumDiff / before.Length;
                    bool passed = maxDiff > 0.01f;
                    results.Add(new TestCaseResult
                    {
                        Category = category,
                        ParameterName = paramName,
                        Passed = passed,
                        MaxPixelDiff = maxDiff,
                        MeanPixelDiff = meanDiff,
                        Note = note
                    });
                }

                // ================================================================ 1. ライト方向・色・上限下限
                RunDiffTest("1. ライト", "ライトの向き回転 (_AdvToonLightDir)",
                    () => ResetTestMaterial(),
                    () => Shader.SetGlobalVector("_AdvToonLightDir", new Vector3(-0.75f, 0.25f, -0.60f).normalized),
                    "ライトの水平・垂直回転で陰影位置が移動するか");

                RunDiffTest("1. ライト", "ライトの色 (_AdvToonLightColor)",
                    () => ResetTestMaterial(),
                    () => Shader.SetGlobalVector("_AdvToonLightColor", new Vector4(1.0f, 0.5f, 0.3f, 1f)),
                    "ライト色変更が反映されるか");

                RunDiffTest("1. ライト", "モノクロ照明 (_LightGray)",
                    () => { ResetTestMaterial(); Shader.SetGlobalVector("_AdvToonLightColor", new Vector4(1.0f, 0.2f, 0.2f, 1f)); },
                    () => mat.SetFloat("_LightGray", 1.0f),
                    "色付きライトを白黒の明るさだけにするか");

                RunDiffTest("1. ライト", "明るさの下限 (_LightFloor)",
                    () => { ResetTestMaterial(); Shader.SetGlobalVector("_AdvToonLightColor", new Vector4(0.05f, 0.05f, 0.05f, 1f)); },
                    () => mat.SetFloat("_LightFloor", 0.6f),
                    "真っ暗な環境でも下限の明るさを保つか");

                // ================================================================ 2. 影 (通常時 & ランプテクスチャ併用時)
                RunDiffTest("2. 影(基本)", "影1の境界 (_Shade1Edge)",
                    () => ResetTestMaterial(false, false),
                    () => mat.SetFloat("_Shade1Edge", 0.78f),
                    "通常時の影境界スライダー");

                RunDiffTest("2. 影(基本)", "影1のぼかし (_Shade1Soft)",
                    () => ResetTestMaterial(false, false),
                    () => mat.SetFloat("_Shade1Soft", 0.35f),
                    "通常時の影ぼかしスライダー");

                RunDiffTest("2. 影(基本)", "影1の色 (_Shade1Tint)",
                    () => ResetTestMaterial(false, false),
                    () => mat.SetColor("_Shade1Tint", new Color(0.2f, 0.3f, 0.8f, 1f)),
                    "通常時の影色変更");

                RunDiffTest("2. 影(ランプ併用)", "ランプ使用時の _Shade1Edge",
                    () => ResetTestMaterial(true, false),
                    () => mat.SetFloat("_Shade1Edge", 0.78f),
                    "_ShadowRampTex 割り当て時にも影境界が動くか");

                RunDiffTest("2. 影(ランプ併用)", "ランプ使用時の _Shade1Soft",
                    () => ResetTestMaterial(true, false),
                    () => mat.SetFloat("_Shade1Soft", 0.35f),
                    "_ShadowRampTex 割り当て時にも影ぼかしが動くか");

                RunDiffTest("2. 影(ランプ併用)", "ランプ使用時の _Shade1Tint",
                    () => ResetTestMaterial(true, false),
                    () => mat.SetColor("_Shade1Tint", new Color(0.3f, 0.5f, 0.9f, 1f)),
                    "_ShadowRampTex 割り当て時にも影色ティントが効くか");

                RunDiffTest("2. 影(ランプ併用)", "ランプ使用時の2段目影 (_Shade2Tint / _Shade2Edge)",
                    () => ResetTestMaterial(true, false),
                    () => { mat.SetColor("_Shade2Tint", new Color(0.3f, 0.2f, 0.5f, 0.9f)); mat.SetFloat("_Shade2Edge", 0.45f); },
                    "_ShadowRampTex 割り当て時にも2段目影が効くか");

                RunDiffTest("2. 影(ランプ併用)", "ランプ使用時の3段目影 (_Shade3Tint / _Shade3Edge)",
                    () => ResetTestMaterial(true, false),
                    () => { mat.SetColor("_Shade3Tint", new Color(0.15f, 0.1f, 0.35f, 0.9f)); mat.SetFloat("_Shade3Edge", 0.35f); },
                    "_ShadowRampTex 割り当て時にも3段目影が効くか");

                // ================================================================ 3. アニメ塗り (ランプ併用含む)
                RunDiffTest("3. アニメ塗り", "アニメ影色シフト (_AnimeShadowHue / _AnimeShadowSat / _AnimeShadowValue)",
                    () => ResetTestMaterial(true, true),
                    () => { mat.SetFloat("_AnimeShadowHue", -35f); mat.SetFloat("_AnimeShadowSat", 1.8f); mat.SetFloat("_AnimeShadowValue", 0.5f); },
                    "ランプ＋アニメモードで影の色相・彩度・明度が変わるか");

                RunDiffTest("3. アニメ塗り", "アニメ境界線の色・太さ (_AnimeEdgeColor / _AnimeEdgeWidth)",
                    () => ResetTestMaterial(true, true),
                    () => { mat.SetColor("_AnimeEdgeColor", new Color(1.0f, 0.2f, 0.1f, 0.9f)); mat.SetFloat("_AnimeEdgeWidth", 0.12f); },
                    "ランプ＋アニメモードで境界線が描かれるか");

                // ================================================================ 4. SSS (表面下散乱・透過光)
                RunDiffTest("4. SSS(肌血色・透過)", "SSS ON/OFF & 血色カラー (_UseSss / _SssColor)",
                    () => ResetTestMaterial(true, true),
                    () => { mat.SetFloat("_UseSss", 1f); mat.SetColor("_SssColor", new Color(1.0f, 0.15f, 0.08f, 1.0f)); mat.SetFloat("_SssEdgeWidth", 0.18f); mat.SetFloat("_SssShadowBleed", 0.35f); },
                    "明暗境界および影内部に血色感が乗るか");

                RunDiffTest("4. SSS(肌血色・透過)", "SSS 境界のにじみ幅 (_SssEdgeWidth)",
                    () => { ResetTestMaterial(true, true); mat.SetFloat("_UseSss", 1f); mat.SetColor("_SssColor", new Color(1.0f, 0.15f, 0.08f, 1.0f)); mat.SetFloat("_SssEdgeWidth", 0.02f); },
                    () => mat.SetFloat("_SssEdgeWidth", 0.35f),
                    "_SssEdgeWidth で赤み帯の太さが変わるか");

                RunDiffTest("4. SSS(肌血色・透過)", "SSS 影内部の血色残り (_SssShadowBleed)",
                    () => { ResetTestMaterial(true, true); mat.SetFloat("_UseSss", 1f); mat.SetColor("_SssColor", new Color(1.0f, 0.15f, 0.08f, 1.0f)); mat.SetFloat("_SssShadowBleed", 0.0f); },
                    () => mat.SetFloat("_SssShadowBleed", 0.65f),
                    "_SssShadowBleed で影全体の血色が変わるか");

                RunDiffTest("4. SSS(肌血色・透過)", "SSS 透過光 (_SssTranslucency) [通常正面斜めライト・_SssFrontScatter=1]",
                    () => { ResetTestMaterial(true, true); mat.SetFloat("_UseSss", 1f); mat.SetFloat("_SssFrontScatter", 1f); mat.SetFloat("_SssTranslucency", 0f); },
                    () => { mat.SetFloat("_SssTranslucency", 1.5f); mat.SetFloat("_SssDistortion", 0.5f); mat.SetFloat("_SssPower", 2.0f); },
                    "正面〜斜めライトでも輪郭・薄肉部に透過散乱が乗るか(_LightCeiling で消されないか)");

                RunDiffTest("4. SSS(肌血色・透過)", "SSS 正面・斜めの光での透け (_SssFrontScatter)",
                    () => { ResetTestMaterial(true, true); mat.SetFloat("_UseSss", 1f); mat.SetFloat("_SssTranslucency", 1.5f); mat.SetFloat("_SssFrontScatter", 0f); },
                    () => mat.SetFloat("_SssFrontScatter", 1f),
                    "既定(0)は逆光だけで、上げると正面〜斜めの光でも輪郭・明暗境界が透けるか");

                RunDiffTest("4. SSS(肌血色・透過)", "SSS 厚みマップ (_ThicknessMap)",
                    () => { ResetTestMaterial(true, true); mat.SetFloat("_UseSss", 1f); mat.SetFloat("_SssShadowBleed", 0.5f); mat.SetFloat("_SssTranslucency", 1.0f); },
                    () => mat.SetTexture("_ThicknessMap", checkerMask),
                    "厚みマップの黒領域でSSSがマスクされるか");

                // ================================================================ 5. 顔 & SDF 顔影 + 上下ライト補正(_FaceSdfPitch)
                RunDiffTest("5. 顔・SDF", "顔の球面法線・フラット化 (_FaceSphere / _FaceShading)",
                    () => ResetTestMaterial(),
                    () => { mat.SetFloat("_FaceShading", 0.7f); },
                    "顔陰影のフラット化が効くか");

                RunDiffTest("5. 顔・SDF", "SDF 顔影と上下角度補正 (_UseFaceSdf / _FaceSdfPitch)",
                    () =>
                    {
                        ResetTestMaterial();
                        mat.SetFloat("_UseFaceSdf", 1f);
                        mat.SetTexture("_FaceSdfTex", sdfMockTex);
                        mat.SetFloat("_FaceSdfPitch", 0f);
                        Shader.SetGlobalVector("_AdvToonLightDir", new Vector3(0.4f, 0.85f, -0.35f).normalized);
                    },
                    () => mat.SetFloat("_FaceSdfPitch", 0.8f),
                    "ライトが高い位置にあるとき _FaceSdfPitch でSDF影の面積が変化するか");

                // ================================================================ 6. ストッキング・薄布シアー (_UseSheer)
                RunDiffTest("6. ストッキング(シアー)", "シアー透け感と素肌色 (_UseSheer / _SheerAmount / _SheerSkinColor)",
                    () => { ResetTestMaterial(); mat.SetColor("_BaseColor", new Color(0.15f, 0.12f, 0.16f, 1f)); },
                    () => { mat.SetFloat("_UseSheer", 1f); mat.SetFloat("_SheerAmount", 0.85f); mat.SetColor("_SheerSkinColor", new Color(0.98f, 0.80f, 0.72f, 1f)); },
                    "黒ストッキングの中心部に素肌色が透け、輪郭が黒く締まるか");

                RunDiffTest("6. ストッキング(シアー)", "シアー輪郭カーブと繊維光沢 (_SheerPower / _SheerSheen)",
                    () => { ResetTestMaterial(); mat.SetColor("_BaseColor", new Color(0.15f, 0.12f, 0.16f, 1f)); mat.SetFloat("_UseSheer", 1f); mat.SetFloat("_SheerAmount", 0.8f); mat.SetFloat("_SheerPower", 1.0f); mat.SetFloat("_SheerSheen", 0f); },
                    () => { mat.SetFloat("_SheerPower", 3.5f); mat.SetFloat("_SheerSheen", 0.8f); },
                    "デニール感カーブとストッキング特有の微細光沢が効くか");

                // ================================================================ 7. ハイライト (_SpecularMask / _SpecularShadowMask)
                RunDiffTest("7. ハイライト", "ハイライト基本とマスク (_SpecularColor / _SpecularMask)",
                    () => { ResetTestMaterial(); mat.SetColor("_SpecularColor", new Color(1f, 1f, 1f, 0.9f)); mat.SetFloat("_SpecularSize", 0.25f); },
                    () => mat.SetTexture("_SpecularMask", checkerMask),
                    "_SpecularMask でハイライトが部分マスクされるか");

                RunDiffTest("7. ハイライト", "影側のハイライト残存 (_SpecularShadowMask)",
                    () =>
                    {
                        ResetTestMaterial();
                        Shader.SetGlobalVector("_AdvToonLightDir", new Vector3(0.8f, 0.5f, 0.3f).normalized);
                        mat.SetFloat("_Shade1Edge", 0.85f); // 広く影にする
                        mat.SetColor("_SpecularColor", new Color(1f, 1f, 1f, 0.9f));
                        mat.SetFloat("_SpecularSize", 0.45f);
                        mat.SetFloat("_SpecularShadowMask", 1.0f);
                    },
                    () => mat.SetFloat("_SpecularShadowMask", 0.0f),
                    "_SpecularShadowMask = 0 で影の領域内にもハイライトが残るか");

                // ================================================================ 8. 異方性ハイライト (毛束ジッター & 2層目リング)
                RunDiffTest("8. 異方性(髪ハイライト)", "1層目・2層目の天使の輪 (_AnisoColor / _Aniso2Color)",
                    () => { ResetTestMaterial(); mat.SetColor("_AnisoColor", new Color(1f, 1f, 1f, 0.6f)); },
                    () => { mat.SetColor("_Aniso2Color", new Color(0.5f, 0.8f, 1.0f, 0.7f)); mat.SetFloat("_Aniso2Shift", -0.25f); },
                    "2本目のサブ天使の輪が追加されるか");

                RunDiffTest("8. 異方性(髪ハイライト)", "毛束ノイズのゆらぎ (_AnisoJitterStrength)",
                    () => { ResetTestMaterial(); mat.SetColor("_AnisoColor", new Color(1f, 1f, 1f, 0.8f)); mat.SetFloat("_AnisoJitterStrength", 0f); },
                    () => { mat.SetFloat("_AnisoJitterStrength", 0.35f); mat.SetFloat("_AnisoJitterScale", 30f); },
                    "毛束ジッターでハイライトリングが波打つか");

                // ================================================================ 9. クリアコート & ベルベット (アニメモード併用含む)
                RunDiffTest("9. 質感(PBR×Toon)", "クリアコート (_ClearCoat / _ClearCoatSmoothness) [アニメモードON時]",
                    () => ResetTestMaterial(true, true),
                    () => { mat.SetFloat("_ClearCoat", 0.9f); mat.SetFloat("_ClearCoatSmoothness", 0.92f); },
                    "_AnimeMode=1 かつ _AnimeNoGloss=1 でも _ClearCoat が効くか");

                RunDiffTest("9. 質感(PBR×Toon)", "ベルベット・起毛エッジ光沢 (_VelvetColor / _VelvetPower)",
                    () => ResetTestMaterial(true, true),
                    () => { mat.SetColor("_VelvetColor", new Color(0.9f, 0.85f, 1.0f, 0.85f)); mat.SetFloat("_VelvetPower", 2.5f); },
                    "輪郭に向かって起毛の柔らかい光沢が乗るか");

                // ================================================================ 10. リムライト (ライト方向絞り & 反対側セカンダリリム)
                RunDiffTest("10. リムライト", "ライト方向への絞り (_RimLightAlign)",
                    () => { ResetTestMaterial(); mat.SetColor("_RimColor", new Color(1f, 1f, 1f, 0.9f)); mat.SetFloat("_RimShadeFade", 0f); mat.SetFloat("_RimLightAlign", 0f); },
                    () => mat.SetFloat("_RimLightAlign", 1.0f),
                    "リムライトがライト側と逆側で絞られるか");

                RunDiffTest("10. リムライト", "反対側の環境リム色 (_RimOppositeColor)",
                    () => { ResetTestMaterial(); mat.SetColor("_RimColor", new Color(1f, 0.9f, 0.7f, 0.8f)); },
                    () => mat.SetColor("_RimOppositeColor", new Color(0.2f, 0.5f, 1.0f, 0.85f)),
                    "影側の輪郭に逆側の寒色セカンダリリムが乗るか");

                // ================================================================ 11. 輪郭線 (色トレース & Zオフセット)
                RunDiffTest("11. 輪郭線", "輪郭線の色トレース (_OutlineColorTrace)",
                    () => { ResetTestMaterial(); mat.SetFloat("_OutlineWidth", 3.5f); mat.SetColor("_OutlineColor", Color.black); mat.SetFloat("_OutlineColorTrace", 0f); },
                    () => mat.SetFloat("_OutlineColorTrace", 0.95f),
                    "輪郭線が黒から地の色と同系色の濃い色へ変化するか");

                RunDiffTest("11. 輪郭線", "輪郭線の奥行き押し込み (_OutlineZOffset)",
                    () => { ResetTestMaterial(); mat.SetFloat("_OutlineWidth", 3.5f); mat.SetFloat("_OutlineZOffset", 0f); },
                    () => mat.SetFloat("_OutlineZOffset", 8.0f),
                    "輪郭線のZオフセットが反映されるか");

                // ================================================================ 12. ノーマルマップ・色調補正・頬染め
                RunDiffTest("12. 基本・色調・頬染め", "ノーマルマップ (_BumpMap / _BumpScale)",
                    () => { ResetTestMaterial(); mat.SetTexture("_BumpMap", bumpMockTex); mat.SetFloat("_BumpScale", 0f); },
                    () => mat.SetFloat("_BumpScale", 1.5f),
                    "法線マップの凹凸で陰影が変化するか");

                RunDiffTest("12. 基本・色調・頬染め", "色調補正 (_HueShift / _Saturation / _Brightness)",
                    () => ResetTestMaterial(),
                    () => { mat.SetFloat("_HueShift", 45f); mat.SetFloat("_Saturation", 1.4f); },
                    "色調補正が反映されるか");

                RunDiffTest("12. 基本・色調・頬染め", "頬染め (_BlushAmount)",
                    () => ResetTestMaterial(),
                    () => { mat.SetFloat("_BlushAmount", 0.85f); mat.SetVector("_BlushUV", new Vector4(0.5f, 0.5f, 0f, 0f)); mat.SetFloat("_BlushSize", 0.35f); },
                    "頬染めが反映されるか");
            }
            finally
            {
                // グローバル変数を復元し、一時オブジェクトを破棄
                Shader.SetGlobalVector("_AdvToonLightDir", origAdvDir);
                Shader.SetGlobalVector("_AdvToonLightColor", origAdvCol);
                Shader.SetGlobalFloat("_GalaxyToonOff", origToonOff);
                ShaderUtil.allowAsyncCompilation = asyncCompile;

                Object.DestroyImmediate(mat);
                Object.DestroyImmediate(rampTex);
                Object.DestroyImmediate(checkerMask);
                Object.DestroyImmediate(sdfMockTex);
                Object.DestroyImmediate(bumpMockTex);
                Object.DestroyImmediate(readTex);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(sphereGo);
                Object.DestroyImmediate(camGo);
            }

            passedCount = 0;
            failedCount = 0;
            var sb = new StringBuilder();
            foreach (TestCaseResult r in results)
            {
                if (r.Passed) passedCount++;
                else failedCount++;
                string mark = r.Passed ? "[PASS]" : "[FAIL]";
                sb.AppendLine($"{mark} [{r.Category}] {r.ParameterName} (最大差分: {r.MaxPixelDiff:F3}, 平均差分: {r.MeanPixelDiff:F4}) - {r.Note}");
            }
            summaryReport = sb.ToString();
            return results;
        }

        private const HideFlags HideAndDontSave = HideFlags.HideAndDontSave;

        private static Texture2D CreateMockRampTexture()
        {
            var tex = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++)
            {
                float t = x / 63f;
                Color c = Color.Lerp(new Color(0.68f, 0.52f, 0.55f, 1f), Color.white, Mathf.SmoothStep(0.35f, 0.65f, t));
                for (int y = 0; y < 4; y++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateCheckerTexture()
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    bool on = ((x / 4) + (y / 4)) % 2 == 0;
                    tex.SetPixel(x, y, on ? Color.white : Color.black);
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSdfMockTexture()
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float u = x / 31f;
                    tex.SetPixel(x, y, new Color(u, u, u, 1f));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateBumpMockTexture()
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float nx = Mathf.Sin(x * 0.6f) * 0.4f + 0.5f;
                    float ny = Mathf.Cos(y * 0.6f) * 0.4f + 0.5f;
                    tex.SetPixel(x, y, new Color(nx, ny, 1f, 1f));
                }
            tex.Apply();
            return tex;
        }
    }
}
