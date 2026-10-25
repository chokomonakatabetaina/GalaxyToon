using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static GalaxyToon.Editor.GalaxyToonText;

namespace GalaxyToon.Editor
{
    /// <summary>GalaxyShader/Toon の Inspector。一番上に「描画モード」「描画する面」「プリセット」、その下を折りたためる見出しに分けて並べる(開閉は記録)。
    /// 描画モードを選ぶだけでブレンド・ZWrite・描画順・使うパスが揃い、テクスチャを入れるだけでノーマルマップ・ランプ・レイヤーが有効になる。</summary>
    public sealed class GalaxyToonGUI : ShaderGUI
    {
        public enum RenderMode { Opaque = 0, Cutout = 1, Transparent = 2 }

        // 表示の文字は言語(GalaxyToonText)で変わるので、毎回作る
        private static GUIContent ModeLabel => new GUIContent(L("描画モード", "Render Mode"), L(
            "不透明: ふつうの面。\n切り抜き: テクスチャの透明部分を切り抜く(髪の毛先・レース・網など)。\n半透明: 透けて重なる(ガラス・頬染め・涙など)。半透明では輪郭線と影を落とすのを止めます。",
            "Opaque: a normal surface.\nCutout: cuts out transparent parts of the texture (hair tips, lace, mesh, etc.).\nTransparent: see-through and layered (glass, blush, tears, etc.). Transparent disables the outline and shadow casting."));
        private static string[] ModeNames => new[] { L("不透明", "Opaque"), L("切り抜き", "Cutout"), L("半透明", "Transparent") };
        private static GUIContent CullLabel => new GUIContent(L("描画する面", "Render Face"), L(
            "両面: 表も裏も描く(髪・スカート・薄い布)。\n表だけ: ふつうのモデル(軽い)。\n裏だけ: 特殊な用途(内側から見る球など)。",
            "Both: draws front and back (hair, skirts, thin cloth).\nFront: normal models (cheaper).\nBack: special uses (spheres seen from inside, etc.)."));
        private static string[] CullNames => new[] { L("両面", "Both"), L("裏だけ", "Back"), L("表だけ", "Front") }; // CullMode の値の順(Off=0, Front=1, Back=2)

        // 見出し → 中の項目(並び順)。どこにも入っていない項目は「その他」に出す
        private static readonly (string title, string[] props)[] Sections =
        {
            ("基本(色・立体感)", new[] { "_BaseMap", "_BaseColor", "_BumpMap", "_BumpScale", "_Bump2Map", "_Bump2Scale", "_OcclusionMap", "_OcclusionStrength" }),
            ("色調補正", new[] { "_HueShift", "_Saturation", "_Brightness", "_Gamma" }),
            ("金属・つや・映り込み", new[] { "_Metallic", "_Smoothness", "_MetallicGlossMap", "_ReflectionStrength", "_ReflectionSteps" }),
            ("ラメ・屈折 (宝石・ガラス)", new[] { "_GlitterColor", "_GlitterMask", "_GlitterScale", "_GlitterSize", "_GlitterDensity", "_GlitterSharpness", "_GlitterShadowMask", "_RefractionStrength", "_RefractionDispersion" }),
            ("トライプラナー (UV なしで貼る)", new[] { "_UseTriplanar", "_TriplanarScale", "_TriplanarBlend" }),
            ("ライト", new[] { "_LightSource", "_LightMinLimit", "_LightMaxLimit", "_MonochromeLighting", "_AmbientStrength", "_AdditionalLightStrength", "_BakedSteps" }),
            ("影", new[] { "_ShadowColor", "_ShadowBorder", "_ShadowBlur", "_Shadow2ndColor", "_Shadow2ndBorder", "_Shadow2ndBlur", "_Shadow3rdColor", "_Shadow3rdBorder", "_Shadow3rdBlur", "_ShadowStrengthMask", "_ShadowRampTex", "_ShadowRampRows", "_ShadowRampRow", "_RampIdMap", "_ShadowReceive", "_AAStrength" }),
            ("アニメ塗り (日本のアニメ調)", new[] { "_AnimeMode", "_AnimeHardness", "_AnimeShadowHue", "_AnimeShadowSat", "_AnimeShadowValue", "_AnimeEdgeColor", "_AnimeEdgeWidth", "_AnimeFlatLight", "_AnimeFlatNormal", "_AnimeFlatTexture", "_AnimeFlatMip", "_AnimeNoGloss" }),
            ("顔", new[] { "_FaceShading", "_FaceSphere", "_UseFaceSdf", "_FaceSdfTex" }),
            ("瞳 (奥行き・ハイライト・うるうる・きらめき)", new[] { "_UseEye", "_EyeRect1", "_EyeRect2", "_EyeParallax", "_EyeHighlightColor", "_EyeHighlightPos", "_EyeHighlightSize", "_EyeWet", "_EyeSparkleColor", "_EyeSparkleScale", "_EyeSparkleSpeed" }),
            ("頬染め (照れ)", new[] { "_BlushAmount", "_BlushColor", "_BlushUV", "_BlushSize", "_BlushSoft", "_BlushLines" }),
            ("髪の揺れ", new[] { "_HairSway" }),
            ("髪の影 (前髪の影を顔に落とす)", new[] { "_HairShadowCast", "_HairShadowReceive", "_HairShadowDistance", "_HairShadowBias" }),
            ("ハイライト・マットキャップ", new[] { "_SpecularColor", "_SpecularSize", "_SpecularBlur", "_SpecularRampTex", "_MatCapTex", "_MatCapColor", "_MatCapShadowMask", "_MatCap2Tex", "_MatCap2Color", "_MatCap2Blend", "_MatCap2Mask", "_MatCap2ShadowMask" }),
            ("異方性ハイライト (髪の天使の輪)", new[] { "_AnisoColor", "_AnisoDirection", "_AnisoShift", "_AnisoSharpness", "_AnisoBorder", "_AnisoBlur", "_AnisoShadowMask", "_AnisoUseRamp" }),
            ("逆光ライト", new[] { "_BacklightColor", "_BacklightBorder", "_BacklightBlur", "_BacklightDirectivity", "_BacklightViewStrength", "_BacklightMainStrength" }),
            ("リムライト", new[] { "_RimColor", "_RimBorder", "_RimBlur", "_RimFresnelPower", "_RimShadowMask", "_DepthRim", "_RimDepthWidth", "_RimDepthThreshold" }),
            ("発光", new[] { "_EmissionMap", "_EmissionColor", "_EmissionAnimate", "_EmissionScroll", "_EmissionBlink", "_Emission2Map", "_Emission2Color", "_Emission2Animate", "_Emission2Scroll", "_Emission2Blink" }),
            ("レイヤー1", LayerProps(1)),
            ("レイヤー2", LayerProps(2)),
            ("レイヤー3", LayerProps(3)),
            ("濡れ・汗・液体・汚れ", new[] { "_UseFx", "_FxMask", "_WetAmount", "_WetReceive", "_WetDarken", "_WetGloss", "_WetSoak", "_WetShrink", "_HairWet", "_HairWetClumps", "_SweatAmount", "_SweatReceive", "_SweatScale", "_SweatSize", "_SweatFlow", "_LiquidAmount", "_LiquidReceive", "_LiquidColor", "_LiquidScale", "_LiquidFlow", "_LiquidGloss", "_LiquidViscosity", "_LiquidFlowDir", "_FoamAmount", "_FoamReceive", "_FoamColor", "_FoamScale", "_FoamSize", "_FoamFlow", "_FoamPop", "_FoamMerge", "_RainAmount", "_RainReceive", "_RainRipples", "_RainFlowAmount", "_RainSplash", "_RainScale", "_RainSpeed", "_DirtAmount", "_DirtReceive", "_DirtColor", "_DirtScale", "_ScratchAmount", "_ScratchColor", "_ScratchScale", "_FxBump" }),
            ("輪郭線", new[] { "_OutlineWidth", "_OutlineColor", "_OutlineTexBlend", "_OutlineLit", "_OutlineFixWidth", "_AnimeOutlinePixel", "_OutlineWidthMask", "_OutlineSmoothNormal" }),
            ("カメラが近いと透ける", new[] { "_UseNearFade", "_NearFadeStart", "_NearFadeEnd" }),
            ("ディゾルブ (消える演出)", new[] { "_UseDissolve", "_DissolveAmount", "_DissolveMode", "_DissolveDirection", "_DissolveStart", "_DissolveNoise", "_DissolveNoiseScale", "_DissolveNoiseStrength", "_DissolveInvert", "_DissolveWorld", "_DissolveEdgeWidth", "_DissolveEdgeColor" }),
            ("ステンシル (瞳を髪に透かす など)", new[] { "_StencilRef", "_StencilComp", "_StencilPass", "_StencilFail", "_StencilZFail", "_StencilReadMask", "_StencilWriteMask" }),
        };

        private static string[] LayerProps(int i) => new[] { $"_Layer{i}Tex", $"_Layer{i}Color", $"_Layer{i}Blend", $"_Layer{i}Animate", $"_Layer{i}Scroll", $"_Layer{i}Fade", $"_Layer{i}Blink", $"_Layer{i}BlinkPhase", $"_Layer{i}Unlit", $"_Layer{i}Mask" };

        private static GUIStyle logoStyle;

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
        {
            // 小さなロゴ(右上)
            logoStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.45f, 0.65f, 0.35f) } };
            using (new EditorGUILayout.HorizontalScope())
            {
                // 表示言語(既定は日本語)。選択は EditorPrefs に記憶
                int language = EditorGUILayout.Popup(English ? 1 : 0, LanguageNames, GUILayout.Width(80f));
                English = language == 1;
                GUILayout.Label("GalaxyToon  Author: @matechagame", logoStyle);
            }

            EditorGUI.BeginChangeCheck();

            // ---- 見比べ: シェーダー ON/OFF(全マテリアル共通。Ctrl+Shift+T でも切り替え)
            bool off = GalaxyToonTools.ShaderOff;
            Color offButtonBg = GUI.backgroundColor;
            if (off) GUI.backgroundColor = new Color(1f, 0.6f, 0.5f);
            if (GUILayout.Button(new GUIContent(off ? L("シェーダー OFF 中 (URP Lit で表示) — 押すと ON", "Shader OFF (shown with URP Lit) — click to turn ON") : L("シェーダー ON — 押すと OFF (URP Lit で見比べ)", "Shader ON — click to turn OFF (compare with URP Lit)"),
                L("GalaxyToon の全マテリアルを、同じテクスチャのまま URP 標準の Lit で描いて見比べます。マテリアルは変わりません。Ctrl+Shift+T", "Draws every GalaxyToon material with the standard URP Lit using the same textures, for comparison. Materials are not changed. Ctrl+Shift+T")), EditorStyles.miniButton))
                GalaxyToonTools.ToggleShaderOff();
            GUI.backgroundColor = offButtonBg;
            EditorGUILayout.Space(2f);

            // ---- 描画モード・描画する面・切り抜きのしきい値
            MaterialProperty mode = FindProperty("_RenderMode", props);
            EditorGUI.showMixedValue = mode.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup(ModeLabel, (int)mode.floatValue, ModeNames);
            if (EditorGUI.EndChangeCheck()) { editor.RegisterPropertyChangeUndo("描画モード"); mode.floatValue = picked; }
            MaterialProperty cull = FindProperty("_Cull", props);
            EditorGUI.showMixedValue = cull.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int cullPicked = EditorGUILayout.Popup(CullLabel, (int)cull.floatValue, CullNames);
            if (EditorGUI.EndChangeCheck()) { editor.RegisterPropertyChangeUndo("描画する面"); cull.floatValue = cullPicked; }
            EditorGUI.showMixedValue = false;
            if ((RenderMode)(int)mode.floatValue == RenderMode.Cutout) editor.ShaderProperty(FindProperty("_Cutoff", props), L("切り抜きのしきい値", "Alpha Cutoff"));
            editor.ShaderProperty(FindProperty("_QueueOffset", props), new GUIContent(L("描画順のずらし", "Render Queue Offset"), L("同じ面に重ねた文字・デカール・ステッカーは +1 にすると、下の面より後に描かれて消えなくなります。", "Set text, decals, or stickers layered on the same surface to +1 so they draw after the surface below and don't disappear.")));

            // ---- プリセット(折りたためる): 押すと、その部位らしい値をまとめて入れる(テクスチャ・色・描画モードは変えない)
            bool presetsOpen = EditorPrefs.GetBool(PresetsFoldKey, true);
            bool presetsNext = EditorGUILayout.Foldout(presetsOpen, L("プリセット", "Presets"), true, EditorStyles.foldoutHeader);
            if (presetsNext != presetsOpen) EditorPrefs.SetBool(PresetsFoldKey, presetsNext);
            if (presetsNext)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    PresetRow(editor, new GUIContent(L("キャラ", "Character"), L("部位に合った影・リム・輪郭線などの値をまとめて入れます。テクスチャ・メインカラー・描画モードは変えません。", "Sets shadow, rim, outline, and other values suited to each body part. Textures, main color, and render mode are not changed.")), Presets);
                    PresetRow(editor, new GUIContent(L("背景", "Environment"), L("建物・小物向け。シーンのライト・環境光・影を受け、金属・つや・映り込みを素材らしく入れます。輪郭線は付けません。", "For buildings and props. Receives scene lights, ambient light, and shadows, and sets metallic, smoothness, and reflection to match the material. No outline.")), EnvironmentPresets);
                    PresetRow(editor, new GUIContent(L("肌・服の質感", "Skin / Cloth Surface"), L("肌の汗・濡れ・液体と、服の素材感(柔らかい布、ラテックス、革)。影やハイライトなど今の値の上に、質感だけ上書きします。", "Sweat, wetness, and liquid on skin, and cloth materials (soft fabric, latex, leather). Overwrites only the surface look on top of the current shadow and highlight values.")), SurfacePresets);
                    PresetRow(editor, new GUIContent(L("追加 (今の設定に足す)", "Add (on top of current)"), L("ほかの値は変えず、濡れ・汗・液体・汚れ・演出だけを足します。重ねて押せます(汗 + ウェット など)。", "Adds only wetness, sweat, liquid, dirt, or effects without changing other values. Can be stacked (Sweat + Wet, etc.).")), AdditivePresets);
                    PresetRow(editor, new GUIContent(L("キャラの仕上げ", "Character Finish"), L("髪の影・瞳・ラメ・近づくと透けるなど、キャラの見栄えを上げる機能を足します(ほかの値は変えない)。", "Adds features that polish the character, such as hair shadow, eyes, glitter, and fade when close (other values are not changed).")), FinishPresets);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel(new GUIContent(L("リセット", "Reset")));
                        if (GUILayout.Button(new GUIContent(L("基本以外リセット", "Reset All but Basic"), L("テクスチャ・メインカラー・立体感・描画モード・描画する面はそのまま、ほかの項目を全部初期値に戻す。", "Keeps textures, main color, normal maps, render mode, and render face, and resets everything else to defaults.")), EditorStyles.miniButton))
                            foreach (Object t in editor.targets) { Undo.RecordObject(t, "基本以外リセット"); ResetExceptBase((Material)t); }
                    }
                }
            }
            // ---- 検索・アコーディオン・全部開く/閉じる
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(L("テクスチャの見比べ", "Compare Textures"), L("各テクスチャの右の「オフ」で、選び直さずに有り/無しを切り替えられます(外している間も参照は保持)。", "Use Off next to each texture to toggle it without reassigning it (the reference is kept while it is off).")), GUILayout.Width(110f));
                if (GUILayout.Button(new GUIContent(L("全部オフ", "All Off"), L("入っているテクスチャを全部いったん外す(参照は保持)。", "Temporarily removes all assigned textures (references are kept).")), EditorStyles.miniButtonLeft, GUILayout.Width(60f)))
                    foreach (Object t in editor.targets) SetAllTextures((Material)t, false);
                if (GUILayout.Button(new GUIContent(L("全部戻す", "All On"), L("外していたテクスチャを全部戻す。", "Restores all removed textures.")), EditorStyles.miniButtonRight, GUILayout.Width(60f)))
                    foreach (Object t in editor.targets) SetAllTextures((Material)t, true);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField(search ?? "", EditorStyles.toolbarSearchField);
                bool accordion = EditorPrefs.GetBool(AccordionKey, true);
                bool next = GUILayout.Toggle(accordion, new GUIContent(L("1つずつ開く", "One at a Time"), L("ON: 見出しを開くと、ほかの見出しは自動で閉じる(アコーディオン)。", "ON: opening a section closes the others (accordion).")), EditorStyles.miniButton, GUILayout.Width(72f));
                if (next != accordion) EditorPrefs.SetBool(AccordionKey, next);
                using (new EditorGUI.DisabledScope(next))
                    if (GUILayout.Button(L("全部開く", "Expand"), EditorStyles.miniButtonLeft, GUILayout.Width(56f))) SetAll(true);
                if (GUILayout.Button(L("全部閉じる", "Collapse"), EditorStyles.miniButtonRight, GUILayout.Width(60f))) SetAll(false);
                GUILayout.Space(8f);
                if (GUILayout.Button(new GUIContent(L("全て初期化", "Reset All"), L("テクスチャ・描画モードも含めて、このマテリアルの全項目をシェーダの初期値に戻す(確認あり)。", "Resets every property of this material to the shader defaults, including textures and render mode (asks first).")), EditorStyles.miniButton, GUILayout.Width(64f))
                    && EditorUtility.DisplayDialog(L("全て初期化", "Reset All"), L("テクスチャ・描画モードも含めて、全項目を初期値に戻します。よろしいですか？", "Reset every property to its default, including textures and render mode?"), L("初期化", "Reset"), L("キャンセル", "Cancel")))
                    foreach (Object t in editor.targets) { Undo.RecordObject(t, "全て初期化"); ResetAll((Material)t); }
            }

            // ---- 見出しごと(箱で区切る)。検索中は、名前に検索語を含む項目だけを見出しごとに出す
            var shown = new System.Collections.Generic.HashSet<string> { "_RenderMode", "_Cull", "_Cutoff", "_QueueOffset" };
            var sections = new System.Collections.Generic.List<(string, string[])>(Sections);
            var otherNames = new System.Collections.Generic.List<string>();
            foreach (var (_, names) in Sections) foreach (string n in names) shown.Add(n);
            foreach (MaterialProperty p in props) if (!shown.Contains(p.name) && (p.propertyFlags & ShaderPropertyFlags.HideInInspector) == 0) otherNames.Add(p.name);
            if (otherNames.Count > 0) sections.Add(("その他", otherNames.ToArray()));

            bool searching = !string.IsNullOrWhiteSpace(search);
            int hits = 0;
            foreach (var (title, names) in sections)
            {
                var visible = new System.Collections.Generic.List<MaterialProperty>();
                foreach (string n in names)
                {
                    MaterialProperty p = FindProperty(n, props, false);
                    if (p == null || ((p.propertyFlags & ShaderPropertyFlags.HideInInspector) != 0 && !n.StartsWith("_Stencil"))) continue;
                    if (searching && !Matches(search, p.displayName, Property(p), p.name, title, Section(title))) continue;
                    visible.Add(p);
                }
                if (visible.Count == 0) continue;
                hits += visible.Count;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool inUse = SectionInUse((Material)editor.target, title);
                    string header = inUse ? Section(title) + L("  ● 使用中", "  ● In use") : Section(title);
                    if (searching) GUILayout.Label(header, EditorStyles.boldLabel);
                    else if (!Foldout(title, header, title == "基本(色・立体感)", inUse)) continue;
                    EditorGUI.indentLevel++;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent(L("この項目を初期化", "Reset Section"), L($"「{title}」の項目だけを初期値に戻す(確認あり)。", $"Resets only the \"{Section(title)}\" properties to defaults (asks first).")), EditorStyles.miniButton, GUILayout.Width(110f))
                            && EditorUtility.DisplayDialog(L("項目を初期化", "Reset Section"), L($"「{title}」の項目を初期値に戻します。よろしいですか？", $"Reset the \"{Section(title)}\" properties to defaults?"), L("初期化", "Reset"), L("キャンセル", "Cancel")))
                            foreach (Object t in editor.targets) { Undo.RecordObject(t, title + " を初期化"); ResetProperties((Material)t, names); }
                    }
                    if (title.StartsWith("ステンシル")) StencilButtons(editor);
                    Shader shader = ((Material)editor.target).shader;
                    foreach (MaterialProperty p in visible)
                    {
                        Color prevBg = GUI.backgroundColor;
                        if (IsInUse(shader, p)) GUI.backgroundColor = InUseColor; // 初期値と違う = 効いている項目を着色(色は「表示の設定」)
                        if (p.propertyType == ShaderPropertyType.Texture) TextureRow(editor, p, props);
                        else editor.ShaderProperty(p, Property(p));
                        GUI.backgroundColor = prevBg;
                    }
                    EditorGUI.indentLevel--;
                }
            }
            if (searching && hits == 0) EditorGUILayout.HelpBox(L($"「{search}」に合う項目はありません。", $"No properties match \"{search}\"."), MessageType.None);

            EditorGUILayout.Space(6f);
            editor.RenderQueueField();
            DisplaySettings();
            if (EditorGUI.EndChangeCheck()) foreach (Object target in editor.targets) Apply((Material)target); // モード・キーワードを揃える
        }

        private string search;
        private const string AccordionKey = "GalaxyToonGUI.Accordion";

        // ---- テクスチャの有り/無しの見比べ: 「オフ」でテクスチャを隠し枠(_X_Stash)へ退避し、もう一度押すと戻す
        private static string StashName(string texture) => texture + "_Stash";

        private static bool IsTextureOff(Material m, string texture) => m.HasProperty(StashName(texture)) && m.GetTexture(StashName(texture)) != null;

        /// <summary>on=false でテクスチャを退避して外す、on=true で戻す。</summary>
        public static void SetTextureOn(Material m, string texture, bool on)
        {
            string stash = StashName(texture);
            if (!m.HasProperty(stash)) return;
            if (!on && m.GetTexture(texture) != null && m.GetTexture(stash) == null) { m.SetTexture(stash, m.GetTexture(texture)); m.SetTexture(texture, null); }
            else if (on && m.GetTexture(stash) != null) { m.SetTexture(texture, m.GetTexture(stash)); m.SetTexture(stash, null); }
            Apply(m); // テクスチャで決まる機能(ノーマル・レイヤーなど)を合わせる
            EditorUtility.SetDirty(m);
        }

        public static void SetAllTextures(Material m, bool on)
        {
            Undo.RecordObject(m, on ? "テクスチャを全部戻す" : "テクスチャを全部オフ");
            Shader s = m.shader;
            for (int i = 0; i < s.GetPropertyCount(); i++)
                if (s.GetPropertyType(i) == ShaderPropertyType.Texture && !s.GetPropertyName(i).EndsWith("_Stash")) SetTextureOn(m, s.GetPropertyName(i), on);
        }

        private static void TextureRow(MaterialEditor editor, MaterialProperty p, MaterialProperty[] props)
        {
            var first = (Material)editor.target;
            bool off = IsTextureOff(first, p.name);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(off))
                    editor.ShaderProperty(p, off ? Property(p) + L(" (オフ中)", " (off)") : Property(p));
                bool hasAny = off || p.textureValue != null;
                using (new EditorGUI.DisabledScope(!hasAny))
                    if (GUILayout.Button(new GUIContent(off ? L("戻す", "On") : L("オフ", "Off"), L("テクスチャを選び直さずに、有り/無しを見比べる。", "Compare with and without the texture without reassigning it.")), EditorStyles.miniButton, GUILayout.Width(40f)))
                        foreach (Object t in editor.targets) { Undo.RecordObject(t, "テクスチャ " + (off ? "戻す" : "オフ")); SetTextureOn((Material)t, p.name, off); }
            }
        }

        /// <summary>ステンシルの決まった組み合わせ。瞳・眉を「書き込む」、髪を「透かす」にすると、髪の上から瞳が見える(髪の描画順を1つ後ろにする)。</summary>
        private static void StencilButtons(MaterialEditor editor)
        {
            EditorGUILayout.HelpBox(L("瞳・眉のマテリアルで「書き込む」、髪のマテリアルで「透かす」を押すと、髪の上から瞳が見えます。", "Press Write on the eye and eyebrow materials and See Through on the hair material to show the eyes over the hair."), MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(L("書き込む (瞳・眉)", "Write (Eyes, Brows)"), L("ステンシル 1 を書き込む。描画順はそのまま。", "Writes stencil 1. The render queue is unchanged.")), EditorStyles.miniButtonLeft))
                    foreach (Object t in editor.targets) SetStencil((Material)t, 1, CompareFunction.Always, StencilOp.Replace, RenderQueue.Geometry);
                if (GUILayout.Button(new GUIContent(L("透かす (髪)", "See Through (Hair)"), L("ステンシル 1 の所は描かない。描画順を瞳の後(Geometry+1)にする。", "Does not draw where stencil is 1. Moves the render queue after the eyes (Geometry+1).")), EditorStyles.miniButtonMid))
                    foreach (Object t in editor.targets) SetStencil((Material)t, 1, CompareFunction.NotEqual, StencilOp.Keep, RenderQueue.Geometry + 1);
                if (GUILayout.Button(new GUIContent(L("使わない", "Off"), L("ステンシルを無効に戻す。", "Turns the stencil off.")), EditorStyles.miniButtonRight))
                    foreach (Object t in editor.targets) SetStencil((Material)t, 0, CompareFunction.Always, StencilOp.Keep, RenderQueue.Geometry);
            }
        }

        private static void SetStencil(Material m, int reference, CompareFunction comp, StencilOp pass, RenderQueue queue)
        {
            Undo.RecordObject(m, "ステンシル");
            m.SetFloat("_StencilRef", reference);
            m.SetFloat("_StencilComp", (float)comp);
            m.SetFloat("_StencilPass", (float)pass);
            m.SetFloat("_StencilFail", (float)StencilOp.Keep);
            m.SetFloat("_StencilZFail", (float)StencilOp.Keep);
            if (m.GetFloat("_RenderMode") < 1.5f) m.renderQueue = (int)queue; // 半透明は描画順を変えない
            EditorUtility.SetDirty(m);
        }
        private static string FoldKey(string title) => "GalaxyToonGUI.Fold." + title;

        // ---- 表示の設定(色は EditorPrefs に記憶。Inspector 下部の「表示の設定」で変更)
        private const string HeaderColorKey = "GalaxyToonGUI.HeaderInUseColor";
        private const string InUseColorKey = "GalaxyToonGUI.ItemInUseColor";
        private static readonly Color DefaultHeaderColor = new Color(1f, 0.95f, 0.55f, 0.45f);
        private static readonly Color DefaultInUseColor = new Color(0.72f, 1f, 0.72f);
        private static Color HeaderInUseColor => LoadColor(HeaderColorKey, DefaultHeaderColor);
        private static Color InUseColor => LoadColor(InUseColorKey, DefaultInUseColor);

        private static Color LoadColor(string key, Color fallback) =>
            ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(key, ""), out Color c) ? c : fallback;

        private static void SaveColor(string key, Color c) => EditorPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(c));

        private static void DisplaySettings()
        {
            EditorGUILayout.Space(4f);
            bool open = EditorPrefs.GetBool(FoldKey("表示の設定"), false);
            bool next = EditorGUILayout.Foldout(open, L("表示の設定", "Display Settings"), true, EditorStyles.foldoutHeader);
            if (next != open) EditorPrefs.SetBool(FoldKey("表示の設定"), next);
            if (!next) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                Color header = EditorGUILayout.ColorField(new GUIContent(L("使用中の見出しの背景", "In-use Section Background"), L("機能が効いている見出し(折りたたみの行)の背景色。", "Background color of section headers whose feature is active.")), HeaderInUseColor);
                Color item = EditorGUILayout.ColorField(new GUIContent(L("使用中の項目の色", "In-use Property Color"), L("初期値と違う(効いている)項目の入力欄の色。", "Color of fields whose value differs from the default (active).")), InUseColor);
                if (EditorGUI.EndChangeCheck()) { SaveColor(HeaderColorKey, header); SaveColor(InUseColorKey, item); }
                if (GUILayout.Button(L("色を初期値に戻す", "Reset Colors"), EditorStyles.miniButton, GUILayout.Width(120f))) { EditorPrefs.DeleteKey(HeaderColorKey); EditorPrefs.DeleteKey(InUseColorKey); }
            }
        }

        /// <summary>指定した項目(とその退避枠)だけをシェーダの初期値に戻す。</summary>
        public static void ResetProperties(Material m, IEnumerable<string> names)
        {
            Shader s = m.shader;
            foreach (string name in names)
                foreach (string n in new[] { name, StashName(name) })
                {
                    int i = s.FindPropertyIndex(n);
                    if (i < 0) continue;
                    switch (s.GetPropertyType(i))
                    {
                        case ShaderPropertyType.Texture: m.SetTexture(n, null); break;
                        case ShaderPropertyType.Color: m.SetColor(n, (Color)s.GetPropertyDefaultVectorValue(i)); break;
                        case ShaderPropertyType.Vector: m.SetVector(n, s.GetPropertyDefaultVectorValue(i)); break;
                        default: m.SetFloat(n, s.GetPropertyDefaultFloatValue(i)); break;
                    }
                }
            Apply(m);
            EditorUtility.SetDirty(m);
        }

        /// <summary>全項目(テクスチャ・描画モード含む)をシェーダの初期値に戻す。</summary>
        public static void ResetAll(Material m)
        {
            var all = new List<string>();
            for (int i = 0; i < m.shader.GetPropertyCount(); i++) all.Add(m.shader.GetPropertyName(i));
            ResetProperties(m, all);
            m.renderQueue = -1;
            Apply(m);
        }

        /// <summary>「基本」(テクスチャ・色・立体感)と描画モード・描画する面以外を、シェーダの初期値に戻す。</summary>
        public static void ResetExceptBase(Material m)
        {
            var keep = new HashSet<string> { "_RenderMode", "_Cull", "_Cutoff", "_QueueOffset", "_SrcBlend", "_DstBlend", "_ZWrite", "_AlphaClip" };
            foreach (var (title, names) in Sections) if (title.StartsWith("基本")) foreach (string n in names) keep.Add(n);
            Shader s = m.shader;
            for (int i = 0; i < s.GetPropertyCount(); i++)
            {
                string n = s.GetPropertyName(i);
                if (keep.Contains(n) || n.EndsWith("_Stash") && keep.Contains(n.Substring(0, n.Length - "_Stash".Length))) continue;
                switch (s.GetPropertyType(i))
                {
                    case ShaderPropertyType.Texture: m.SetTexture(n, null); break;
                    case ShaderPropertyType.Color: m.SetColor(n, (Color)s.GetPropertyDefaultVectorValue(i)); break;
                    case ShaderPropertyType.Vector: m.SetVector(n, s.GetPropertyDefaultVectorValue(i)); break;
                    default: m.SetFloat(n, s.GetPropertyDefaultFloatValue(i)); break;
                }
            }
            Apply(m);
            EditorUtility.SetDirty(m);
        }

        /// <summary>値が初期値と違う(= 効いている)か。テクスチャは入っていれば使用中。</summary>
        public static bool IsInUse(Shader shader, MaterialProperty p)
        {
            int i = shader.FindPropertyIndex(p.name);
            if (i < 0) return false;
            switch (p.propertyType)
            {
                case ShaderPropertyType.Texture: return p.textureValue != null;
                case ShaderPropertyType.Color: return p.colorValue != (Color)shader.GetPropertyDefaultVectorValue(i);
                case ShaderPropertyType.Vector: return p.vectorValue != shader.GetPropertyDefaultVectorValue(i);
                default: return !Mathf.Approximately(p.floatValue, shader.GetPropertyDefaultFloatValue(i));
            }
        }

        /// <summary>見出しの機能が今効いているか(キーワードやパスで判定)。</summary>
        public static bool SectionInUse(Material m, string title)
        {
            bool Kw(params string[] k) { foreach (string x in k) if (m.IsKeywordEnabled(x)) return true; return false; }
            if (title.StartsWith("色調補正")) return Kw("_COLORADJUST");
            if (title.StartsWith("金属")) return Kw("_GLOSSY");
            if (title.StartsWith("トライプラナー")) return Kw("_TRIPLANAR");
            if (title.StartsWith("ラメ")) return Kw("_GLITTER", "_REFRACTION");
            if (title.StartsWith("瞳")) return Kw("_EYE");
            if (title.StartsWith("髪の影")) return Kw("_HAIR_SHADOW") || m.GetFloat("_HairShadowCast") > 0.5f;
            if (title.StartsWith("カメラが近い")) return Kw("_NEAR_FADE");
            if (title.StartsWith("アニメ塗り")) return Kw("_ANIME");
            if (title.StartsWith("顔")) return Kw("_FACE", "_FACE_SDF");
            if (title.StartsWith("髪の揺れ")) return m.GetFloat("_HairSway") > 0f;
            if (title.StartsWith("ハイライト")) return Kw("_SPECULAR", "_MATCAP", "_MATCAP2");
            if (title.StartsWith("異方性")) return Kw("_ANISOTROPY");
            if (title.StartsWith("リムライト")) return Kw("_RIMLIGHT");
            if (title.StartsWith("逆光")) return Kw("_BACKLIGHT");
            if (title.StartsWith("発光")) return Kw("_EMISSION");
            if (title.StartsWith("レイヤー")) return Kw("_LAYER" + title.Substring("レイヤー".Length, 1));
            if (title.StartsWith("濡れ")) return Kw("_GALAXY_FX");
            if (title.StartsWith("輪郭線")) return m.GetShaderPassEnabled("SRPDefaultUnlit") && m.GetFloat("_OutlineWidth") > 0f;
            if (title.StartsWith("ディゾルブ")) return Kw("_DISSOLVE");
            if (title.StartsWith("ステンシル")) return m.GetFloat("_StencilComp") != (float)CompareFunction.Always || m.GetFloat("_StencilPass") != (float)StencilOp.Keep;
            if (title.StartsWith("影")) return Kw("_SHADOW2ND", "_SHADOW3RD", "_SHADOW_RAMP");
            if (title.StartsWith("基本")) return m.GetTexture("_BaseMap") != null || Kw("_NORMALMAP", "_NORMALMAP2");
            return false;
        }

        /// <summary>見出し(折りたたみ)。アコーディオンが ON なら、開いたときにほかを閉じる。</summary>
        private static bool Foldout(string title, string header, bool defaultOpen, bool inUse = false)
        {
            bool open = EditorPrefs.GetBool(FoldKey(title), defaultOpen);
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.foldoutHeader);
            if (inUse) EditorGUI.DrawRect(rect, HeaderInUseColor); // 使用中の見出しは背景を着色(色は「表示の設定」)
            bool next = EditorGUI.Foldout(rect, open, header, true, EditorStyles.foldoutHeader);
            if (next != open)
            {
                if (next && EditorPrefs.GetBool(AccordionKey, true)) SetAll(false);
                EditorPrefs.SetBool(FoldKey(title), next);
            }
            return next;
        }

        private static void SetAll(bool open)
        {
            foreach (var (title, _) in Sections) EditorPrefs.SetBool(FoldKey(title), open);
            EditorPrefs.SetBool(FoldKey("その他"), open);
        }

        /// <summary>空白区切りの語がすべて、項目名・内部名・見出しのどれかに含まれていれば一致(大文字小文字は区別しない)。</summary>
        private static bool Matches(string query, params string[] texts)
        {
            foreach (string word in query.Split(new[] { ' ', '　' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                bool found = false;
                foreach (string t in texts) if (t != null && t.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        private static void PresetRow(MaterialEditor editor, GUIContent label, Preset[] presets)
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(presets.Length / 6f));
            int perRow = Mathf.CeilToInt(presets.Length / (float)rows); // 多い行は 6 個までずつ、段に分ける
            for (int start = 0; start < presets.Length; start += perRow)
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (start == 0) EditorGUILayout.PrefixLabel(label);
                    else EditorGUILayout.PrefixLabel(" ");
                    for (int i = start; i < Mathf.Min(start + perRow, presets.Length); i++)
                    {
                        Preset preset = presets[i];
                        if (GUILayout.Button(new GUIContent(PresetName(preset.Name), PresetTooltip(preset.Name, preset.Tooltip)), EditorStyles.miniButton))
                            foreach (Object target in editor.targets) { Undo.RecordObject(target, "プリセット " + preset.Name); preset.Apply((Material)target); Apply((Material)target); }
                    }
                }
        }

        private sealed class Preset
        {
            public string Name, Tooltip;
            public (string, float)[] Floats;
            public (string, Color)[] Colors;
            public bool Environment;
            public void Apply(Material m)
            {
                if (!Environment) // キャラ: キャラ専用ライト・環境光なし・金属なし(背景プリセットの後に押しても戻る)
                    foreach (var (n, v) in new[] { ("_LightSource", 0f), ("_AmbientStrength", 0f), ("_AdditionalLightStrength", 0f), ("_ShadowReceive", 0f), ("_Metallic", 0f), ("_Smoothness", 0f) }) m.SetFloat(n, v);
                foreach (var (n, v) in Floats) m.SetFloat(n, v);
                foreach (var (n, c) in Colors) m.SetColor(n, c);
            }
        }

        private static readonly Preset[] Presets =
        {
            new Preset { Name = "肌", Tooltip = "やわらかい1段影(赤み)、弱いリム、細い輪郭線。",
                Floats = new[] { ("_ShadowBorder", 0.5f), ("_ShadowBlur", 0.12f), ("_FaceShading", 0f), ("_OutlineWidth", 0.06f), ("_OutlineTexBlend", 0.7f), ("_RimFresnelPower", 4f), ("_SpecularSize", 0.05f) },
                Colors = new[] { ("_ShadowColor", new Color(0.97f, 0.82f, 0.8f, 1f)), ("_Shadow2ndColor", new Color(0.9f, 0.7f, 0.7f, 0f)), ("_RimColor", new Color(1f, 0.95f, 0.9f, 0.15f)), ("_SpecularColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Name = "顔", Tooltip = "肌と同じ色で、顔の細かい影を消す(顔の陰影を平らに 0.8)。キャラに GalaxyToonFace を付けると首の向きに追従します。",
                Floats = new[] { ("_ShadowBorder", 0.45f), ("_ShadowBlur", 0.1f), ("_FaceShading", 0.8f), ("_OutlineWidth", 0.04f), ("_OutlineTexBlend", 0.7f), ("_RimFresnelPower", 5f) },
                Colors = new[] { ("_ShadowColor", new Color(0.97f, 0.84f, 0.82f, 1f)), ("_Shadow2ndColor", new Color(0.9f, 0.7f, 0.7f, 0f)), ("_RimColor", new Color(1f, 0.95f, 0.9f, 0.1f)), ("_SpecularColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Name = "髪", Tooltip = "くっきりした2段影、ハイライト(天使の輪っぽい光沢)、細いリム、輪郭線。前髪を透かすときはメニュー「Tools/GalaxyToon/前髪を透過する」でキャラに付けます。",
                Floats = new[] { ("_ShadowBorder", 0.5f), ("_ShadowBlur", 0.05f), ("_Shadow2ndBorder", 0.3f), ("_Shadow2ndBlur", 0.05f), ("_FaceShading", 0f), ("_OutlineWidth", 0.08f), ("_OutlineTexBlend", 0.6f), ("_SpecularSize", 0.04f), ("_SpecularBlur", 0.02f), ("_RimFresnelPower", 3.5f), ("_RimBorder", 0.6f), ("_RimBlur", 0.1f), ("_RimShadowMask", 0.25f) },
                Colors = new[] { ("_ShadowColor", new Color(0.8f, 0.72f, 0.78f, 1f)), ("_Shadow2ndColor", new Color(0.62f, 0.55f, 0.68f, 0.5f)), ("_SpecularColor", new Color(1f, 0.97f, 0.9f, 0.35f)), ("_RimColor", new Color(1.05f, 1.02f, 1f, 0.6f)) } },
            new Preset { Name = "服", Tooltip = "布らしい2段影、細いリム、輪郭線。光沢なし。",
                Floats = new[] { ("_ShadowBorder", 0.5f), ("_ShadowBlur", 0.08f), ("_Shadow2ndBorder", 0.25f), ("_Shadow2ndBlur", 0.1f), ("_FaceShading", 0f), ("_OutlineWidth", 0.08f), ("_OutlineTexBlend", 0.5f), ("_RimFresnelPower", 3.5f), ("_RimBorder", 0.6f), ("_RimBlur", 0.1f), ("_RimShadowMask", 0.25f) },
                Colors = new[] { ("_ShadowColor", new Color(0.8f, 0.78f, 0.88f, 1f)), ("_Shadow2ndColor", new Color(0.62f, 0.6f, 0.75f, 0.4f)), ("_SpecularColor", new Color(1f, 1f, 1f, 0f)), ("_RimColor", new Color(1.05f, 1.02f, 1f, 0.6f)) } },
            new Preset { Name = "金属", Tooltip = "強いハイライトとリム、濃い影。マットキャップに金属の球テクスチャを入れるとさらに金属らしくなります。",
                Floats = new[] { ("_ShadowBorder", 0.55f), ("_ShadowBlur", 0.03f), ("_Shadow2ndBorder", 0.35f), ("_Shadow2ndBlur", 0.03f), ("_FaceShading", 0f), ("_OutlineWidth", 0.06f), ("_OutlineTexBlend", 0.3f), ("_SpecularSize", 0.12f), ("_SpecularBlur", 0.01f), ("_RimFresnelPower", 2f) },
                Colors = new[] { ("_ShadowColor", new Color(0.55f, 0.55f, 0.62f, 1f)), ("_Shadow2ndColor", new Color(0.35f, 0.35f, 0.42f, 0.8f)), ("_SpecularColor", new Color(1f, 1f, 1f, 0.9f)), ("_RimColor", new Color(1f, 1f, 1f, 0.4f)) } },
            new Preset { Name = "顔パーツ", Tooltip = "目・歯・舌・まつ毛など。影をほぼ付けず、輪郭線なし。",
                Floats = new[] { ("_ShadowBorder", 0.3f), ("_ShadowBlur", 0.2f), ("_FaceShading", 0f), ("_OutlineWidth", 0f) },
                Colors = new[] { ("_ShadowColor", new Color(0.92f, 0.9f, 0.95f, 1f)), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", new Color(1f, 1f, 1f, 0f)), ("_RimColor", new Color(1f, 1f, 1f, 0f)) } },
        };

        // 背景: シーンのメインライト・環境光・追加ライト・ほかの物の影を受ける。輪郭線なし。
        private static (string, float)[] Env(params (string, float)[] extra)
        {
            var list = new System.Collections.Generic.List<(string, float)>
            {
                ("_LightSource", 1f), ("_AmbientStrength", 1f), ("_AdditionalLightStrength", 1f), ("_ShadowReceive", 1f),
                ("_LightMinLimit", 0f), ("_LightMaxLimit", 1.5f), ("_OutlineWidth", 0f), ("_FaceShading", 0f), ("_FaceSphere", 0f),
                ("_ShadowBorder", 0.5f), ("_ShadowBlur", 0.12f), ("_ReflectionSteps", 3f),
            };
            list.AddRange(extra);
            return list.ToArray();
        }

        private static readonly Color NeutralShadow = new Color(0.62f, 0.64f, 0.74f, 1f);
        private static readonly Color NoSpec = new Color(1f, 1f, 1f, 0f);

        // 肌・服の質感: 濡れ・汗・液体の量と、布/ラテックス/革のつや。ライトの受け方・影の色・輪郭線は触らない
        private const string PresetsFoldKey = "GalaxyToonGUI.Fold.Presets";

        // 追加: 今の設定の上に、濡れ・汗・液体だけを足す(ほかの量は消さない)
        private static readonly Preset[] AdditivePresets =
        {
            new Preset { Environment = true, Name = "汗", Tooltip = "汗の粒を足す(汗 0.7、流れない)。",
                Floats = new[] { ("_UseFx", 1f), ("_SweatAmount", 0.7f), ("_SweatReceive", 1f), ("_SweatFlow", 0f) }, Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "液体", Tooltip = "白い液体の筋を足す(液体 0.6、垂れる速さはそのまま)。",
                Floats = new[] { ("_UseFx", 1f), ("_LiquidAmount", 0.6f), ("_LiquidReceive", 1f) }, Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "ウェット", Tooltip = "全体を濡らす(濡れ 1)。",
                Floats = new[] { ("_UseFx", 1f), ("_WetAmount", 1f), ("_WetReceive", 1f) }, Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "流れる汗", Tooltip = "汗の粒がゆっくり下へ流れる(汗 0.7、流れる速さ 0.3)。",
                Floats = new[] { ("_UseFx", 1f), ("_SweatAmount", 0.7f), ("_SweatReceive", 1f), ("_SweatFlow", 0.3f) }, Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "ネバネバ", Tooltip = "納豆の糸のような、とろっと粘る半透明の液を足す(粘り 0.9、ゆっくり垂れる)。糸を引かせるには GalaxyStickyStrings を使う。",
                Floats = new[] { ("_UseFx", 1f), ("_LiquidAmount", 0.5f), ("_LiquidReceive", 1f), ("_LiquidViscosity", 0.9f), ("_LiquidFlow", 0.05f), ("_LiquidGloss", 1.6f), ("_FxBump", 0.5f) },
                Colors = new[] { ("_LiquidColor", new Color(0.95f, 0.88f, 0.7f, 0.55f)) } },
            new Preset { Environment = true, Name = "泡", Tooltip = "白い泡を足す。ゆっくり滑り落ち、泡どうしがつながり、割れて小さくなる(泡 0.7)。",
                Floats = new[] { ("_UseFx", 1f), ("_FoamAmount", 0.7f), ("_FoamReceive", 1f), ("_FoamFlow", 0.15f), ("_FoamPop", 0.12f), ("_FoamMerge", 0.6f), ("_FoamScale", 18f), ("_FoamSize", 0.5f) },
                Colors = new[] { ("_FoamColor", new Color(1f, 1f, 1f, 0.9f)) } },
            new Preset { Environment = true, Name = "雨", Tooltip = "雨に打たれる。上を向いた面(地面・肩・屋根)に波紋が広がり、立った面(壁・体の横)をしずくが流れ落ち、全体が濡れる。雨粒・しぶきのパーティクルは Rain プレハブ。",
                Floats = new[] { ("_UseFx", 1f), ("_RainAmount", 0.7f), ("_RainReceive", 1f), ("_RainScale", 8f), ("_RainSpeed", 1f), ("_WetGloss", 0.8f) }, Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "雨 染み込み", Tooltip = "コンクリート・石・布の壁や地面が雨に打たれて水を吸い、まだらに色が濃くなる(染み込み 1、濡れると暗く 0.55)。つやはあまり出ない。",
                Floats = new[] { ("_UseFx", 1f), ("_RainAmount", 0.7f), ("_RainReceive", 1f), ("_RainScale", 8f), ("_RainSpeed", 1f), ("_WetSoak", 1f), ("_WetDarken", 0.55f), ("_WetGloss", 0.3f) }, Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "転送(消える)", Tooltip = "SF の転送・ワープで消える。足元から光る線が上がり、線の通った所が消える。ディゾルブの量を 0→1 に動かす(GalaxyTeleport を付ければ自動。服の入れ替えも可)。",
                Floats = new[] { ("_UseDissolve", 1f), ("_DissolveAmount", 0f), ("_DissolveMode", 1f), ("_DissolveInvert", 0f), ("_DissolveStart", 0f), ("_DissolveNoiseScale", 1f), ("_DissolveNoiseStrength", 0.15f), ("_DissolveEdgeWidth", 0.03f) },
                Colors = new[] { ("_DissolveEdgeColor", new Color(0.5f, 1.8f, 3.5f, 1f)) } },
            new Preset { Environment = true, Name = "転送(現れる)", Tooltip = "SF の転送で現れる(服の入れ替えの新しい服にも)。ディゾルブの量を 1→0 に動かすと、足元から光る線とともに現れる。",
                Floats = new[] { ("_UseDissolve", 1f), ("_DissolveAmount", 0f), ("_DissolveMode", 1f), ("_DissolveInvert", 1f), ("_DissolveStart", 0f), ("_DissolveNoiseScale", 1f), ("_DissolveNoiseStrength", 0.15f), ("_DissolveEdgeWidth", 0.03f) },
                Colors = new[] { ("_DissolveEdgeColor", new Color(0.5f, 1.8f, 3.5f, 1f)) } },
            new Preset { Environment = true, Name = "汚れ・すす", Tooltip = "火事・爆発・事故のあとのすす汚れ。しみが体に張り付き、つやが消える(汚れ 0.5)。GalaxyToonEffects の dirt でゲーム中に増減。",
                Floats = new[] { ("_UseFx", 1f), ("_DirtAmount", 0.5f), ("_DirtReceive", 1f), ("_DirtScale", 12f) },
                Colors = new[] { ("_DirtColor", new Color(0.12f, 0.1f, 0.09f, 0.85f)) } },
            new Preset { Environment = true, Name = "泥汚れ", Tooltip = "茶色い泥はね(汚れ 0.45、色は泥)。",
                Floats = new[] { ("_UseFx", 1f), ("_DirtAmount", 0.45f), ("_DirtReceive", 1f), ("_DirtScale", 18f) },
                Colors = new[] { ("_DirtColor", new Color(0.35f, 0.25f, 0.15f, 0.8f)) } },
            new Preset { Environment = true, Name = "傷", Tooltip = "細いひっかき傷。下地の明るい色が線になって見え、少しへこむ(傷 0.5)。",
                Floats = new[] { ("_UseFx", 1f), ("_ScratchAmount", 0.5f), ("_DirtReceive", 1f), ("_ScratchScale", 12f) },
                Colors = new[] { ("_ScratchColor", new Color(0.85f, 0.82f, 0.78f, 0.8f)) } },
            new Preset { Environment = true, Name = "流れる液体", Tooltip = "液体の筋が下へ垂れていく(液体 0.6、垂れる速さ 0.5)。GalaxyToonEffects の「乾く速さ」で消していけます。",
                Floats = new[] { ("_UseFx", 1f), ("_LiquidAmount", 0.6f), ("_LiquidReceive", 1f), ("_LiquidFlow", 0.5f) }, Colors = new (string, Color)[0] },
        };

        // キャラの仕上げ: 髪の影・瞳・ラメ・めり込み防止(ほかの値は変えない)
        private static readonly Preset[] FinishPresets =
        {
            new Preset { Environment = true, Name = "アニメ塗り", Tooltip = "日本のアニメ調。影色を地の色から自動で作り(鮮やかに・少し青紫へ)、境界をくっきり、肌の赤い境界線、ライトで色をくすませない、輪郭線を画面上で同じ太さに。肌は「影色の色相ずらし」を + にすると赤みの影に。",
                Floats = new[] { ("_AnimeMode", 1f), ("_AnimeHardness", 0.85f), ("_AnimeShadowHue", -0.03f), ("_AnimeShadowSat", 0.35f), ("_AnimeShadowValue", 0.72f), ("_AnimeEdgeWidth", 0.035f), ("_AnimeFlatLight", 0.8f), ("_AnimeFlatNormal", 0.8f), ("_AnimeNoGloss", 0.85f), ("_AnimeOutlinePixel", 0.7f) },
                Colors = new[] { ("_ShadowColor", new Color(1f, 1f, 1f, 1f)), ("_AnimeEdgeColor", new Color(1f, 0.5f, 0.42f, 0.5f)) } },
            new Preset { Environment = true, Name = "髪の影を落とす", Tooltip = "髪のマテリアルに。この髪の形で、顔・瞳に前髪の影を落とします(顔のマテリアルは「髪の影を受ける」)。",
                Floats = new[] { ("_HairShadowCast", 1f) }, Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "髪の影を受ける", Tooltip = "顔・瞳のマテリアルに。髪の影が光の向きに合わせて落ちます(長さ 1.2cm)。",
                Floats = new[] { ("_HairShadowReceive", 1f), ("_HairShadowDistance", 1.2f), ("_HairShadowBias", 0.3f) }, Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "瞳 くっきり", Tooltip = "瞳に奥行きと大小のハイライト。瞳の範囲はメニュー「Tools/GalaxyToon/瞳の範囲を探す」で自動設定。",
                Floats = new[] { ("_UseEye", 1f), ("_EyeParallax", 0.4f), ("_EyeHighlightSize", 0.2f), ("_EyeWet", 0f) },
                Colors = new[] { ("_EyeHighlightColor", new Color(1f, 1f, 1f, 0.9f)), ("_EyeSparkleColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "瞳 うるうる", Tooltip = "涙ぐんだ瞳。ハイライトが小刻みに揺れ、瞳の下に水面の光が波打つ。涙(GalaxyTears)と一緒に。",
                Floats = new[] { ("_UseEye", 1f), ("_EyeParallax", 0.45f), ("_EyeHighlightSize", 0.24f), ("_EyeWet", 0.9f) },
                Colors = new[] { ("_EyeHighlightColor", new Color(1.2f, 1.2f, 1.25f, 1f)) } },
            new Preset { Environment = true, Name = "瞳 きらめき", Tooltip = "瞳の中で十字の星がまたたく(憧れ・感動・決意の表情に)。",
                Floats = new[] { ("_UseEye", 1f), ("_EyeSparkleScale", 3f), ("_EyeSparkleSpeed", 1.5f) },
                Colors = new[] { ("_EyeSparkleColor", new Color(1.4f, 1.4f, 1.3f, 1f)) } },
            new Preset { Environment = true, Name = "照れ顔", Tooltip = "顔(肌)のマテリアルに。頬がほんのり赤くなり、斜めの照れ線が入る。頬の位置はメニュー「Tools/GalaxyToon/頬の位置を探す」。量は GalaxyToonEffects の blush でも動かせる。",
                Floats = new[] { ("_BlushAmount", 0.8f), ("_BlushSoft", 0.7f), ("_BlushLines", 0.6f) },
                Colors = new[] { ("_BlushColor", new Color(1f, 0.42f, 0.48f, 0.6f)) } },
            new Preset { Environment = true, Name = "ラメ", Tooltip = "細かい鏡の粒が、光と視線の向きでキラキラまたたく(アイドル衣装・ネイル・装飾)。出る所は「ラメの出る所」マスクで。",
                Floats = new[] { ("_GlitterScale", 300f), ("_GlitterSize", 0.25f), ("_GlitterDensity", 0.6f), ("_GlitterSharpness", 12f), ("_GlitterShadowMask", 0.7f) },
                Colors = new[] { ("_GlitterColor", new Color(2f, 2f, 2f, 1f)) } },
            new Preset { Environment = true, Name = "近づくと透ける", Tooltip = "カメラが 30cm より近いと網目で透け、10cm で消える(寄りのカメラで髪・体にめり込んだとき中が見えない)。",
                Floats = new[] { ("_UseNearFade", 1f), ("_NearFadeStart", 0.1f), ("_NearFadeEnd", 0.3f) }, Colors = new (string, Color)[0] },
        };

        private static readonly Preset[] SurfacePresets =
        {
            new Preset { Environment = true, Name = "肌 汗", Tooltip = "肌に汗の粒を出す(汗 0.7)。GalaxyToonEffects を付ければゲーム中に量を変えられます。",
                Floats = new[] { ("_UseFx", 1f), ("_SweatAmount", 0.7f), ("_SweatReceive", 1f), ("_WetAmount", 0f), ("_LiquidAmount", 0f), ("_SweatScale", 35f), ("_SweatSize", 0.3f), ("_SweatFlow", 0f), ("_WetGloss", 0.6f), ("_FxBump", 0.3f) },
                Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "肌 濡れ", Tooltip = "肌全体が濡れて色が濃くなり、てかる(濡れ 1)。",
                Floats = new[] { ("_UseFx", 1f), ("_WetAmount", 1f), ("_WetReceive", 1f), ("_WetDarken", 0.3f), ("_WetGloss", 0.8f), ("_SweatAmount", 0f), ("_LiquidAmount", 0f) },
                Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "肌 液体(白)", Tooltip = "白い液体が垂れた筋になって流れる(液体 0.6)。色は「液体の色」で変えられます。",
                Floats = new[] { ("_UseFx", 1f), ("_LiquidAmount", 0.6f), ("_LiquidReceive", 1f), ("_LiquidScale", 25f), ("_LiquidFlow", 0.1f), ("_LiquidGloss", 1.2f), ("_FxBump", 0.3f), ("_WetAmount", 0f), ("_SweatAmount", 0f) },
                Colors = new[] { ("_LiquidColor", new Color(1f, 1f, 1f, 0.75f)) } },
            new Preset { Environment = true, Name = "涙・しずく", Tooltip = "涙・水滴・雨粒の透明な水(GalaxyTears や糸のマテリアル用)。半透明、両面、鋭いハイライトと縁の光、影を受けない。",
                Floats = new[] { ("_RenderMode", 2f), ("_Cull", 0f), ("_ShadowReceive", 0f), ("_LightMinLimit", 0.8f), ("_Metallic", 0f), ("_Smoothness", 0f), ("_ReflectionStrength", 1f), ("_SpecularSize", 0.2f), ("_SpecularBlur", 0.02f), ("_RimFresnelPower", 1.5f) },
                Colors = new[] { ("_BaseColor", new Color(0.85f, 0.93f, 1f, 0.3f)), ("_RimColor", new Color(0.9f, 0.96f, 1f, 0.8f)), ("_SpecularColor", new Color(1f, 1f, 1f, 1f)) } },
            new Preset { Environment = true, Name = "宝石(屈折)", Tooltip = "宝石・クリスタル・厚いガラス。半透明で後ろの景色がゆがみ、虹色ににじみ、細かい面がきらめく。",
                Floats = new[] { ("_RenderMode", 2f), ("_RefractionStrength", 0.5f), ("_RefractionDispersion", 0.35f), ("_Metallic", 0f), ("_Smoothness", 0.95f), ("_ReflectionStrength", 1.2f), ("_SpecularSize", 0.02f), ("_SpecularBlur", 0.01f), ("_GlitterScale", 40f), ("_GlitterSize", 0.35f), ("_GlitterDensity", 0.8f), ("_GlitterSharpness", 6f), ("_GlitterShadowMask", 0f), ("_RimFresnelPower", 1.5f) },
                Colors = new[] { ("_BaseColor", new Color(0.8f, 0.9f, 1f, 0.35f)), ("_GlitterColor", new Color(2.5f, 2.5f, 2.5f, 1f)), ("_SpecularColor", new Color(1f, 1f, 1f, 1f)), ("_RimColor", new Color(0.9f, 0.95f, 1f, 0.7f)) } },
            new Preset { Environment = true, Name = "服 ソフト", Tooltip = "柔らかい布(綿・ニット)。ぼけた影、つやなし。",
                Floats = new[] { ("_ShadowBlur", 0.2f), ("_Shadow2ndBlur", 0.2f), ("_SpecularSize", 0.05f), ("_Metallic", 0f), ("_Smoothness", 0f), ("_RimFresnelPower", 4f) },
                Colors = new[] { ("_SpecularColor", new Color(1f, 1f, 1f, 0f)), ("_RimColor", new Color(1f, 1f, 1f, 0.08f)), ("_AnisoColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "服 ラテックス", Tooltip = "ラテックス・エナメル。くっきりした影、小さく鋭いハイライト、強い縁のてかりと映り込み。",
                Floats = new[] { ("_ShadowBlur", 0.05f), ("_SpecularSize", 0.015f), ("_SpecularBlur", 0.015f), ("_Metallic", 0f), ("_Smoothness", 0.85f), ("_ReflectionStrength", 0.6f), ("_ReflectionSteps", 3f), ("_RimFresnelPower", 2.5f) },
                Colors = new[] { ("_SpecularColor", new Color(1f, 1f, 1f, 0.5f)), ("_RimColor", new Color(0.85f, 0.9f, 1f, 0.35f)), ("_AnisoColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "服 皮製品", Tooltip = "革(ジャケット・ブーツ・ベルト)。やや広いハイライトと弱い映り込み、しっかりした影。",
                Floats = new[] { ("_ShadowBlur", 0.08f), ("_SpecularSize", 0.06f), ("_SpecularBlur", 0.1f), ("_Metallic", 0f), ("_Smoothness", 0.45f), ("_ReflectionStrength", 0.3f), ("_ReflectionSteps", 3f), ("_RimFresnelPower", 3f) },
                Colors = new[] { ("_SpecularColor", new Color(1f, 0.97f, 0.92f, 0.3f)), ("_RimColor", new Color(1f, 1f, 1f, 0.15f)), ("_AnisoColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "服 濡れ", Tooltip = "雨や水で濡れた服。色が濃く沈み、張りが無くなってふくらみが減り、体に沿う(縮み 1.5cm)。つやは控えめ。雨は表面で跳ね返り、流れるしずくは少量。量は GalaxyToonEffects の wet・rain で動く。",
                Floats = new[] { ("_UseFx", 1f), ("_WetAmount", 1f), ("_WetReceive", 1f), ("_WetDarken", 0.8f), ("_WetSoak", 0.7f), ("_WetGloss", 0.2f), ("_WetShrink", 0.4f), ("_RainReceive", 1f), ("_RainFlowAmount", 0.08f), ("_RainSplash", 1f), ("_RainRipples", 0f), ("_SweatReceive", 0f), ("_SweatAmount", 0f), ("_LiquidAmount", 0f) },
                Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "髪 濡れ", Tooltip = "雨に濡れた髪。当たった雨が小さく跳ね返り、少量の水滴が流れる。隙間が多いので布のようにじわじわ色が濃くなる(染み込み 0.5)。水で毛が束になり(濡れ髪)、ふくらみが縮む(1cm)。量は GalaxyToonEffects の wet で動く。",
                Floats = new[] { ("_UseFx", 1f), ("_WetAmount", 1f), ("_WetReceive", 1f), ("_WetDarken", 0.7f), ("_WetSoak", 0.5f), ("_WetGloss", 0.5f), ("_WetShrink", 0.4f), ("_HairWet", 1f), ("_HairWetClumps", 40f), ("_SweatReceive", 0.3f), ("_SweatScale", 60f), ("_SweatSize", 0.2f), ("_RainReceive", 1f), ("_RainRipples", 0f), ("_RainFlowAmount", 0.12f), ("_RainSplash", 1f), ("_AnisoSharpness", 60f), ("_AnisoBorder", 0.5f), ("_AnisoBlur", 0.15f), ("_AnisoShadowMask", 0.5f), ("_LiquidAmount", 0f) },
                Colors = new[] { ("_AnisoColor", new Color(1f, 0.97f, 0.92f, 0.5f)) } },
            new Preset { Environment = true, Name = "肌 雨", Tooltip = "雨に打たれた肌。皮脂があるので水は染み込まず、すぐに水滴が玉になって流れ落ちる(染み込み 0、水滴と流れるしずく、薄いつや)。波紋は出さない。",
                Floats = new[] { ("_UseFx", 1f), ("_WetReceive", 1f), ("_WetDarken", 0.15f), ("_WetSoak", 0f), ("_WetGloss", 0.8f), ("_WetShrink", 0f), ("_SweatReceive", 1f), ("_SweatScale", 45f), ("_SweatSize", 0.2f), ("_SweatFlow", 0.15f), ("_RainReceive", 1f), ("_RainRipples", 0f), ("_RainScale", 45f), ("_FxBump", 0.3f), ("_LiquidAmount", 0f) },
                Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "革 濡れ", Tooltip = "雨に濡れた革(ベルト・ブーツ・コルセット)。少しだけ水を吸って色が深くなり(染み込み 0.3)、布より早く表面につやが出る。縮まない。",
                Floats = new[] { ("_UseFx", 1f), ("_WetReceive", 1f), ("_WetDarken", 0.65f), ("_WetSoak", 0.3f), ("_WetGloss", 0.7f), ("_WetShrink", 0f), ("_SweatReceive", 0f), ("_RainReceive", 1f), ("_RainRipples", 0f), ("_LiquidAmount", 0f) },
                Colors = new (string, Color)[0] },
            new Preset { Environment = true, Name = "木目", Tooltip = "ニス・ワックスを塗った木(床・家具・楽器)。木目の模様はテクスチャで。木目に沿って筋状に伸びる異方性のつや(UV の横方向)と、弱い映り込み。",
                Floats = new[] { ("_Metallic", 0f), ("_Smoothness", 0.55f), ("_ReflectionStrength", 0.35f), ("_ReflectionSteps", 3f), ("_ShadowBlur", 0.1f), ("_SpecularSize", 0.08f), ("_SpecularBlur", 0.15f), ("_AnisoSharpness", 25f), ("_AnisoBlur", 0.3f), ("_AnisoShadowMask", 0.5f), ("_RimFresnelPower", 4f) },
                Colors = new[] { ("_SpecularColor", new Color(1f, 0.95f, 0.85f, 0.3f)), ("_AnisoColor", new Color(1f, 0.95f, 0.85f, 0.4f)), ("_RimColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "木材", Tooltip = "塗っていない木(角材・板・丸太・木箱)。つやも映り込みも無く、影はやわらかめ。",
                Floats = new[] { ("_Metallic", 0f), ("_Smoothness", 0.1f), ("_ReflectionStrength", 0f), ("_ShadowBlur", 0.2f), ("_SpecularSize", 0.05f), ("_RimFresnelPower", 4f) },
                Colors = new[] { ("_SpecularColor", NoSpec), ("_AnisoColor", new Color(1f, 1f, 1f, 0f)), ("_RimColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "ペットボトル", Tooltip = "薄い透明な樹脂(ペットボトル・ビニール・透明な容器)。半透明・両面で、鋭いハイライト、縁が白く光り、後ろがわずかにゆがむ。中身の色はメインカラー(A)で。",
                Floats = new[] { ("_RenderMode", 2f), ("_Cull", 0f), ("_Metallic", 0f), ("_Smoothness", 0.9f), ("_ReflectionStrength", 0.7f), ("_ReflectionSteps", 0f), ("_RefractionStrength", 0.08f), ("_RefractionDispersion", 0f), ("_SpecularSize", 0.03f), ("_SpecularBlur", 0.02f), ("_RimFresnelPower", 2f), ("_RimBlur", 0.4f), ("_LightMinLimit", 0.5f) },
                Colors = new[] { ("_BaseColor", new Color(0.92f, 0.96f, 1f, 0.25f)), ("_SpecularColor", new Color(1f, 1f, 1f, 0.8f)), ("_RimColor", new Color(1f, 1f, 1f, 0.5f)), ("_AnisoColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "レース", Tooltip = "レース・網・透かし編み。テクスチャの A で穴を抜く(切り抜き)、両面、つやなし、光を少し通す薄い影。",
                Floats = new[] { ("_RenderMode", 1f), ("_Cull", 0f), ("_Cutoff", 0.5f), ("_Metallic", 0f), ("_Smoothness", 0f), ("_ReflectionStrength", 0f), ("_ShadowBlur", 0.25f), ("_SpecularSize", 0.05f), ("_RimFresnelPower", 4f), ("_LightMinLimit", 0.25f) },
                Colors = new[] { ("_SpecularColor", NoSpec), ("_RimColor", new Color(1f, 1f, 1f, 0.12f)), ("_AnisoColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "水", Tooltip = "水面・水たまり・水の入った容器。半透明で後ろがゆがみ、鏡のような映り込みと鋭いハイライト。上を向いた面には雨の波紋がゆっくり広がる(止めるなら「雨」を 0)。色はメインカラー(A)で。",
                Floats = new[] { ("_RenderMode", 2f), ("_Metallic", 0f), ("_Smoothness", 1f), ("_ReflectionStrength", 1.5f), ("_ReflectionSteps", 0f), ("_RefractionStrength", 0.3f), ("_RefractionDispersion", 0.05f), ("_SpecularSize", 0.03f), ("_SpecularBlur", 0.01f), ("_RimFresnelPower", 2f), ("_RimBlur", 0.4f), ("_LightMinLimit", 0.4f), ("_ShadowReceive", 0f), ("_UseFx", 1f), ("_RainAmount", 0.5f), ("_RainReceive", 1f), ("_RainScale", 6f), ("_RainSpeed", 0.6f) },
                Colors = new[] { ("_BaseColor", new Color(0.55f, 0.8f, 0.95f, 0.45f)), ("_SpecularColor", new Color(1f, 1f, 1f, 1f)), ("_RimColor", new Color(0.8f, 0.95f, 1f, 0.6f)), ("_AnisoColor", new Color(1f, 1f, 1f, 0f)) } },
        };

        private static readonly Preset[] EnvironmentPresets =
        {
            new Preset { Environment = true, Name = "金属", Tooltip = "むき出しの金属(配管・手すり・機械)。金属 1・つや 0.7、映り込みが地の色に染まる。",
                Floats = Env(("_Metallic", 1f), ("_Smoothness", 0.7f), ("_ReflectionStrength", 1f), ("_ShadowBlur", 0.06f)),
                Colors = new[] { ("_ShadowColor", new Color(0.5f, 0.52f, 0.6f, 1f)), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", NoSpec), ("_RimColor", new Color(1f, 1f, 1f, 0.15f)) } },
            new Preset { Environment = true, Name = "塗装金属", Tooltip = "塗装した金属(扉・ロッカー・機械の外板)。金属 0・つや 0.5 の塗膜のつや。",
                Floats = Env(("_Metallic", 0f), ("_Smoothness", 0.5f), ("_ReflectionStrength", 0.6f)),
                Colors = new[] { ("_ShadowColor", NeutralShadow), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", NoSpec), ("_RimColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "コンクリート", Tooltip = "コンクリート・石・床。つやなし、ぼけた影。UV が無いならトライプラナーを ON。",
                Floats = Env(("_Metallic", 0f), ("_Smoothness", 0.05f), ("_ReflectionStrength", 0f), ("_ShadowBlur", 0.25f)),
                Colors = new[] { ("_ShadowColor", new Color(0.66f, 0.66f, 0.72f, 1f)), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", NoSpec), ("_RimColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "プラスチック", Tooltip = "樹脂・ゴム以外の小物。金属 0・つや 0.65、白いハイライト。",
                Floats = Env(("_Metallic", 0f), ("_Smoothness", 0.65f), ("_ReflectionStrength", 0.4f), ("_ShadowBlur", 0.08f)),
                Colors = new[] { ("_ShadowColor", NeutralShadow), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", NoSpec), ("_RimColor", new Color(1f, 1f, 1f, 0.1f)) } },
            new Preset { Environment = true, Name = "発光パネル", Tooltip = "照明・モニター・看板。影を薄くし、発光色を入れる(発光色はあとで好みに)。ライトマップを焼くと周りも照らす。",
                Floats = Env(("_Metallic", 0f), ("_Smoothness", 0.3f), ("_ReflectionStrength", 0.2f)),
                Colors = new[] { ("_ShadowColor", new Color(0.85f, 0.85f, 0.9f, 1f)), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", NoSpec), ("_RimColor", new Color(1f, 1f, 1f, 0f)), ("_EmissionColor", new Color(1.5f, 1.5f, 1.4f, 1f)) } },
            new Preset { Environment = true, Name = "ガラス", Tooltip = "窓・レンズ。描画モードを「半透明」にして使う。つや 0.95 の強い映り込み。",
                Floats = Env(("_Metallic", 0f), ("_Smoothness", 0.95f), ("_ReflectionStrength", 1.2f), ("_ReflectionSteps", 0f)),
                Colors = new[] { ("_ShadowColor", new Color(0.9f, 0.9f, 0.95f, 1f)), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", NoSpec), ("_RimColor", new Color(0.8f, 0.9f, 1f, 0.3f)) } },
            new Preset { Environment = true, Name = "紙", Tooltip = "紙・段ボール・布張り。つやも映り込みも無く、影はやわらかく薄め(光を少し通す感じ)。",
                Floats = Env(("_Metallic", 0f), ("_Smoothness", 0.02f), ("_ReflectionStrength", 0f), ("_ShadowBlur", 0.3f), ("_ShadowBorder", 0.45f), ("_LightMaxLimit", 1.2f)),
                Colors = new[] { ("_ShadowColor", new Color(0.8f, 0.78f, 0.78f, 1f)), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", NoSpec), ("_RimColor", new Color(1f, 1f, 1f, 0f)) } },
            new Preset { Environment = true, Name = "透過プラスチック", Tooltip = "ヘッドライトのカバー・色付きハザード・航行灯。描画モードを「半透明」にし、強い映り込みと縁の光を入れます。色と透け具合はメインカラー(A)、点灯は「発光色」で。",
                Floats = Env(("_RenderMode", 2f), ("_Metallic", 0f), ("_Smoothness", 0.9f), ("_ReflectionStrength", 0.8f), ("_ReflectionSteps", 0f), ("_ShadowBlur", 0.2f), ("_LightMinLimit", 0.3f), ("_RimFresnelPower", 2.5f), ("_RimBlur", 0.5f)),
                Colors = new[] { ("_ShadowColor", new Color(0.85f, 0.85f, 0.9f, 0.5f)), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", new Color(1f, 1f, 1f, 0.6f)), ("_RimColor", new Color(1f, 1f, 1f, 0.35f)) } },
            new Preset { Environment = true, Name = "ダイヤモンド", Tooltip = "宝石・クリスタル。鏡のような強い映り込み、鋭いハイライト、強い縁の光。影はほぼ付けない。",
                Floats = Env(("_Metallic", 0.2f), ("_Smoothness", 1f), ("_ReflectionStrength", 2f), ("_ReflectionSteps", 4f), ("_ShadowBlur", 0.05f), ("_LightMinLimit", 0.5f), ("_LightMaxLimit", 3f), ("_SpecularSize", 0.02f), ("_SpecularBlur", 0.01f), ("_RimFresnelPower", 1.5f), ("_RimBlur", 0.3f)),
                Colors = new[] { ("_ShadowColor", new Color(0.7f, 0.75f, 0.9f, 0.4f)), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", new Color(1f, 1f, 1f, 1f)), ("_RimColor", new Color(0.9f, 0.95f, 1f, 0.8f)) } },
            new Preset { Environment = true, Name = "マグマ", Tooltip = "溶岩・溶けた金属。赤橙の発光がゆっくり脈打ち、影は黒い岩肌。発光の形はテクスチャの明るい所(発光マスク)で決まります。",
                Floats = Env(("_Metallic", 0f), ("_Smoothness", 0.1f), ("_ReflectionStrength", 0f), ("_ShadowBlur", 0.1f), ("_LightMaxLimit", 4f), ("_EmissionBlink", 0.4f)),
                Colors = new[] { ("_ShadowColor", new Color(0.25f, 0.08f, 0.05f, 1f)), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", NoSpec), ("_RimColor", new Color(1f, 0.4f, 0.1f, 0.4f)), ("_EmissionColor", new Color(3f, 0.8f, 0.1f, 1f)) } },
            new Preset { Environment = true, Name = "建物の壁", Tooltip = "外壁・内装のつや消し塗装。つや 0.2、やわらかい影。",
                Floats = Env(("_Metallic", 0f), ("_Smoothness", 0.2f), ("_ReflectionStrength", 0.3f), ("_ShadowBlur", 0.18f)),
                Colors = new[] { ("_ShadowColor", NeutralShadow), ("_Shadow2ndColor", new Color(1f, 1f, 1f, 0f)), ("_SpecularColor", NoSpec), ("_RimColor", new Color(1f, 1f, 1f, 0f)) } },
        };

        /// <summary>名前でプリセットを当てる(ツール用)。見つかれば true。</summary>
        public static bool ApplyPreset(Material material, string name)
        {
            foreach (Preset[] row in new[] { Presets, EnvironmentPresets, SurfacePresets, AdditivePresets, FinishPresets })
                foreach (Preset p in row)
                    if (p.Name == name) { p.Apply(material); Apply(material); return true; }
            return false;
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            Apply(material);
        }

        /// <summary>描画モードに合わせて、ブレンド・ZWrite・描画順・キーワード・パスの有効/無効を揃える。</summary>
        private static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
        }

        public static void Apply(Material material)
        {
            var mode = (RenderMode)Mathf.RoundToInt(material.GetFloat("_RenderMode"));
            bool transparent = mode == RenderMode.Transparent;
            bool cutout = mode == RenderMode.Cutout;
            material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            if (cutout) material.EnableKeyword("_ALPHATEST_ON"); else material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
            int baseQueue = (int)(transparent ? RenderQueue.Transparent : cutout ? RenderQueue.AlphaTest : RenderQueue.Geometry);
            bool stencilReader = material.GetFloat("_StencilComp") == (float)CompareFunction.NotEqual && !transparent;
            int offset = material.HasProperty("_QueueOffset") ? Mathf.RoundToInt(material.GetFloat("_QueueOffset")) : 0;
            material.renderQueue = baseQueue + (stencilReader ? 1 : 0) + offset; // ステンシルで「透かす」側は書き込む側の後に、重ねた文字・デカールは「描画順のずらし」ぶん後に描く
            // テクスチャが入っているときだけ使う機能(入っていなければ計算しない)
            SetKeyword(material, "_SHADOW_RAMP", material.GetTexture("_ShadowRampTex") != null);
            // ランプの部位分け・ハイライトのランプはキーワードを増やさず値で切り替える(切り替えでコンパイルが走らない)
            if (material.HasProperty("_RampIdOn")) material.SetFloat("_RampIdOn", material.GetTexture("_RampIdMap") != null ? 1f : 0f);
            if (material.HasProperty("_SpecularRampOn")) material.SetFloat("_SpecularRampOn", material.GetTexture("_SpecularRampTex") != null ? 1f : 0f);
            // トグル式の機能(プリセットやスクリプトから値だけ変えたときも、キーワードを合わせる)
            SetKeyword(material, "_GALAXY_FX", material.GetFloat("_UseFx") > 0.5f);
            SetKeyword(material, "_DISSOLVE", material.GetFloat("_UseDissolve") > 0.5f);
            SetKeyword(material, "_DEPTH_RIM", material.GetFloat("_DepthRim") > 0.5f);
            SetKeyword(material, "_FACE_SDF", material.GetFloat("_UseFaceSdf") > 0.5f);
            if (material.HasProperty("_AnimeMode")) SetKeyword(material, "_ANIME", material.GetFloat("_AnimeMode") > 0.5f);
            SetKeyword(material, "_TRIPLANAR", material.GetFloat("_UseTriplanar") > 0.5f);
            SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") != null);
            SetKeyword(material, "_METALLICGLOSSMAP", material.GetTexture("_MetallicGlossMap") != null);
            SetKeyword(material, "_EMISSION2", material.GetTexture("_Emission2Map") != null);
            // 値が「効かない」機能は外す(計算もビルドもしない)
            SetKeyword(material, "_FACE", material.GetFloat("_FaceShading") > 0f || material.GetFloat("_FaceSphere") > 0f);
            SetKeyword(material, "_SHADOW2ND", material.GetColor("_Shadow2ndColor").a > 0f);
            SetKeyword(material, "_GLOSSY", material.GetFloat("_Metallic") > 0f || material.GetFloat("_Smoothness") > 0f);
            SetKeyword(material, "_ADDLIGHT", material.GetFloat("_AdditionalLightStrength") > 0f);
            SetKeyword(material, "_SPECULAR", material.GetColor("_SpecularColor").a > 0f);
            SetKeyword(material, "_RIMLIGHT", material.GetColor("_RimColor").a > 0f);
            SetKeyword(material, "_MATCAP", material.GetTexture("_MatCapTex") != null && material.GetColor("_MatCapColor").a > 0f);
            SetKeyword(material, "_COLORADJUST", Mathf.Abs(material.GetFloat("_HueShift")) > 1e-4f || Mathf.Abs(material.GetFloat("_Saturation") - 1f) > 1e-4f || Mathf.Abs(material.GetFloat("_Brightness") - 1f) > 1e-4f || Mathf.Abs(material.GetFloat("_Gamma") - 1f) > 1e-4f);
            SetKeyword(material, "_SHADOW3RD", material.GetColor("_Shadow3rdColor").a > 0f);
            SetKeyword(material, "_ANISOTROPY", material.GetColor("_AnisoColor").a > 0f);
            SetKeyword(material, "_BACKLIGHT", material.GetColor("_BacklightColor").a > 0f);
            SetKeyword(material, "_MATCAP2", material.GetTexture("_MatCap2Tex") != null && material.GetColor("_MatCap2Color").a > 0f);
            SetKeyword(material, "_NORMALMAP2", material.GetTexture("_Bump2Map") != null);
            Color emission = material.GetColor("_EmissionColor"), emission2 = material.GetColor("_Emission2Color");
            SetKeyword(material, "_EMISSION", emission.maxColorComponent > 0f || (material.GetTexture("_Emission2Map") != null && emission2.maxColorComponent > 0f));
            for (int i = 1; i <= 3; i++) SetKeyword(material, "_LAYER" + i, material.GetTexture($"_Layer{i}Tex") != null);
            SetKeyword(material, "_HAIR_SHADOW", material.GetFloat("_HairShadowReceive") > 0f);
            SetKeyword(material, "_EYE", material.GetFloat("_UseEye") > 0.5f);
            SetKeyword(material, "_GLITTER", material.GetColor("_GlitterColor").a > 0f);
            SetKeyword(material, "_REFRACTION", transparent && material.GetFloat("_RefractionStrength") > 0f);
            SetKeyword(material, "_NEAR_FADE", material.GetFloat("_UseNearFade") > 0.5f);
            material.SetShaderPassEnabled("SRPDefaultUnlit", !transparent && material.GetFloat("_OutlineWidth") > 0f); // 輪郭線(太さ 0 なら描画そのものをしない)
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            material.SetShaderPassEnabled("DepthOnly", !transparent);
            material.SetShaderPassEnabled("DepthNormals", !transparent);
            EditorUtility.SetDirty(material);
        }
    }
}
