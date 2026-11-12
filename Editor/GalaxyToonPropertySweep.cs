using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GalaxyToon.Editor
{
    /// <summary>
    /// インスペクターに出る全項目を 1 つずつ動かして、球の描画が変わるかを総当たりで確かめる(効いていない項目を探す)。
    /// 見出しごとに、その見出しの機能を入れた状態(トグル ON・色を不透明・マスクに仮のテクスチャ)を作り、項目を大きく動かして前後の画素を比べる。
    /// 変わらなかった項目を Console に出す。ステンシルと、別の部品(髪の影の RendererFeature など)が要る項目は対象外。
    /// </summary>
    public static class GalaxyToonPropertySweep
    {
        private const int Resolution = 128;
        private const float Threshold = 1.5f / 255f;

        // 単体の球では確かめられない項目(ステンシル、別の部品・カメラの設定が要るもの)
        private static readonly HashSet<string> Skip = new HashSet<string>
        {
            "_StencilRef", "_StencilComp", "_StencilPass", "_StencilFail", "_StencilZFail", "_StencilReadMask", "_StencilWriteMask",
            "_BakedSteps",                                   // ライトマップを焼いた背景だけ
            "_CastShadowAmount", "_ShadowTerminatorFade", "_FaceSdfIgnoreShadow", // 他の物が落とす影(シャドウマップ)が要る
            "_HairSway",                                     // GalaxyToonHairSway が毎フレーム入れる値と組み合わせて動く
            "_HairShadowCast", "_HairShadowReceive", "_HairShadowDistance", "_HairShadowBias", "_HairShadowSoftness", // GalaxyHairShadowFeature が要る
            "_OutlineNearClamp",                              // カメラが 1m より近いときだけ
            "_DissolveDirection", "_DissolveStart", "_DissolveWorld", // 消え方が「向き」のときだけ
            "_RefractionDispersion",                         // 後ろの景色(Opaque Texture)が要る
            "_GlitterShadeMask",                            // ラメは光の側でしか光らないので、単体の球では影の側に出ない
            "_OutlineVertexColor", "_OutlineVertexAlpha", "_VertexColorShadow", // 頂点カラーのあるメッシュが要る(球は白=変化なし)
            "_ReceiveShadowOffset",                          // 他の物が落とす影(シャドウマップ)が要る
            "_OutlineBoilFps", "_EmissionHueSpeed",          // 時間で動く(同じ時刻の 2 枚では比べられない)
            "_MatCapNoRoll",                                 // カメラを傾けたときだけ
            "_OutlineZOffset", "_ZOffsetMask",               // 深度だけ動く(前後に重なる物が要る)
            "_ContactShadow", "_ContactShadowStrength", "_ContactShadowLength",
            "_SelfShadowStrength", "_SelfShadowLength", "_SelfShadowThickness", // 凸の球は自分に影を落とさない(重なる物が要る)
        };

        // 項目を確かめるために入れるグローバル値(ふだんは部品が毎フレーム入れる物)。終わったら元に戻す
        private static readonly string[] Globals =
        {
            "_GalaxyAudio", "_GalaxySkirtShadow", "_GalaxySkirtShadowCenter", "_HeadForward", "_PerspectiveCenter",
            "_GalaxyLegStart0", "_GalaxyLegEnd0", "_GalaxyLegStart1", "_GalaxyLegEnd1", "_GalaxyVolumeA", "_GalaxyVolumeB",
        };

        [MenuItem("Tools/GalaxyToon/開発用/全項目の総当たりテスト(効いていない項目を探す)", false, 101)]
        public static void RunMenu()
        {
            string report = Run(-1);
            Debug.Log("[GalaxyToon 総当たり]\n" + report);
        }

        /// <summary>見出し section(-1 なら全部)を確かめて、効いていなかった項目の一覧を返す。</summary>
        public static string Run(int section)
        {
            Shader shader = Shader.Find("GalaxyShader/Toon");
            if (shader == null) return "GalaxyShader/Toon が見つかりません";

            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false; // 仮の水色表示で比べないように、その場でコンパイルする
            var origin = new Vector3(0f, -5000f, 0f);
            var camGo = new GameObject("__GalaxyToonSweepCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.transform.position = origin + new Vector3(0f, 0f, -2.5f);
            cam.transform.LookAt(origin);
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.hideFlags = HideFlags.HideAndDontSave;
            sphere.transform.position = origin;
            sphere.transform.rotation = Quaternion.Euler(15f, 30f, 0f);
            var sphereRenderer = sphere.GetComponent<MeshRenderer>();
            var rt = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
            var read = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            var checker = MakeTexture(0);
            var ramp = MakeTexture(1);
            var bump = MakeTexture(2);
            var gradient = MakeTexture(3);
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            sphere.GetComponent<MeshRenderer>().sharedMaterial = mat;

            Vector4 oldDir = Shader.GetGlobalVector("_AdvToonLightDir");
            Vector4 oldColor = Shader.GetGlobalVector("_AdvToonLightColor");
            float oldOff = Shader.GetGlobalFloat("_GalaxyToonOff");
            var oldGlobals = new Vector4[Globals.Length];
            for (int g = 0; g < Globals.Length; g++) oldGlobals[g] = Shader.GetGlobalVector(Globals[g]);
            float oldContacts = Shader.GetGlobalFloat("_SoftContactCount");
            // Volume の値は Galaxy Toon Volume Feature が毎フレーム入れ直すので、一時的な Volume で入れる(Feature が無いときはグローバル値で)
            var volumeGo = new GameObject("__GalaxyToonSweepVolume") { hideFlags = HideFlags.HideAndDontSave };
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile.hideFlags = HideFlags.HideAndDontSave;
            var volumeSettings = volume.sharedProfile.Add<GalaxyToonVolume>(true);
            volumeSettings.rim.Override(2f);
            volumeSettings.saturation.Override(-0.6f);
            volumeSettings.brightness.Override(0.3f);
            volume.enabled = false;
            Shader.SetGlobalFloat("_GalaxyToonOff", 0f);
            Shader.SetGlobalVector("_AdvToonLightDir", new Vector3(0.55f, 0.45f, -0.70f).normalized);
            Shader.SetGlobalVector("_AdvToonLightColor", new Vector4(1f, 1f, 1f, 1f));

            var lightDir = new Vector3(0.55f, 0.45f, -0.70f).normalized;
            var lightColor = new Vector4(1f, 1f, 1f, 1f);
            var ambient = RenderSettings.ambientProbe;
            var pointGo = new GameObject("__GalaxyToonSweepPoint") { hideFlags = HideFlags.HideAndDontSave };
            var point = pointGo.AddComponent<Light>();
            point.type = LightType.Point;
            point.range = 4f;
            point.intensity = 3f;
            point.color = new Color(0.3f, 0.6f, 1f);
            pointGo.transform.position = origin + new Vector3(-1.2f, 0.6f, -1.2f);
            point.enabled = false;

            Texture TextureFor(string name) =>
                name.Contains("Ramp") ? ramp : name.Contains("Bump") || name.Contains("Normal") ? bump : name.Contains("Sdf") || name.Contains("Thickness") ? gradient : (Texture)checker;

            Color[] Render()
            {
                Shader.SetGlobalVector("_AdvToonLightDir", lightDir);
                Shader.SetGlobalVector("_AdvToonLightColor", lightColor);
                GalaxyToonGUI.Apply(mat);
                cam.targetTexture = rt;
                cam.Render();
                cam.Render(); // 2 回描く: DX12 は新しいキーワードの組み合わせの初回の描画が間に合わないことがある
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
                read.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                return read.GetPixels();
            }

            // 見出しの機能を入れた状態
            void Context(string[] props)
            {
                GalaxyToonGUI.ResetAll(mat);
                mat.SetColor("_BaseColor", new Color(0.95f, 0.82f, 0.78f, 1f));
                mat.SetColor("_Shade1Tint", new Color(0.65f, 0.50f, 0.55f, 1f));
                mat.SetFloat("_Shade1Edge", 0.5f);
                mat.SetFloat("_Shade1Soft", 0.05f);
                mat.SetFloat("_LightSource", 0f);
                lightDir = new Vector3(0.55f, 0.45f, -0.70f).normalized;
                lightColor = new Vector4(1f, 1f, 1f, 1f);
                RenderSettings.ambientProbe = ambient;
                point.enabled = false;
                volume.enabled = false;
                sphereRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                sphereRenderer.SetPropertyBlock(null);
                mat.SetTextureScale("_BaseMap", Vector2.one);
                foreach (string g in Globals) Shader.SetGlobalVector(g, Vector4.zero);
                Shader.SetGlobalFloat("_SoftContactCount", 0f);
                foreach (string p in props)
                {
                    int i = shader.FindPropertyIndex(p);
                    if (i < 0) continue;
                    var type = shader.GetPropertyType(i);
                    if (type == ShaderPropertyType.Texture)
                    {
                        if (mat.GetTexture(p) == null) mat.SetTexture(p, TextureFor(p));
                    }
                    else if (type == ShaderPropertyType.Color)
                    {
                        Color c = mat.GetColor(p);
                        if (c.a <= 0f || c.r + c.g + c.b <= 0f) mat.SetColor(p, new Color(0.9f, 0.4f, 0.2f, 1f));
                    }
                    else if (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range)
                    {
                        bool toggle = p.StartsWith("_Use") || p == "_AnimeMode" || p == "_DepthRim" || p == "_CreviceAO" || p.EndsWith("Animate") ||
                                      p == "_Inflate" || p == "_SoftTouch" || p == "_HairMapOn";
                        Vector2 limits = type == ShaderPropertyType.Range ? shader.GetPropertyRangeLimits(i) : new Vector2(0f, 1f);
                        if (toggle) mat.SetFloat(p, 1f);
                        else if (Mathf.Abs(mat.GetFloat(p)) < 1e-6f && (p.Contains("Amount") || p.Contains("Receive") || p.Contains("Strength") || p.Contains("Width") ||
                                                                         p.Contains("Shading") || p.Contains("Sphere") || p.Contains("Gloss") || p.Contains("Wet")))
                            mat.SetFloat(p, Mathf.Lerp(limits.x, limits.y, 0.6f));
                    }
                }
            }

            var sb = new StringBuilder();
            int tested = 0, dead = 0;
            try
            {
                var sections = GalaxyToonGUI.Sections;
                for (int s = 0; s < sections.Length; s++)
                {
                    if (section >= 0 && s != section) continue;
                    var (title, props) = sections[s];
                    foreach (string p in props)
                    {
                        int i = shader.FindPropertyIndex(p);
                        if (i < 0)
                        {
                            sb.AppendLine($"{title} | {p} : シェーダーに無い");
                            continue;
                        }

                        if (Skip.Contains(p)) continue;
                        Context(props);
                        Setup(title, p, mat, checker, bump, ramp, origin, ref lightDir, ref lightColor, point);
                        if (p == "_AmbientHeadSplit")
                        {
                            // 前と後ろで違う環境光を、球にだけ直接入れる(シーンの環境光の設定に左右されない)
                            var sh = new SphericalHarmonicsL2();
                            sh.AddAmbientLight(new Color(0.2f, 0.2f, 0.25f));
                            sh.AddDirectionalLight(Vector3.back, new Color(1f, 0.6f, 0.3f), 1.5f);
                            var block = new MaterialPropertyBlock();
                            block.CopySHCoefficientArraysFrom(new[] { sh });
                            sphereRenderer.lightProbeUsage = LightProbeUsage.CustomProvided;
                            sphereRenderer.SetPropertyBlock(block);
                        }
                        if (p == "_VolumeReceive")
                        {
                            volume.enabled = true;
                            Shader.SetGlobalVector("_GalaxyVolumeA", new Vector4(1f, 0.3f, -0.5f, 0.2f));
                            Shader.SetGlobalVector("_GalaxyVolumeB", new Vector4(0.6f, 0.7f, 1f, 0.5f));
                        }
                        Color[] before = Render();
                        string change = Change(shader, mat, i, p, TextureFor);
                        Color[] after = Render();
                        tested++;
                        float diff = MaxDiff(before, after);
                        if (diff < Threshold)
                        {
                            dead++;
                            sb.AppendLine($"{title} | {p} ({change}) 差 {diff:0.0000}");
                        }
                    }
                }
            }
            finally
            {
                Shader.SetGlobalVector("_AdvToonLightDir", oldDir);
                Shader.SetGlobalVector("_AdvToonLightColor", oldColor);
                Shader.SetGlobalFloat("_GalaxyToonOff", oldOff);
                for (int g = 0; g < Globals.Length; g++) Shader.SetGlobalVector(Globals[g], oldGlobals[g]);
                Shader.SetGlobalFloat("_SoftContactCount", oldContacts);
                Object.DestroyImmediate(volume.sharedProfile);
                Object.DestroyImmediate(volumeGo);
                ShaderUtil.allowAsyncCompilation = async;
                RenderSettings.ambientProbe = ambient;
                Object.DestroyImmediate(pointGo);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(sphere);
                Object.DestroyImmediate(mat);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(read);
                Object.DestroyImmediate(checker);
                Object.DestroyImmediate(ramp);
                Object.DestroyImmediate(bump);
                Object.DestroyImmediate(gradient);
            }

            return $"確かめた項目 {tested} / 描画が変わらなかった項目 {dead}\n" + sb;
        }

        // 見出し・項目ごとに、効き目が見える前提を足す(光の向き・色、半透明、元の値など)
        private static void Setup(string title, string p, Material m, Texture checker, Texture bump, Texture ramp, Vector3 origin,
            ref Vector3 lightDir, ref Vector4 lightColor, Light point)
        {
            var back = new Vector3(0.3f, 0.35f, 0.9f).normalized; // カメラの向こうからの光
            if (title.StartsWith("金属"))
            {
                m.SetFloat("_Metallic", 0.6f);
                m.SetFloat("_Smoothness", 0.6f);
                m.SetFloat("_ClearCoat", 0.7f);
            }
            else if (title.StartsWith("ラメ"))
            {
                m.SetFloat("_RenderMode", 2f);
                m.SetColor("_BaseColor", new Color(0.95f, 0.82f, 0.78f, 0.6f));
            }
            else if (title.StartsWith("トライプラナー")) m.SetTexture("_BaseMap", checker);
            else if (title.StartsWith("ライト"))
            {
                lightColor = new Vector4(0.5f, 0.12f, 0.12f, 1f);
                var sh = new SphericalHarmonicsL2();
                sh.AddAmbientLight(new Color(0.2f, 0.5f, 0.3f));
                RenderSettings.ambientProbe = sh;
                point.enabled = true;
            }
            else if (title.StartsWith("影"))
            {
                m.SetFloat("_ShadowRampRows", 4f);
                if (p == "_ShadowRampRow") m.SetTexture("_RampIdMap", null); // 部位マップがあると段はそちらで決まる
                if (p.StartsWith("_ShadowPattern") && p != "_ShadowPattern") m.SetFloat("_ShadowPattern", 1f);
                if (p.StartsWith("_Manga") && p != "_MangaStyle") m.SetFloat("_MangaStyle", 1f); // ハッチング
                if (p == "_SkirtShadowReceive")
                {
                    // 球の上の方を腰、下の方を裾とするスカートの影(x=裾の高さ y=帯の幅 z=強さ w=半径、中心: x・z と腰の高さ)
                    Shader.SetGlobalVector("_GalaxySkirtShadow", new Vector4(origin.y - 0.2f, 0.3f, 1f, 1f));
                    Shader.SetGlobalVector("_GalaxySkirtShadowCenter", new Vector4(origin.x, origin.z, origin.y + 0.6f, 0f));
                }
            }
            else if (title.StartsWith("アニメ"))
            {
                m.SetTexture("_BaseMap", checker);
                m.SetTexture("_BumpMap", bump);
                m.SetFloat("_Metallic", 0.6f);
                m.SetFloat("_Smoothness", 0.7f);
                if (p != "_AnimeFlatTexture") m.SetFloat("_AnimeFlatTexture", 0.8f); // 平らな色を使うときだけ、ぼかしの段(_AnimeFlatMip)が効く
                if (p == "_AnimeFlatMip") m.SetFloat("_AnimeFlatMip", 1f); // 市松は 4 段より上だとどの段も同じ灰色になる
                lightColor = p == "_AnimeFlatLight" ? new Vector4(0.25f, 0.2f, 0.2f, 1f) : new Vector4(1.8f, 1.6f, 1.4f, 1f); // 暗い光を持ち上げる
            }
            else if (title.StartsWith("SSS"))
            {
                if (p != "_SssFrontScatter") lightDir = back; // 正面の光での透けだけは正面から
            }
            else if (title.StartsWith("顔"))
            {
                if (p == "_FaceShading" || p == "_FaceSphere") m.SetFloat("_UseFaceSdf", 0f);
                m.SetVector("_FaceCenter", new Vector4(origin.x, origin.y + 0.3f, origin.z, 1f));
                m.SetVector("_FaceForward", new Vector4(0f, 0f, -1f, 0f)); // 顔をカメラ(と光)の側へ向ける
                m.SetVector("_FaceRight", new Vector4(-1f, 0f, 0f, 0f));
            }
            else if (title.StartsWith("瞳"))
            {
                m.SetVector("_EyeRect1", new Vector4(0.3f, 0.3f, 0.7f, 0.7f));
                m.SetVector("_EyeRect2", new Vector4(0.05f, 0.3f, 0.25f, 0.7f));
                m.SetTexture("_BaseMap", checker);
            }
            else if (title.StartsWith("頬染め")) m.SetVector("_BlushUV", new Vector4(0.45f, 0.5f, 0.7f, 0.5f));
            else if (title.StartsWith("網タイツ")) m.SetTexture("_BaseMap", checker);
            else if (title.StartsWith("ハイライト"))
            {
                m.SetFloat("_SpecularSize", 0.3f);
                if (p == "_SpecularSoft") m.SetTexture("_SpecularRampTex", null); // ランプがあると光の形はランプで決まる
            }
            else if (title.StartsWith("異方性"))
            {
                m.SetTexture("_SpecularRampTex", ramp);
                if (p == "_RingTex" || p == "_RingOffset") m.SetFloat("_RingMode", 1f); // マットキャップの輪
                if (p == "_RingViewShift") m.SetFloat("_RingMode", 2f);                 // UV2 の帯
            }
            else if (title.StartsWith("逆光")) lightDir = back;
            else if (title.StartsWith("発光") || title.StartsWith("レイヤー"))
            {
                foreach (string n in new[] { "_EmissionScroll", "_Emission2Scroll", "_Layer1Scroll", "_Layer2Scroll", "_Layer3Scroll" })
                    m.SetVector(n, new Vector4(0.37f, 0.21f, 0f, 13f));
                // 明滅は、明滅の項目を確かめるときだけ入れる(ほかの項目で明滅の消えている時刻に当たると、何を変えても見た目が変わらない)
                if (p.Contains("Blink") || p.Contains("Fade"))
                    foreach (string n in new[] { "_EmissionFlicker", "_Emission2Blink", "_Layer1Blink", "_Layer2Blink", "_Layer3Blink" })
                        m.SetFloat(n, 0.37f);
                else
                    foreach (string n in new[] { "_EmissionFlicker", "_Emission2Blink", "_Layer1Blink", "_Layer2Blink", "_Layer3Blink" })
                        if (m.HasProperty(n)) m.SetFloat(n, 0f);
            }
            else if (title.StartsWith("輪郭線"))
            {
                lightColor = new Vector4(0.4f, 0.2f, 0.9f, 1f);
                if (p == "_OutlineCenterRadius") m.SetFloat("_OutlineCenterBoost", 2f);
            }
            else if (title.StartsWith("トゥーンの強化")) SetupToonPlus(p, m, checker, ramp, origin, ref lightColor, point);
            else if (title.StartsWith("ふくらむ"))
            {
                m.SetVector("_InflateCenter", new Vector4(origin.x, origin.y, origin.z, 0.6f)); // 丸さは中心と半径があるときだけ効く
                m.SetFloat("_InflateAmount", 0.04f);
                if (p == "_LegPush")
                {
                    // 球を縦に貫く脚(w=半径)
                    Shader.SetGlobalVector("_GalaxyLegStart0", new Vector4(origin.x + 0.25f, origin.y - 0.8f, origin.z, 0.2f));
                    Shader.SetGlobalVector("_GalaxyLegEnd0", new Vector4(origin.x + 0.25f, origin.y + 0.8f, origin.z, 0.2f));
                }
            }
            else if (title.StartsWith("触れる"))
            {
                // 球の手前に触れる指(カプセル。A.xyz・B.xyz が両端、A.w が半径、B.w が強さ)
                var a = new Vector4[32];
                var b = new Vector4[32];
                a[0] = new Vector4(origin.x - 0.15f, origin.y + 0.1f, origin.z - 0.47f, 0.12f);
                b[0] = new Vector4(origin.x + 0.15f, origin.y + 0.1f, origin.z - 0.47f, 1f);
                Shader.SetGlobalVectorArray("_SoftContactA", a);
                Shader.SetGlobalVectorArray("_SoftContactB", b);
                Shader.SetGlobalFloat("_SoftContactCount", 1f);
            }
            else if (title.StartsWith("ディゾルブ") && p == "_DissolveNoiseAmount")
            {
                // ノイズの強さは「向き」で消すときの境目の乱れ
                m.SetFloat("_DissolveMode", 1f);
                m.SetVector("_DissolveDirection", new Vector4(0f, 1f, 0f, 1f));
                m.SetFloat("_DissolveStart", -0.5f);
                m.SetFloat("_DissolveAmount", 0.5f);
            }
            else if (title.StartsWith("カメラが近い"))
            {
                m.SetFloat("_NearFadeStart", 2.2f);
                m.SetFloat("_NearFadeEnd", 2.9f);
                if (p == "_NearFadeStart") m.SetFloat("_NearFadeEnd", 3f);
            }
        }

        // トゥーンの強化: 項目ごとに、効き目が見える前提(明るい色の光・追加ライト・マットキャップ・発光・音の大きさなど)
        private static void SetupToonPlus(string p, Material m, Texture checker, Texture ramp, Vector3 origin, ref Vector4 lightColor, Light point)
        {
            switch (p)
            {
                case "_LightNormalize":
                case "_LightSoftLimit":
                    lightColor = new Vector4(2.5f, 1.5f, 1f, 1f); // 1 を超える色の光
                    break;
                case "_AmbientHeadSplit":
                    var sh = new SphericalHarmonicsL2();
                    sh.AddDirectionalLight(Vector3.back, new Color(1f, 0.6f, 0.3f), 1.5f); // 前と後ろで違う環境光
                    RenderSettings.ambientProbe = sh;
                    Shader.SetGlobalVector("_HeadForward", new Vector4(0f, 0f, -1f, 1f));
                    m.SetFloat("_AmbientStrength", 1f); // 既定は 0(環境光を使わない)
                    lightColor = new Vector4(0.3f, 0.3f, 0.3f, 1f); // 白い光(1)だと、足した環境光が明るさの上限で切れて見えない
                    break;
                case "_AddLightIgnoreNormal":
                case "_AddLightBorder":
                case "_AddLightMax":
                case "_AddLightMono":
                    m.SetFloat("_AdditionalLightStrength", 1f);
                    point.enabled = true;
                    break;
                case "_RimDepthSoft":
                    m.SetFloat("_DepthRim", 1f);
                    m.SetColor("_RimColor", Color.white);
                    break;
                case "_MatCapViewFix":
                case "_MatCapMask":
                    m.SetTexture("_MatCapTex", ramp);
                    m.SetColor("_MatCapColor", Color.white);
                    break;
                case "_IridescenceShift":
                    m.SetFloat("_IridescenceStrength", 0.8f);
                    m.SetFloat("_IridescenceShift", 0.5f); // 0→1 は色相がひと回りして同じ色になるので、半分から動かす
                    break;
                case "_PerspectiveRemoval":
                    Shader.SetGlobalVector("_PerspectiveCenter", new Vector4(origin.x, origin.y, origin.z, 1f));
                    break;
                case "_RampDayNight":
                case "_RampNightAmount":
                    m.SetTexture("_ShadowRampTex", ramp); // 上半分が昼、下半分が夜
                    if (p == "_RampNightAmount") m.SetFloat("_RampDayNight", 1f);
                    break;
                case "_AnisoCenterShape":
                    m.SetColor("_AnisoColor", Color.white);
                    break;
                case "_SpecularAA":
                    m.SetColor("_SpecularColor", Color.white);
                    m.SetFloat("_SpecularSize", 0.3f);
                    m.SetFloat("_SpecularSoft", 0f);
                    break;
                case "_TexLineKeep":
                    m.SetTexture("_BaseMap", checker);
                    m.SetTextureScale("_BaseMap", new Vector2(16f, 16f)); // 画面で縮んで、粗いミップを引くように
                    break;
                case "_EmissionViewColor":
                    m.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.2f));
                    break;
                case "_OutlineFadeStart":
                case "_OutlineFadeEnd":
                    m.SetFloat("_OutlineFadeStart", 1f); // カメラ(2.5m)が、薄くなり始めと消える所の間に来るように
                    m.SetFloat("_OutlineFadeEnd", 4f);
                    break;
                case "_AudioBand":
                case "_AudioEmission":
                case "_AudioRim":
                case "_AudioOutline":
                    Shader.SetGlobalVector("_GalaxyAudio", new Vector4(1f, 0.5f, 0.25f, 0.8f));
                    m.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.2f));
                    m.SetColor("_RimColor", Color.white);
                    if (p == "_AudioBand") m.SetFloat("_AudioEmission", 4f);
                    break;
            }
        }

        // 項目を大きく動かす(範囲なら反対の端、色は別の色、テクスチャは付け外し)
        private static string Change(Shader shader, Material mat, int i, string p, System.Func<string, Texture> textureFor)
        {
            switch (shader.GetPropertyType(i))
            {
                case ShaderPropertyType.Texture:
                    Texture current = mat.GetTexture(p);
                    mat.SetTexture(p, current == null ? textureFor(p) : null);
                    return current == null ? "テクスチャを付ける" : "テクスチャを外す";
                case ShaderPropertyType.Color:
                    Color c = mat.GetColor(p);
                    mat.SetColor(p, new Color(1f - c.r * 0.8f, 0.2f + c.b * 0.5f, 1f - c.g, c.a));
                    return "色";
                case ShaderPropertyType.Vector:
                    mat.SetVector(p, mat.GetVector(p) + new Vector4(0.3f, -0.25f, 0.2f, 0.15f));
                    return "ベクトル";
                default:
                    float v = mat.GetFloat(p), nv;
                    if (shader.GetPropertyType(i) == ShaderPropertyType.Range)
                    {
                        Vector2 lim = shader.GetPropertyRangeLimits(i);
                        nv = v - lim.x < (lim.y - lim.x) * 0.5f ? lim.y : lim.x;
                    }
                    else nv = v == 0f ? 1f : v == 1f ? 0f : v * 2f;
                    mat.SetFloat(p, nv);
                    return $"{v:0.###}→{nv:0.###}";
            }
        }

        private static float MaxDiff(Color[] a, Color[] b)
        {
            float max = 0f;
            for (int i = 0; i < a.Length; i++)
                max = Mathf.Max(max, Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b))));
            return max;
        }

        private static Texture2D MakeTexture(int kind)
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, kind == 2) { wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (n - 1f), v = y / (n - 1f);
                px[y * n + x] = kind switch
                {
                    0 => (x / 8 + y / 8) % 2 == 0 ? new Color(1f, 1f, 1f, 1f) : new Color(0.3f, 0.3f, 0.3f, 0.5f), // 市松(マスク・模様)
                    1 => new Color(u, u * 0.6f + 0.2f * v, 1f - u, 1f),                                              // ランプ
                    2 => new Color(Mathf.Sin(u * 25f) * 0.2f + 0.5f, Mathf.Cos(v * 25f) * 0.2f + 0.5f, 1f, 1f),        // 法線
                    _ => new Color(u, u, u, 1f),                                                                      // グラデーション(SDF・厚み)
                };
            }

            t.SetPixels(px);
            t.Apply(true);
            return t;
        }
    }
}
