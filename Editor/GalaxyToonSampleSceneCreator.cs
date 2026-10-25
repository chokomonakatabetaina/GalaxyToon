using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GalaxyToon.Editor
{
    /// <summary>機能ごとに球を1つずつ並べたサンプルシーンを作る。マテリアル・テクスチャも Samples フォルダに作る(作り直すと上書き)。
    /// 動く機能(涙・糸・転送・髪の揺れ・毛・雨)は再生すると動く。</summary>
    public static class GalaxyToonSampleSceneCreator
    {
        private const string ShaderName = "GalaxyShader/Toon";
        private const int Columns = 8;
        private const float StepX = 1.5f, StepY = 1.9f;
        // 瞳の範囲(カメラ側の UV からのずれ)。瞳のテクスチャと _EyeRect で同じ値を使う
        private const float EyeX = 0.045f, EyeY = 0.08f, EyeW = 0.022f, EyeH = 0.05f;

        private sealed class Sample
        {
            public string Jp, En;
            public Action<Material> Setup;
            public Action<GameObject, Material> Build; // コンポーネントを付ける(無ければ球だけ)
        }

        private static string root, matDir, texDir;
        private static Mesh sphereMesh;
        private static Vector2 front;
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        [MenuItem("Tools/GalaxyToon/サンプルシーンを作る (機能ごとの球)")]
        public static void CreateMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string scene = Create();
            Debug.Log($"[GalaxyToon] サンプルシーンを作りました: {scene}\n再生すると、涙・糸・転送・髪の揺れ・毛・雨が動きます。");
        }

        /// <summary>シーンを作って保存し、パスを返す。</summary>
        public static string Create()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException(ShaderName + " が見つかりません。");
            // 配布フォルダの場所はシェーダから決める(フォルダごと移しても動く)
            root = Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(shader))).Replace('\\', '/');
            // パッケージ(Packages/)として入れたときは中に書けないので、Assets 側に作る
            string samples = root.StartsWith("Packages/") ? "Assets/GalaxyToon Samples" : root + "/Samples";
            matDir = samples + "/Materials";
            texDir = samples + "/Textures";
            foreach (string dir in new[] { samples, matDir, texDir })
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\', '/'), Path.GetFileName(dir));

            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temp);
            front = sphereMesh.uv[NearestVertex(new Vector3(0f, 0f, -0.5f))]; // カメラ側(-Z)の UV。瞳・頬の位置の基準
            textures.Clear();
            MakeTextures();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rows = Rows();
            var viewer = BuildEnvironment(rows.Count).gameObject.AddComponent<GalaxyToonSampleViewer>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int r = 0; r < rows.Count; r++)
            {
                var (title, list) = rows[r];
                float y = -r * StepY;
                Label(null, title, new Vector3(-StepX * 0.6f, y, 0f), font, 0.06f, TextAnchor.MiddleRight, new Color(1f, 0.9f, 0.6f));
                for (int i = 0; i < list.Count; i++)
                {
                    Sample s = list[i];
                    var go = new GameObject(s.En);
                    go.transform.position = new Vector3(i * StepX, y, 0f);
                    Material m = NewMaterial(shader, $"{r + 1:00}_{i + 1:00}_{Safe(s.En)}", i);
                    s.Setup?.Invoke(m);
                    GalaxyToonGUI.Apply(m);
                    if (s.Build != null) s.Build(go, m);
                    else AddSphere(go, m);
                    Label(go.transform, s.Jp + "\n" + s.En, new Vector3(0f, -0.6f, 0f), font, 0.035f, TextAnchor.UpperCenter, Color.white);
                    viewer.entries.Add(new GalaxyToonSampleViewer.Entry { target = go.transform, title = s.Jp + " / " + s.En });
                }
            }
            AssetDatabase.SaveAssets();
            string path = samples + "/GalaxyToon_FeatureSamples.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ---------------------------------------------------------------- 並べる機能

        private static List<(string, List<Sample>)> Rows()
        {
            var eyes = new Action<Material>(m =>
            {
                m.SetTexture("_BaseMap", Tex("Eye"));
                for (int e = 0; e < 2; e++)
                {
                    float cx = front.x + (e == 0 ? -EyeX : EyeX), cy = front.y + EyeY;
                    m.SetVector(e == 0 ? "_EyeRect1" : "_EyeRect2", new Vector4(cx - EyeW, cy - EyeH, cx + EyeW, cy + EyeH));
                }
            });
            Color skin = new Color(1f, 0.88f, 0.8f);
            Color liquidBg = new Color(0.18f, 0.32f, 0.68f); // 液体の見本は白い液が見えるように濃い青の地にする

            return new List<(string, List<Sample>)>
            {
                ("基本\nBasic", new List<Sample>
                {
                    S("初期値", "Default", null),
                    S("ノーマルマップ", "Normal Map", m => { m.SetTexture("_BumpMap", Tex("Normal")); F(m, "_BumpScale", 1f); }),
                    S("色調補正", "Color Adjust", m => { m.SetTexture("_BaseMap", Tex("Checker")); F(m, "_HueShift", 0.35f); F(m, "_Saturation", 1.6f); F(m, "_Brightness", 1.1f); }),
                    S("金属・映り込み", "Metal / Reflection", m => { F(m, "_Metallic", 1f); F(m, "_Smoothness", 0.75f); F(m, "_ReflectionStrength", 1f); F(m, "_ReflectionSteps", 3f); }),
                    S("ラメ", "Glitter", m => { P(m, "ラメ"); F(m, "_GlitterScale", 60f); F(m, "_GlitterSize", 0.4f); }), // 球は遠いので粒を大きく
                    S("屈折 (宝石)", "Refraction (Gem)", m => P(m, "宝石(屈折)")),
                    S("トライプラナー", "Triplanar", m => { m.SetTexture("_BaseMap", Tex("Checker")); F(m, "_UseTriplanar", 1f); F(m, "_TriplanarScale", 2f); }),
                    S("レース (切り抜き)", "Lace (Cutout)", m => { P(m, "レース"); m.SetTexture("_BaseMap", Tex("Lace")); }),
                }),
                ("影・ライト\nShadow / Light", new List<Sample>
                {
                    S("影 2段", "2-Step Shadow", m => { C(m, "_Shadow2ndColor", new Color(0.55f, 0.5f, 0.7f, 1f)); F(m, "_Shadow2ndBorder", 0.3f); F(m, "_Shadow2ndBlur", 0.05f); }),
                    S("影 3段", "3-Step Shadow", m => { C(m, "_Shadow2ndColor", new Color(0.6f, 0.55f, 0.75f, 1f)); F(m, "_Shadow2ndBorder", 0.35f); C(m, "_Shadow3rdColor", new Color(0.35f, 0.3f, 0.5f, 1f)); F(m, "_Shadow3rdBorder", 0.15f); }),
                    S("影のランプ", "Shadow Ramp", m => { m.SetTexture("_ShadowRampTex", Ramp("Shadow")); F(m, "_ShadowRampRows", 4f); F(m, "_ShadowRampRow", 1f); }),
                    S("ハイライトのランプ", "Specular Ramp", m => { C(m, "_SpecularColor", new Color(1f, 1f, 1f, 0.8f)); F(m, "_SpecularSize", 0.15f); m.SetTexture("_SpecularRampTex", Ramp("Specular")); F(m, "_ShadowRampRows", 4f); }),
                    S("アニメ塗り", "Anime Shading", m => { C(m, "_BaseColor", skin); P(m, "アニメ塗り"); F(m, "_OutlineWidth", 0.08f); }),
                    S("顔の陰影を平らに", "Flat Face Shading", m => { F(m, "_FaceShading", 0.8f); F(m, "_FaceSphere", 1f); }),
                    S("SDF 顔影", "Face SDF Shadow", m => { m.SetTexture("_FaceSdfTex", Tex("FaceSdf")); F(m, "_UseFaceSdf", 1f); F(m, "_SdfFollowHead", 0f); }),
                    S("シーンのライトを受ける", "Scene Lights", m => P(m, "プラスチック"), (go, m) => { AddSphere(go, m); PointLight(go, new Color(1f, 0.3f, 0.2f)); }),
                }),
                ("ハイライト・輪郭\nHighlight / Outline", new List<Sample>
                {
                    S("ハイライト", "Specular", m => { C(m, "_SpecularColor", new Color(1f, 1f, 1f, 0.8f)); F(m, "_SpecularSize", 0.08f); F(m, "_SpecularBlur", 0.02f); }),
                    S("マットキャップ", "MatCap", m => { m.SetTexture("_MatCapTex", Tex("MatCap")); C(m, "_MatCapColor", Color.white); }),
                    S("マットキャップ2", "MatCap 2", m => { m.SetTexture("_MatCap2Tex", Tex("MatCap")); C(m, "_MatCap2Color", new Color(0.4f, 0.8f, 1f, 1f)); }),
                    S("異方性 (天使の輪)", "Anisotropic", m => { C(m, "_BaseColor", new Color(0.35f, 0.25f, 0.2f)); C(m, "_AnisoColor", new Color(1f, 0.95f, 0.85f, 0.8f)); }),
                    S("逆光", "Backlight", m => C(m, "_BacklightColor", new Color(1f, 0.6f, 0.4f, 1f))),
                    S("リムライト", "Rim Light", m => { C(m, "_RimColor", new Color(0.6f, 0.9f, 1f, 1f)); F(m, "_RimBorder", 0.5f); }),
                    S("深度リム", "Depth Rim", m => { C(m, "_RimColor", Color.white); F(m, "_DepthRim", 1f); }),
                    S("輪郭線", "Outline", m => { F(m, "_OutlineWidth", 0.3f); C(m, "_OutlineColor", new Color(0.15f, 0.1f, 0.2f, 1f)); }),
                }),
                ("発光・レイヤー\nEmission / Layers", new List<Sample>
                {
                    S("発光 (明滅)", "Emission (Blink)", m => { C(m, "_EmissionColor", new Color(0.2f, 1.2f, 2f, 1f)); F(m, "_EmissionBlink", 1f); }),
                    S("発光 (流れる)", "Emission (Scroll)", m => { m.SetTexture("_EmissionMap", Tex("Stripes")); C(m, "_EmissionColor", new Color(2f, 0.6f, 0.2f, 1f)); F(m, "_EmissionAnimate", 1f); V(m, "_EmissionScroll", new Vector4(0f, 0.3f, 0f, 0f)); }),
                    S("発光レイヤー2", "Emission Layer 2", m => { m.SetTexture("_Emission2Map", Tex("Stars")); C(m, "_Emission2Color", new Color(2f, 2f, 0.6f, 1f)); F(m, "_Emission2Animate", 1f); V(m, "_Emission2Scroll", new Vector4(0.05f, 0f, 0f, 0f)); }),
                    S("レイヤー1 (流れる)", "Layer 1 (Scroll)", m => { m.SetTexture("_Layer1Tex", Tex("Stripes")); C(m, "_Layer1Color", new Color(0.3f, 0.5f, 1f, 1f)); F(m, "_Layer1Animate", 1f); V(m, "_Layer1Scroll", new Vector4(0.1f, 0f, 0f, 0f)); }),
                    S("レイヤー2 (明滅)", "Layer 2 (Blink)", m => { m.SetTexture("_Layer2Tex", Tex("Stars")); C(m, "_Layer2Color", new Color(1f, 0.4f, 0.8f, 1f)); F(m, "_Layer2Blink", 1.5f); }),
                    S("レイヤー3 (光る)", "Layer 3 (Unlit)", m => { m.SetTexture("_Layer3Tex", Tex("Checker")); C(m, "_Layer3Color", new Color(0.4f, 1f, 0.6f, 0.6f)); F(m, "_Layer3Unlit", 1f); }),
                    S("マグマ", "Magma", m => { P(m, "マグマ"); m.SetTexture("_EmissionMap", Tex("Stars")); }),
                    S("発光パネル", "Emissive Panel", m => P(m, "発光パネル")),
                }),
                ("濡れ・汗・液体\nWet / Sweat / Liquid", new List<Sample>
                {
                    S("濡れ", "Wet", m => { C(m, "_BaseColor", liquidBg); P(m, "肌 濡れ"); }),
                    S("汗", "Sweat", m => { C(m, "_BaseColor", liquidBg); P(m, "汗"); }),
                    S("流れる汗", "Flowing Sweat", m => { C(m, "_BaseColor", liquidBg); P(m, "流れる汗"); }),
                    S("液体", "Liquid", m => { C(m, "_BaseColor", liquidBg); P(m, "肌 液体(白)"); F(m, "_LiquidAmount", 1f); F(m, "_LiquidFlow", 0.3f); F(m, "_LiquidScale", 12f); }),
                    S("ネバネバ", "Sticky", m => { C(m, "_BaseColor", liquidBg); P(m, "ネバネバ"); }),
                    S("泡", "Foam", m => { C(m, "_BaseColor", liquidBg); P(m, "泡"); }),
                    S("雨", "Rain", m => { C(m, "_BaseColor", liquidBg); P(m, "雨"); }),
                    S("雨 染み込み", "Rain Soak", m => { C(m, "_BaseColor", liquidBg); P(m, "雨 染み込み"); }),
                }),
                ("汚れ・質感\nDirt / Surface", new List<Sample>
                {
                    S("汚れ・すす", "Soot", m => P(m, "汚れ・すす")),
                    S("泥汚れ", "Mud", m => P(m, "泥汚れ")),
                    S("傷", "Scratches", m => { C(m, "_BaseColor", new Color(0.35f, 0.4f, 0.5f)); P(m, "傷"); }),
                    S("髪 濡れ", "Wet Hair", m => { C(m, "_BaseColor", new Color(0.4f, 0.3f, 0.25f)); P(m, "髪 濡れ"); }),
                    S("服 ラテックス", "Latex", m => { C(m, "_BaseColor", new Color(0.15f, 0.1f, 0.2f)); P(m, "服 ラテックス"); }),
                    S("水", "Water", m => P(m, "水")),
                    S("ペットボトル", "Clear Plastic", m => P(m, "ペットボトル")),
                    S("ダイヤモンド", "Diamond", m => P(m, "ダイヤモンド")),
                }),
                ("瞳・顔・髪\nEyes / Face / Hair", new List<Sample>
                {
                    S("瞳 くっきり", "Eyes Crisp", m => { eyes(m); P(m, "瞳 くっきり"); }),
                    S("瞳 うるうる", "Eyes Teary", m => { eyes(m); P(m, "瞳 うるうる"); }),
                    S("瞳 きらめき", "Eyes Sparkle", m => { eyes(m); P(m, "瞳 きらめき"); }),
                    S("照れ顔", "Blush", m => { C(m, "_BaseColor", skin); V(m, "_BlushUV", new Vector4(front.x - 0.07f, front.y - 0.03f, front.x + 0.07f, front.y - 0.03f)); P(m, "照れ顔"); F(m, "_BlushSize", 0.05f); }),
                    S("涙", "GalaxyTears", m => C(m, "_BaseColor", skin), BuildTears),
                    S("髪の影", "Hair Shadow", m => { C(m, "_BaseColor", skin); P(m, "髪の影を受ける"); F(m, "_HairShadowDistance", 12f); }, BuildHairShadow),
                    S("髪の揺れ", "Hair Sway", m => { C(m, "_BaseColor", new Color(0.45f, 0.3f, 0.25f)); F(m, "_HairSway", 1.5f); }, BuildHairSway),
                    S("毛", "GalaxyToonFur", m => { C(m, "_BaseColor", new Color(0.9f, 0.75f, 0.55f)); F(m, "_OutlineWidth", 0f); }, BuildFur), // 輪郭線は毛の中に透けるので外す
                }),
                ("演出\nEffects", new List<Sample>
                {
                    S("ディゾルブ", "Dissolve", m => { F(m, "_UseDissolve", 1f); F(m, "_DissolveAmount", 0.45f); C(m, "_DissolveEdgeColor", new Color(3f, 1.2f, 0.3f, 1f)); F(m, "_DissolveEdgeWidth", 0.05f); }),
                    S("転送", "GalaxyTeleport", m => C(m, "_BaseColor", new Color(0.7f, 0.85f, 1f)), BuildTeleport),
                    S("糸", "GalaxyStickyStrings", m => C(m, "_BaseColor", skin), BuildStrings),
                    S("雨の量を動かす", "GalaxyToonEffects", m => { P(m, "雨"); F(m, "_RainAmount", 0f); }, BuildRainEffects),
                    S("近づくと透ける", "Near Fade", m => { P(m, "近づくと透ける"); F(m, "_NearFadeStart", 1.2f); F(m, "_NearFadeEnd", 4f); }),
                    S("ステンシル", "Stencil", m => { C(m, "_BaseColor", new Color(0.4f, 0.3f, 0.25f)); F(m, "_StencilRef", 1f); F(m, "_StencilComp", (float)CompareFunction.NotEqual); }, BuildStencil),
                    S("左 Toon / 右 Lit", "Toon vs URP Lit", m => { C(m, "_BaseColor", skin); P(m, "肌"); }, BuildLitCompare),
                    S("白い液 どろどろ", "GalaxyTears (viscous)", m => C(m, "_BaseColor", liquidBg), BuildWhiteDrip),
                }),
            };
        }

        // ---------------------------------------------------------------- コンポーネントの見本

        private static void BuildTears(GameObject go, Material m)
        {
            var tears = AddTears(go, m, AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/GalaxyToon_TearDrop.mat"), new Vector3(-0.15f, 0.12f, -0.47f), new Vector3(0.15f, 0.12f, -0.47f));
            // 球は顔より大きいので、涙も合わせて大きくする
            tears.speed = 0.15f; tears.streakRadius = 0.006f; tears.dropRadius = 0.012f; tears.surfaceOffset = 0.003f; tears.climb = 0.01f; tears.interval = 1.5f;
        }

        private static void BuildWhiteDrip(GameObject go, Material m)
        {
            // 白く濁った、粘る液。上から伝って下でたまり、糸を引いてゆっくり垂れ落ちる(GalaxyTears の粘り)
            Material liquid = NewMaterial(m.shader, "White_Liquid", 7);
            P(liquid, "涙・しずく");
            F(liquid, "_RenderMode", 0f); // 濁った白なので不透明
            C(liquid, "_BaseColor", new Color(0.97f, 0.97f, 0.94f, 1f));
            C(liquid, "_ShadowColor", new Color(0.9f, 0.9f, 0.94f, 1f)); // 濁った液は光を通すので、影でも白いまま
            F(liquid, "_LightMinLimit", 0.9f);
            F(liquid, "_OutlineWidth", 0f); // 不透明にすると輪郭線が付き、細い筋が黒く見える
            GalaxyToonGUI.Apply(liquid);
            var drip = AddTears(go, m, liquid, new Vector3(-0.1f, 0.3f, -0.38f), new Vector3(0.14f, 0.25f, -0.4f));
            // 太い筋で途切れずに伝う(止まっては進む・次の液が重なると粒が並んで見えるので、間隔を空ける)
            drip.viscosity = 0.9f; drip.strandLength = 0.3f; drip.speed = 0.1f; drip.stopAndGo = 0.15f; drip.poolTime = 1.2f; drip.poolSize = 1.6f;
            drip.streakRadius = 0.022f; drip.dropRadius = 0.032f; drip.surfaceOffset = 0.006f; drip.climb = 0.02f; drip.interval = 5f; drip.fadeTime = 3f;
        }

        /// <summary>球を「顔」(スキンメッシュ)にして、指定の場所(ローカル)から GalaxyTears を流す。</summary>
        private static GalaxyTears AddTears(GameObject go, Material skin, Material liquid, Vector3 from1, Vector3 from2)
        {
            var face = new GameObject("Face").AddComponent<SkinnedMeshRenderer>();
            face.transform.SetParent(go.transform, false);
            face.sharedMesh = sphereMesh;
            face.sharedMaterial = skin;
            var tearsGo = new GameObject("Tears", typeof(MeshFilter), typeof(MeshRenderer));
            tearsGo.transform.SetParent(go.transform, false);
            tearsGo.GetComponent<MeshRenderer>().sharedMaterial = liquid;
            var tears = tearsGo.AddComponent<GalaxyTears>();
            tears.face = face;
            tears.startVertices = new[] { NearestVertex(from1), NearestVertex(from2) };
            tears.crying = true;
            return tears;
        }

        private static void BuildHairShadow(GameObject go, Material face)
        {
            var faceRenderer = AddSphere(go, face);
            var bangs = AddSphere(new GameObject("Bangs"), NewMaterial(face.shader, "Hair_Bangs", 2)).transform.parent;
            bangs.SetParent(go.transform, false);
            bangs.localPosition = new Vector3(0f, 0.33f, -0.12f);
            bangs.localScale = new Vector3(0.95f, 0.35f, 0.8f);
            Material hair = bangs.GetComponentInChildren<Renderer>().sharedMaterial;
            C(hair, "_BaseColor", new Color(0.35f, 0.25f, 0.2f));
            P(hair, "髪の影を落とす");
            GalaxyToonGUI.Apply(hair);
            var toonFace = go.AddComponent<GalaxyToonFace>();
            toonFace.head = go.transform;
            toonFace.faceRenderers.Add(faceRenderer);
        }

        private static void BuildHairSway(GameObject go, Material m)
        {
            AddSphere(go, m).transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            var head = new GameObject("Head").transform;
            head.SetParent(go.transform, false);
            head.localPosition = new Vector3(0f, 0.5f, 0f);
            var sway = go.AddComponent<GalaxyToonHairSway>();
            sway.head = head;
            sway.hairLength = 1f; sway.maxOffset = 0.25f; sway.idleSway = 0.05f;
        }

        private static void BuildFur(GameObject go, Material m)
        {
            var sphere = AddSphere(go, m);
            var fur = new Material(Shader.Find("GalaxyShader/ToonFur"));
            C(fur, "_BaseColor", new Color(0.9f, 0.75f, 0.55f));
            // 球は周り約 3m あるので、密度(UV 1 あたりの本数)を上げないと毛がまばらに見える
            F(fur, "_FurLength", 10f);
            F(fur, "_FurDensity", 900f);
            F(fur, "_FurThickness", 0.9f);
            F(fur, "_FurGravity", 0.4f);
            C(fur, "_FurTipColor", new Color(1f, 0.97f, 0.88f, 0.6f));
            var comp = sphere.gameObject.AddComponent<GalaxyToonFur>();
            comp.furMaterial = Save(fur, "Fur");
            comp.shells = 24;
        }

        private static void BuildTeleport(GameObject go, Material m)
        {
            var teleport = go.AddComponent<GalaxyTeleport>();
            teleport.incoming = new[] { AddSphere(go, m) }; // 空だと子の名前札まで消える
            teleport.kind = GalaxyTeleport.Kind.Appear;
            teleport.playOnStart = true;
            UnityEventTools.AddVoidPersistentListener(teleport.onFinished, teleport.Play); // 繰り返す
        }

        private static void BuildStrings(GameObject go, Material m)
        {
            // 端は球そのもの(シーンで球をつかんで動かすと糸が伸びる)。B は自動で離れて切れ、近づくとまたつながる
            Transform Ball(string n, float x)
            {
                var b = AddSphere(go, m).transform;
                b.name = n;
                b.localPosition = new Vector3(x, 0f, 0f);
                b.localScale = Vector3.one * 0.3f;
                return b;
            }
            Transform a = Ball("A", -0.55f), b2 = Ball("B", -0.24f);
            var motion = b2.gameObject.AddComponent<GalaxyToonSampleMotion>();
            motion.offset = new Vector3(1.3f, 0f, 0f);
            motion.period = 6f;
            var strings = new GameObject("Strings", typeof(MeshFilter), typeof(MeshRenderer));
            strings.transform.SetParent(go.transform, false);
            strings.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/GalaxyToon_StickyString.mat");
            var s = strings.AddComponent<GalaxyStickyStrings>();
            s.anchorA = a; s.anchorB = b2;
            // 球は口より大きいので、太さ・切れる長さ・つながる距離も合わせて大きくする
            s.spread = 0.15f; s.radius = 0.008f; s.wobble = 0.01f; s.breakLength = 1.1f; s.reformDistance = 0.4f; s.retractTime = 0.5f;
            s.enabled = false; s.enabled = true; // 糸ごとの切れる長さは有効化のときに決まるので、値を入れてから作り直す
        }

        private static void BuildRainEffects(GameObject go, Material m)
        {
            AddSphere(go, m);
            go.AddComponent<GalaxyToonEffects>().rain = 1f;
        }

        private static void BuildStencil(GameObject go, Material front)
        {
            // 奥の小さい球(書く側)が、手前の大きい球(読む側)を透かして見える。瞳を前髪の上に出すのと同じ仕組み
            AddSphere(go, front);
            Material back = NewMaterial(front.shader, "Stencil_Writer", 5);
            C(back, "_BaseColor", new Color(0.3f, 0.7f, 1f));
            F(back, "_StencilRef", 1f);
            F(back, "_StencilPass", (float)StencilOp.Replace);
            GalaxyToonGUI.Apply(back);
            var inner = AddSphere(new GameObject("Behind"), back).transform.parent;
            inner.SetParent(go.transform, false);
            inner.localPosition = new Vector3(0f, 0f, 0.6f);
            inner.localScale = Vector3.one * 0.5f;
        }

        private static void BuildLitCompare(GameObject go, Material m)
        {
            var toon = AddSphere(go, m).transform;
            toon.localPosition = new Vector3(-0.3f, 0f, 0f);
            toon.localScale = Vector3.one * 0.55f;
            var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lit.SetColor("_BaseColor", m.GetColor("_BaseColor"));
            var other = AddSphere(new GameObject("URP Lit"), Save(lit, "URP_Lit")).transform.parent;
            other.SetParent(go.transform, false);
            other.localPosition = new Vector3(0.3f, 0f, 0f);
            other.localScale = Vector3.one * 0.55f;
        }

        // ---------------------------------------------------------------- シーン・部品

        private static Camera BuildEnvironment(int rowCount)
        {
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            sun.gameObject.AddComponent<GalaxyToonLight>().intensity = 1f; // キャラ専用ライト(無くてもメインライトで描く)

            float width = (Columns - 1) * StepX, height = (rowCount - 1) * StepY;
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(width * 0.5f - 0.8f, -height * 0.5f - 0.3f, -19.5f);
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            cam.gameObject.AddComponent<AudioListener>();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.36f, 0.42f);
            return cam;
        }

        private static Renderer AddSphere(GameObject parent, Material m)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.name = "Sphere";
            sphere.transform.SetParent(parent.transform, false);
            var r = sphere.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            return r;
        }

        private static void PointLight(GameObject go, Color color)
        {
            var l = new GameObject("Point Light").AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 2f;
            l.intensity = 3f;
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0.7f, -0.3f, -0.5f);
        }

        private static void Label(Transform parent, string text, Vector3 local, Font font, float size, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var t = go.AddComponent<TextMesh>();
            t.font = font;
            t.text = text;
            t.fontSize = 48;
            t.characterSize = size;
            t.anchor = anchor;
            t.alignment = anchor == TextAnchor.MiddleRight ? TextAlignment.Right : TextAlignment.Center;
            t.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // ---------------------------------------------------------------- マテリアル

        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.75f, 0.75f), new Color(0.75f, 0.85f, 1f), new Color(0.8f, 0.95f, 0.75f), new Color(1f, 0.9f, 0.65f),
            new Color(0.85f, 0.75f, 1f), new Color(0.7f, 0.95f, 0.95f), new Color(1f, 0.8f, 0.6f), new Color(0.9f, 0.9f, 0.9f),
        };

        private static Material NewMaterial(Shader shader, string name, int colorIndex)
        {
            var m = new Material(shader);
            C(m, "_BaseColor", Palette[colorIndex % Palette.Length]);
            return Save(m, name);
        }

        /// <summary>保存する。同じ名前のマテリアルがあれば中身を入れ替える(GUID を保つ)。</summary>
        private static Material Save(Material m, string name)
        {
            m.name = name;
            string path = $"{matDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null) { AssetDatabase.CreateAsset(m, path); return m; }
            existing.shader = m.shader;
            existing.CopyPropertiesFromMaterial(m);
            existing.shaderKeywords = m.shaderKeywords;
            UnityEngine.Object.DestroyImmediate(m);
            return existing;
        }

        private static Sample S(string jp, string en, Action<Material> setup, Action<GameObject, Material> build = null) => new Sample { Jp = jp, En = en, Setup = setup, Build = build };
        private static void F(Material m, string n, float v) => m.SetFloat(n, v);
        private static void C(Material m, string n, Color c) => m.SetColor(n, c);
        private static void V(Material m, string n, Vector4 v) => m.SetVector(n, v);
        private static void P(Material m, string preset) { if (!GalaxyToonGUI.ApplyPreset(m, preset)) Debug.LogWarning("[GalaxyToon] プリセットが見つかりません: " + preset); }
        private static string Safe(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace(' ', '_'); }

        private static int NearestVertex(Vector3 local)
        {
            Vector3[] v = sphereMesh.vertices;
            int best = 0;
            for (int i = 1; i < v.Length; i++) if ((v[i] - local).sqrMagnitude < (v[best] - local).sqrMagnitude) best = i;
            return best;
        }

        // ---------------------------------------------------------------- テクスチャ(作って PNG に保存)

        private static Texture2D Tex(string name) => textures.TryGetValue(name, out var t) ? t : null;
        private static Texture2D Ramp(string kind) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{root}/Ramps/GalaxyToon_{kind}Ramp_4Rows.png");

        private static void MakeTextures()
        {
            const int n = 256;
            var clear = new Color(0f, 0f, 0f, 0f);
            Paint("Checker", n, (x, y) => (x / 32 + y / 32) % 2 == 0 ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.55f, 0.6f, 0.75f));
            Paint("Stripes", n, (x, y) => (x + y) / 16 % 2 == 0 ? Color.white : clear);
            Paint("Stars", n, (x, y) => Hash(x / 8, y / 8) > 0.85f && Vector2.Distance(new Vector2(x % 8, y % 8), new Vector2(3.5f, 3.5f)) < 2.5f ? Color.white : clear);
            Paint("Lace", n, (x, y) => { float d = Vector2.Distance(new Vector2(x % 32, y % 32), new Vector2(16f, 16f)); return d < 9f || (d > 12f && d < 14f) ? new Color(1f, 1f, 1f, 0f) : Color.white; });
            Paint("MatCap", n, (x, y) =>
            {
                var p = new Vector2(x / (n - 1f) * 2f - 1f, y / (n - 1f) * 2f - 1f);
                float v = Mathf.Clamp01(Vector2.Dot(p, new Vector2(-0.5f, 0.6f)) * 0.8f + 0.3f + Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(-0.35f, 0.45f)) * 2.5f), 2f));
                return new Color(v, v, v, 1f);
            });
            Paint("Bumps", n, (x, y) => { float v = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x % 32, y % 32), new Vector2(16f, 16f)) / 16f); return new Color(v, v, v, 1f); });
            textures["Normal"] = AssetDatabase.LoadAssetAtPath<Texture2D>(GalaxyToonNormal.Bake(Tex("Bumps"), texDir + "/Sample_Normal.png", 4f, 1, 0, false, n));
            // 瞳: 肌色の地に、瞳の範囲(_EyeRect と同じ四角)に内接する青い瞳
            Paint("Eye", 512, (x, y) =>
            {
                var uv = new Vector2(x / 511f, y / 511f);
                for (int e = -1; e <= 1; e += 2)
                {
                    var d = new Vector2((uv.x - (front.x + e * EyeX)) / EyeW, (uv.y - (front.y + EyeY)) / EyeH);
                    float r = d.magnitude;
                    if (r < 1f) return r < 0.4f ? new Color(0.05f, 0.05f, 0.15f) : Color.Lerp(new Color(0.2f, 0.45f, 0.9f), new Color(0.1f, 0.15f, 0.4f), r);
                }
                return new Color(1f, 0.88f, 0.8f);
            });
            // SDF 顔影: 球のメッシュから焼く(正面 = カメラ側の -Z)
            WritePng("FaceSdf", n, GalaxyToonSdf.Bake(sphereMesh, 0, n, Vector3.back, Vector3.left, 0.6f, 0.7f, false), linear: true);
        }

        private static void Paint(string name, int size, Func<int, int, Color> f)
        {
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = f(x, y);
            WritePng(name, size, px, linear: false);
        }

        private static void WritePng(string name, int size, Color[] px, bool linear)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, linear);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{texDir}/Sample_{name}.png";
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.sRGBTexture = !linear;
                importer.alphaIsTransparency = false; // ON だと透明部分の色が周りで埋まり、発光マスクの黒が消える
                importer.isReadable = true; // ノーマルマップを作る元にも使う
                if (linear) { importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; }
                importer.SaveAndReimport();
            }
            textures[name] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Hash(int x, int y) => Mathf.Abs(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.547f) % 1f;
    }
}
