using System.Collections.Generic;
using UnityEditor;

namespace GalaxyToon.Editor
{
    /// <summary>GalaxyToonGUI の表示言語(日本語 / English)。既定は日本語、選択は EditorPrefs に記憶。
    /// 日本語はシェーダの displayName・GUI のコード中の文字をそのまま使い、English のときだけここの表に置き換える。
    /// 内部の名前(見出しの開閉の記録・プリセット名で当てるツール)は日本語のままなので、言語を変えても設定は消えない。</summary>
    public static class GalaxyToonText
    {
        private const string LanguageKey = "GalaxyToonGUI.Language";
        public static readonly string[] LanguageNames = { "日本語", "English" };

        public static bool English
        {
            get => EditorPrefs.GetInt(LanguageKey, 0) == 1;
            set => EditorPrefs.SetInt(LanguageKey, value ? 1 : 0);
        }

        /// <summary>GUI の文字: 日本語と英語を並べて書き、選んでいる方を返す。</summary>
        public static string L(string ja, string en) => English ? en : ja;

        /// <summary>シェーダの項目名。英語の表に無ければ displayName(日本語)のまま。</summary>
        public static string Property(MaterialProperty p) =>
            English && PropertyEn.TryGetValue(p.name, out string en) ? en : p.displayName;

        public static string Section(string title) =>
            English && SectionEn.TryGetValue(title, out string en) ? en : title;

        public static string PresetName(string name) =>
            English && PresetEn.TryGetValue(name, out var en) ? en.name : name;

        public static string PresetTooltip(string name, string tooltip)
        {
            if (!English) return tooltip;
            if (DuplicateNameTooltipEn.TryGetValue(tooltip, out string byTooltip)) return byTooltip;
            return PresetEn.TryGetValue(name, out var en) ? en.tooltip : tooltip;
        }

        // 同じ名前のプリセットが2つある(キャラの「金属」と背景の「金属」)ので、背景側は日本語の説明文で見分ける
        private static readonly Dictionary<string, string> DuplicateNameTooltipEn = new Dictionary<string, string>
        {
            { "むき出しの金属(配管・手すり・機械)。金属 1・つや 0.7、映り込みが地の色に染まる。",
              "Bare metal (pipes, handrails, machines). Metallic 1, smoothness 0.7; reflections are tinted by the base color." },
        };

        private static readonly Dictionary<string, string> SectionEn = new Dictionary<string, string>
        {
            { "基本(色・立体感)", "Basic (Color / Normal)" },
            { "色調補正", "Color Adjustment" },
            { "金属・つや・映り込み", "Metallic / Smoothness / Reflection" },
            { "ラメ・屈折 (宝石・ガラス)", "Glitter / Refraction (Gems, Glass)" },
            { "トライプラナー (UV なしで貼る)", "Triplanar (No UVs Needed)" },
            { "ライト", "Lighting" },
            { "影", "Shadow" },
            { "アニメ塗り (日本のアニメ調)", "Anime Shading (Japanese Anime Style)" },
            { "顔", "Face" },
            { "瞳 (奥行き・ハイライト・うるうる・きらめき)", "Eyes (Depth / Highlights / Teary / Sparkle)" },
            { "頬染め (照れ)", "Blush (Embarrassed)" },
            { "髪の揺れ", "Hair Sway" },
            { "髪の影 (前髪の影を顔に落とす)", "Hair Shadow (Bangs Cast Shadow on Face)" },
            { "ハイライト・マットキャップ", "Highlight / MatCap" },
            { "異方性ハイライト (髪の天使の輪)", "Anisotropic Highlight (Hair Angel Ring)" },
            { "逆光ライト", "Backlight" },
            { "リムライト", "Rim Light" },
            { "発光", "Emission" },
            { "レイヤー1", "Layer 1" },
            { "レイヤー2", "Layer 2" },
            { "レイヤー3", "Layer 3" },
            { "濡れ・汗・液体・汚れ", "Wet / Sweat / Liquid / Dirt" },
            { "輪郭線", "Outline" },
            { "カメラが近いと透ける", "Fade When Camera Is Close" },
            { "ディゾルブ (消える演出)", "Dissolve (Vanish Effect)" },
            { "ステンシル (瞳を髪に透かす など)", "Stencil (Show Eyes Through Hair, etc.)" },
            { "その他", "Other" },
        };

        private static readonly Dictionary<string, string> PropertyEn = BuildPropertyEn();

        private static Dictionary<string, string> BuildPropertyEn()
        {
            var d = new Dictionary<string, string>
            {
                // 基本
                { "_BaseMap", "Main Texture" },
                { "_BaseColor", "Main Color" },
                { "_Cutoff", "Alpha Cutoff" },
                { "_QueueOffset", "Render Queue Offset (+1 draws text/decals on the same surface on top)" },
                { "_BumpMap", "Normal Map" },
                { "_BumpScale", "Normal Strength" },
                { "_OcclusionMap", "Occlusion (AO Map)" },
                { "_OcclusionStrength", "Occlusion Strength" },
                { "_Bump2Map", "Detail Normal Map (fine bumps)" },
                { "_Bump2Scale", "Detail Normal Strength" },
                // 色調補正
                { "_HueShift", "Hue Shift" },
                { "_Saturation", "Saturation" },
                { "_Brightness", "Brightness" },
                { "_Gamma", "Gamma (1 = unchanged)" },
                // 金属・つや
                { "_Metallic", "Metallic (0 = non-metal, 1 = metal)" },
                { "_Smoothness", "Smoothness (0 = rough, 1 = glossy)" },
                { "_MetallicGlossMap", "Metallic / Smoothness Map (R = metallic, A = smoothness)" },
                { "_ReflectionStrength", "Reflection Strength" },
                { "_ReflectionSteps", "Reflection Steps (0 = smooth)" },
                // トライプラナー
                { "_UseTriplanar", "Triplanar (project in world space without UVs)" },
                { "_TriplanarScale", "Triplanar Tiling (per meter)" },
                { "_TriplanarBlend", "Triplanar Edge Sharpness" },
                // ライト
                { "_LightSource", "Main Light" },
                { "_LightMinLimit", "Min Brightness" },
                { "_LightMaxLimit", "Max Brightness" },
                { "_MonochromeLighting", "Ignore Light Color" },
                { "_AmbientStrength", "Ambient Strength" },
                { "_AdditionalLightStrength", "Additional Light Strength" },
                { "_BakedSteps", "Baked Light Steps (0 = smooth)" },
                // 影
                { "_ShadowColor", "Shadow 1 Color (A = opacity)" },
                { "_ShadowBorder", "Shadow 1 Border" },
                { "_ShadowBlur", "Shadow 1 Blur" },
                { "_Shadow2ndColor", "Shadow 2 Color (A = opacity)" },
                { "_Shadow2ndBorder", "Shadow 2 Border" },
                { "_Shadow2ndBlur", "Shadow 2 Blur" },
                { "_Shadow3rdColor", "Shadow 3 Color (A = opacity)" },
                { "_Shadow3rdBorder", "Shadow 3 Border" },
                { "_Shadow3rdBlur", "Shadow 3 Blur" },
                { "_ShadowStrengthMask", "Shadow Strength Mask (R)" },
                { "_ShadowRampTex", "Shadow Ramp (left = dark, right = bright)" },
                { "_ShadowRampRows", "Ramp Rows (rows stacked vertically, one per body part)" },
                { "_ShadowRampRow", "Ramp Row (used when there is no part ID map; 0 = top)" },
                { "_RampIdMap", "Part ID Map (R = row to use; 0 = top ... 1 = bottom)" },
                { "_ShadowReceive", "Receive Shadows from Other Objects" },
                { "_AAStrength", "Border Anti-aliasing" },
                // 顔・髪の影・瞳・頬
                { "_FaceShading", "Flatten Face Shading" },
                { "_FaceSphere", "Shade Face as a Sphere" },
                { "_UseFaceSdf", "Use SDF Face Shadow" },
                { "_FaceSdfTex", "SDF Face Shadow (R = light from right, G = from left)" },
                { "_HairShadowCast", "Cast Hair Shadow (this hair shades the face and eyes; enable on hair materials)" },
                { "_HairShadowReceive", "Receive Hair Shadow (1 on face and eye materials)" },
                { "_HairShadowDistance", "Hair Shadow Length (cm, offset toward the light)" },
                { "_HairShadowBias", "Hair Shadow Gap (cm, avoids too much shadow where hair touches skin)" },
                { "_UseEye", "Use Eye Effects (depth, highlights, teary, sparkle)" },
                { "_EyeRect1", "Iris 1 Area (UV: XY = bottom-left, ZW = top-right. Auto-filled by the Find Iris Area tool)" },
                { "_EyeRect2", "Iris 2 Area (UV: XY = bottom-left, ZW = top-right)" },
                { "_EyeParallax", "Iris Depth (the iris looks recessed depending on the view angle)" },
                { "_EyeHighlightColor", "Eye Highlight Color (A = strength)" },
                { "_EyeHighlightPos", "Eye Highlight Position (XY = large, ZW = small. -1 to 1, screen right = +X, up = +Y)" },
                { "_EyeHighlightSize", "Eye Highlight Size" },
                { "_EyeWet", "Teary Eyes (highlights tremble, water shimmer below)" },
                { "_EyeSparkleColor", "Sparkle Color (A = strength)" },
                { "_EyeSparkleScale", "Sparkle Count" },
                { "_EyeSparkleSpeed", "Sparkle Speed" },
                { "_BlushAmount", "Blush (can be driven by blush on GalaxyToonEffects)" },
                { "_BlushColor", "Blush Color (A = opacity)" },
                { "_BlushUV", "Cheek Positions (UV: XY = cheek 1, ZW = cheek 2. Auto-set by the Find Cheek Positions menu)" },
                { "_BlushSize", "Blush Size (UV)" },
                { "_BlushSoft", "Blush Softness" },
                { "_BlushLines", "Blush Lines (diagonal hatching)" },
                { "_HairSway", "Hair Sway (add GalaxyToonHairSway to the character)" },
                // ハイライト・マットキャップ・異方性
                { "_SpecularColor", "Highlight Color (A = strength)" },
                { "_SpecularSize", "Highlight Size" },
                { "_SpecularBlur", "Highlight Blur" },
                { "_SpecularRampTex", "Highlight Ramp (left = outer edge, right = center. Sets highlight shape and color; rows match the shadow ramp)" },
                { "_MatCapTex", "MatCap" },
                { "_MatCapColor", "MatCap Color (A = strength)" },
                { "_MatCapShadowMask", "Hide MatCap in Shadow" },
                { "_MatCap2Tex", "MatCap 2" },
                { "_MatCap2Color", "MatCap 2 Color (A = strength)" },
                { "_MatCap2Blend", "MatCap 2 Blend" },
                { "_MatCap2Mask", "MatCap 2 Mask (R)" },
                { "_MatCap2ShadowMask", "Hide MatCap 2 in Shadow" },
                { "_AnisoColor", "Anisotropic Highlight Color (A = strength)" },
                { "_AnisoDirection", "Anisotropic Direction (hair flow)" },
                { "_AnisoShift", "Anisotropic Shift" },
                { "_AnisoSharpness", "Anisotropic Sharpness" },
                { "_AnisoBorder", "Anisotropic Border" },
                { "_AnisoBlur", "Anisotropic Blur" },
                { "_AnisoShadowMask", "Hide Anisotropic Highlight in Shadow" },
                { "_AnisoUseRamp", "Shape the Angel Ring with the Highlight Ramp" },
                { "_HairWet", "Wet Hair (hair clumps into strands, gaps darken, the angel ring gets thin and sharp. Amount = Wet)" },
                { "_HairWetClumps", "Wet Hair Clump Density (per UV unit)" },
                // リム・逆光
                { "_RimColor", "Rim Light Color (A = strength)" },
                { "_RimBorder", "Rim Border" },
                { "_RimBlur", "Rim Blur" },
                { "_RimFresnelPower", "Rim Thinness" },
                { "_RimShadowMask", "Hide Rim in Shadow" },
                { "_DepthRim", "Depth Rim (light only the silhouette)" },
                { "_RimDepthWidth", "Depth Rim Width (cm)" },
                { "_RimDepthThreshold", "Depth Rim Depth Threshold (m)" },
                { "_BacklightColor", "Backlight Color (A = strength)" },
                { "_BacklightBorder", "Backlight Border" },
                { "_BacklightBlur", "Backlight Blur" },
                { "_BacklightDirectivity", "Backlight Directivity (only when the light is directly behind)" },
                { "_BacklightViewStrength", "Backlight View Influence" },
                { "_BacklightMainStrength", "Mix Texture Color into Backlight" },
                // ラメ・屈折
                { "_GlitterColor", "Glitter Color (A = strength)" },
                { "_GlitterMask", "Glitter Mask (R)" },
                { "_GlitterScale", "Glitter Density (flakes per UV unit)" },
                { "_GlitterSize", "Glitter Flake Size" },
                { "_GlitterDensity", "Glitter Amount" },
                { "_GlitterSharpness", "Glitter Sparkle Sharpness (higher = brief flashes)" },
                { "_GlitterShadowMask", "Hide Glitter in Shadow" },
                { "_RefractionStrength", "Refraction (transparent only; distorts what is behind)" },
                { "_RefractionDispersion", "Refraction Rainbow Dispersion" },
                // 発光
                { "_EmissionMap", "Emission Mask" },
                { "_EmissionColor", "Emission Color" },
                { "_EmissionAnimate", "Emission Auto Scroll" },
                { "_EmissionScroll", "Emission Motion (XY = scroll/sec, Z = rotation deg, W = rotation deg/sec)" },
                { "_EmissionBlink", "Emission Blink (times/sec, 0 = off)" },
                { "_Emission2Map", "Emission Layer 2 Mask" },
                { "_Emission2Color", "Emission Layer 2 Color" },
                { "_Emission2Animate", "Emission Layer 2 Auto Scroll" },
                { "_Emission2Scroll", "Emission Layer 2 Motion (XY = scroll/sec, Z = rotation deg, W = rotation deg/sec)" },
                { "_Emission2Blink", "Emission Layer 2 Blink (times/sec, 0 = off)" },
                // 濡れ・汗・液体・泡・雨・汚れ
                { "_UseFx", "Use Wet / Sweat / Liquid" },
                { "_FxMask", "Effect Mask (R = wet, G = sweat, B = liquid, A = foam)" },
                { "_WetAmount", "Wet" },
                { "_WetReceive", "Wet Receive Strength" },
                { "_WetDarken", "Darken When Wet (multiplies by the texture's own color: 0.5 = squared, 1 = cubed. Cloth and leather deepen in color)" },
                { "_WetGloss", "Wet Gloss" },
                { "_WetSoak", "Soak (concrete, stone, cloth: wet areas absorb water and darken in patches)" },
                { "_WetShrink", "Shrink When Wet (cloth and fur lose volume and cling. Inward offset along the normal at wet 1, in cm. Fluffy tails: 4-6)" },
                { "_SweatAmount", "Sweat" },
                { "_SweatReceive", "Sweat Receive Strength (skin = 1, clothes = 0)" },
                { "_SweatScale", "Sweat Droplet Density" },
                { "_SweatSize", "Sweat Droplet Size" },
                { "_SweatFlow", "Sweat Flow Speed" },
                { "_LiquidAmount", "Liquid" },
                { "_LiquidReceive", "Liquid Receive Strength" },
                { "_LiquidColor", "Liquid Color (A = opacity)" },
                { "_LiquidScale", "Liquid Streak Density" },
                { "_LiquidFlow", "Liquid Drip Speed" },
                { "_LiquidGloss", "Liquid Gloss" },
                { "_LiquidViscosity", "Liquid Viscosity (0 = runny, 1 = sticky)" },
                { "_LiquidFlowDir", "Liquid Flow Direction (object-space XYZ. Default is down; toward the tongue tip inside a mouth)" },
                { "_FoamAmount", "Foam" },
                { "_FoamReceive", "Foam Receive Strength" },
                { "_FoamColor", "Foam Color (A = opacity)" },
                { "_FoamScale", "Foam Density (large bubbles per UV unit)" },
                { "_FoamSize", "Foam Size (radius relative to the cell)" },
                { "_FoamFlow", "Foam Slide Speed" },
                { "_FoamPop", "Foam Pop Rate (times/sec)" },
                { "_FoamMerge", "Foam Bubble Merging" },
                { "_RainAmount", "Rain" },
                { "_RainReceive", "Rain Receive Strength (0 under a roof)" },
                { "_RainRipples", "Rain Ripples (ground and puddles = 1, clothes, hair, skin = 0)" },
                { "_RainFlowAmount", "Rain Drip Amount (small for clothes and hair)" },
                { "_RainSplash", "Rain Splash on Surface (GalaxyToonEffects)" },
                { "_RainScale", "Rain Ripple / Drip Density (per meter)" },
                { "_RainSpeed", "Rain Speed (ripple spread, drip flow)" },
                { "_DirtAmount", "Dirt / Soot" },
                { "_DirtReceive", "Dirt / Scratch Receive Strength" },
                { "_DirtColor", "Dirt Color (A = opacity)" },
                { "_DirtScale", "Dirt Density (per meter)" },
                { "_ScratchAmount", "Scratches" },
                { "_ScratchColor", "Scratch Color (A = opacity)" },
                { "_ScratchScale", "Scratch Density (per UV unit)" },
                { "_FxBump", "Droplet / Streak Bump" },
                // ディゾルブ
                { "_UseDissolve", "Use Dissolve" },
                { "_DissolveAmount", "Dissolve Amount (0 = intact, 1 = gone)" },
                { "_DissolveMode", "Dissolve Mode (Noise / Position)" },
                { "_DissolveDirection", "Position: Dissolve Direction (XYZ = world, W = range in m)" },
                { "_DissolveStart", "Position: Start Offset (distance from the origin in m. To dissolve from the top, use direction 0,-1,0 and start = -height)" },
                { "_DissolveNoise", "Noise Texture (R. Uses built-in noise if empty)" },
                { "_DissolveNoiseScale", "Noise Scale" },
                { "_DissolveNoiseStrength", "Noise Strength (2 or more uses the texture)" },
                { "_DissolveInvert", "Invert (appear instead of vanish; for the new outfit in an outfit swap)" },
                { "_DissolveWorld", "Position: Measure by World Height (same line across all body parts; for teleport effects)" },
                { "_DissolveEdgeWidth", "Edge Width" },
                { "_DissolveEdgeColor", "Edge Color (A = strength)" },
                // ステンシル
                { "_StencilRef", "Stencil Value" },
                { "_StencilReadMask", "Stencil Read Mask" },
                { "_StencilWriteMask", "Stencil Write Mask" },
                { "_StencilComp", "Stencil Compare" },
                { "_StencilPass", "Stencil Pass Op" },
                { "_StencilFail", "Stencil Fail Op" },
                { "_StencilZFail", "Stencil Z-Fail Op" },
                // カメラが近いと透ける
                { "_UseNearFade", "Fade When Camera Is Close (prevents seeing inside when clipping)" },
                { "_NearFadeStart", "Fade: Fully Hidden Closer Than (m)" },
                { "_NearFadeEnd", "Fade: Fully Visible Farther Than (m)" },
                // 輪郭線
                { "_OutlineWidth", "Outline Width (cm)" },
                { "_OutlineSmoothNormal", "Use Smoothed Normals for Outline (for meshes baked with the tool; no breaks at corners)" },
                { "_OutlineColor", "Outline Color" },
                { "_OutlineTexBlend", "Mix Texture Color into Outline" },
                { "_OutlineLit", "Match Outline to Light Brightness" },
                { "_OutlineFixWidth", "Thinner When Close" },
                { "_AnimeOutlinePixel", "Constant Screen-Space Outline Width (anime lines; 1 = no thinning with distance)" },
                { "_OutlineWidthMask", "Outline Width Mask (R)" },
                // アニメ塗り
                { "_AnimeMode", "Anime Shading" },
                { "_AnimeHardness", "Crisp Shadow Edges (cel shading)" },
                { "_AnimeShadowHue", "Shadow Hue Shift (- toward blue-violet / + toward red)" },
                { "_AnimeShadowSat", "Shadow Saturation (shadow color is derived from the base color)" },
                { "_AnimeShadowValue", "Shadow Brightness" },
                { "_AnimeEdgeColor", "Shadow Edge Color (A = strength; e.g. a red terminator line on skin)" },
                { "_AnimeEdgeWidth", "Shadow Edge Color Width" },
                { "_AnimeFlatLight", "Keep Painted Colors (don't dull them with lighting)" },
                { "_AnimeFlatNormal", "Remove Fine Bumps (weakens normal maps for flat fills)" },
                { "_AnimeFlatTexture", "Remove Fine Texture Detail (evens out dirt and unevenness into flat colors)" },
                { "_AnimeFlatMip", "Detail Removal Strength (blur level)" },
                { "_AnimeNoGloss", "Remove Realistic Gloss / Reflections" },
            };
            for (int i = 1; i <= 3; i++)
            {
                d[$"_Layer{i}Mask"] = $"Layer {i} Mask (R)";
                d[$"_Layer{i}Tex"] = $"Layer {i} Texture";
                d[$"_Layer{i}Color"] = $"Layer {i} Color (A = opacity)";
                d[$"_Layer{i}Blend"] = $"Layer {i} Blend";
                d[$"_Layer{i}Animate"] = $"Layer {i} Auto Scroll";
                d[$"_Layer{i}Scroll"] = $"Layer {i} Motion (XY = scroll/sec, Z = rotation deg, W = rotation deg/sec)";
                d[$"_Layer{i}Fade"] = $"Layer {i} Fade (0 = hidden, 1 = shown)";
                d[$"_Layer{i}Blink"] = $"Layer {i} Blink (times/sec, 0 = off)";
                d[$"_Layer{i}BlinkPhase"] = $"Layer {i} Blink Phase";
                d[$"_Layer{i}Unlit"] = $"Layer {i} Glow (ignores shadows and lighting)";
            }
            return d;
        }

        // プリセット: 日本語の名前(内部名) → 英語の名前・説明
        private static readonly Dictionary<string, (string name, string tooltip)> PresetEn = new Dictionary<string, (string, string)>
        {
            // キャラ
            { "肌", ("Skin", "Soft single shadow (reddish), weak rim, thin outline.") },
            { "顔", ("Face", "Same colors as skin, removes small shadows on the face (Flatten Face Shading 0.8). Add GalaxyToonFace to the character to follow the head direction.") },
            { "髪", ("Hair", "Crisp two-step shadow, highlight (angel-ring-like gloss), thin rim, outline. To make bangs see-through, add it to the character from the menu Tools/GalaxyToon/前髪を透過する (see-through bangs).") },
            { "服", ("Clothes", "Cloth-like two-step shadow, thin rim, outline. No gloss.") },
            { "金属", ("Metal", "Strong highlight and rim, dark shadow. Put a metal sphere texture in MatCap to look even more metallic.") },
            { "顔パーツ", ("Face Parts", "Eyes, teeth, tongue, eyelashes, etc. Almost no shadow and no outline.") },
            // 背景(「金属」はキャラ側と同じ名前。説明は DuplicateNameTooltipEn)
            { "塗装金属", ("Painted Metal", "Painted metal (doors, lockers, machine panels). Metallic 0, smoothness 0.5 coating gloss.") },
            { "コンクリート", ("Concrete", "Concrete, stone, floors. No gloss, soft shadow. Turn on Triplanar if there are no UVs.") },
            { "プラスチック", ("Plastic", "Small plastic props (not rubber). Metallic 0, smoothness 0.65, white highlight.") },
            { "発光パネル", ("Light Panel", "Lights, monitors, signs. Lighter shadow and an emission color (adjust it later). Lights up the surroundings when lightmaps are baked.") },
            { "ガラス", ("Glass", "Windows and lenses. Use with Render Mode set to Transparent. Strong reflection at smoothness 0.95.") },
            { "紙", ("Paper", "Paper, cardboard, upholstery. No gloss or reflection, soft and light shadow (as if light passes through a little).") },
            { "透過プラスチック", ("Clear Plastic", "Headlight covers, colored hazard lights, navigation lights. Sets Render Mode to Transparent with strong reflection and rim light. Color and opacity via Main Color (A), lit state via Emission Color.") },
            { "ダイヤモンド", ("Diamond", "Gems and crystals. Mirror-like strong reflection, sharp highlight, strong rim light. Almost no shadow.") },
            { "マグマ", ("Magma", "Lava and molten metal. Red-orange emission slowly pulses, shadows are black rock. The emission shape comes from the bright parts of the texture (emission mask).") },
            { "建物の壁", ("Building Wall", "Matte paint for exterior and interior walls. Smoothness 0.2, soft shadow.") },
            // 肌・服の質感
            { "肌 汗", ("Skin Sweat", "Sweat droplets on skin (sweat 0.7). Add GalaxyToonEffects to change the amount during gameplay.") },
            { "肌 濡れ", ("Skin Wet", "The whole skin gets wet, darker and shiny (wet 1).") },
            { "肌 液体(白)", ("Skin Liquid (White)", "White liquid flows down in streaks (liquid 0.6). Change the color with Liquid Color.") },
            { "涙・しずく", ("Tears / Drops", "Clear water for tears, water drops, raindrops (for GalaxyTears and string materials). Transparent, double-sided, sharp highlight and rim light, receives no shadows.") },
            { "宝石(屈折)", ("Gem (Refraction)", "Gems, crystals, thick glass. Transparent; the background is distorted with rainbow dispersion and small facets sparkle.") },
            { "服 ソフト", ("Clothes Soft", "Soft fabric (cotton, knit). Blurry shadow, no gloss.") },
            { "服 ラテックス", ("Clothes Latex", "Latex and enamel. Crisp shadow, small sharp highlight, strong edge shine and reflection.") },
            { "服 皮製品", ("Clothes Leather", "Leather (jackets, boots, belts). Slightly wide highlight, weak reflection, solid shadow.") },
            { "服 濡れ", ("Clothes Wet", "Clothes wet from rain or water. The color sinks deep, the fabric loses volume and clings to the body (shrink 1.5 cm). Gloss is subtle. Rain bounces off the surface with only a few drips. Driven by wet and rain on GalaxyToonEffects.") },
            { "髪 濡れ", ("Hair Wet", "Rain-soaked hair. Rain bounces off slightly and a few drops flow down. It has many gaps, so it darkens gradually like cloth (soak 0.5). Hair clumps into strands (wet hair) and loses volume (1 cm). Driven by wet on GalaxyToonEffects.") },
            { "肌 雨", ("Skin Rain", "Skin in the rain. Skin oil keeps water from soaking in, so drops bead up and run off quickly (soak 0, droplets and drips, light gloss). No ripples.") },
            { "革 濡れ", ("Leather Wet", "Rain-soaked leather (belts, boots, corsets). Absorbs a little water and deepens in color (soak 0.3), and gets surface gloss sooner than cloth. Does not shrink.") },
            { "木目", ("Wood Grain", "Varnished or waxed wood (floors, furniture, instruments). The grain comes from the texture. Anisotropic gloss stretched along the grain (UV horizontal) and weak reflection.") },
            { "木材", ("Raw Wood", "Unfinished wood (lumber, boards, logs, crates). No gloss or reflection, soft shadow.") },
            { "ペットボトル", ("PET Bottle", "Thin clear plastic (PET bottles, vinyl, clear containers). Transparent and double-sided, sharp highlight, white glowing edges, slight distortion behind. Content color via Main Color (A).") },
            { "レース", ("Lace", "Lace, mesh, openwork. Holes are cut out with the texture alpha (Cutout), double-sided, no gloss, light shadow that lets some light through.") },
            { "水", ("Water", "Water surfaces, puddles, containers of water. Transparent with distortion behind, mirror-like reflection and sharp highlight. Rain ripples slowly spread on upward faces (set Rain to 0 to stop them). Color via Main Color (A).") },
            // 追加
            { "汗", ("Sweat", "Adds sweat droplets (sweat 0.7, not flowing).") },
            { "液体", ("Liquid", "Adds white liquid streaks (liquid 0.6, drip speed unchanged).") },
            { "ウェット", ("Wet", "Makes everything wet (wet 1).") },
            { "流れる汗", ("Flowing Sweat", "Sweat droplets slowly flow down (sweat 0.7, flow speed 0.3).") },
            { "ネバネバ", ("Sticky", "Adds thick, sticky, translucent liquid like natto strings (viscosity 0.9, slow drip). Use GalaxyStickyStrings to pull strings.") },
            { "泡", ("Foam", "Adds white foam. It slowly slides down, bubbles merge, and they pop and shrink (foam 0.7).") },
            { "雨", ("Rain", "Getting rained on. Ripples spread on upward faces (ground, shoulders, roofs), drops run down vertical faces (walls, sides of the body), and everything gets wet. Raindrop and splash particles are in the Rain prefab.") },
            { "雨 染み込み", ("Rain Soak", "Concrete, stone, or fabric walls and ground absorb rain and darken in patches (soak 1, darken when wet 0.55). Little gloss.") },
            { "転送(消える)", ("Teleport (Vanish)", "Vanish in a sci-fi teleport or warp. A glowing line rises from the feet and erases what it passes. Animate Dissolve Amount 0 to 1 (automatic with GalaxyTeleport; outfit swaps also work).") },
            { "転送(現れる)", ("Teleport (Appear)", "Appear in a sci-fi teleport (also for the new outfit in an outfit swap). Animating Dissolve Amount 1 to 0 makes it appear with a glowing line from the feet.") },
            { "汚れ・すす", ("Dirt / Soot", "Soot after a fire, explosion, or accident. Stains stick to the body and remove gloss (dirt 0.5). Change it during gameplay with dirt on GalaxyToonEffects.") },
            { "泥汚れ", ("Mud", "Brown mud splashes (dirt 0.45, mud color).") },
            { "傷", ("Scratches", "Thin scratches. The lighter base color shows as lines with a slight dent (scratch 0.5).") },
            { "流れる液体", ("Flowing Liquid", "Liquid streaks drip downward (liquid 0.6, drip speed 0.5). Fade them out with Dry Speed on GalaxyToonEffects.") },
            // キャラの仕上げ
            { "アニメ塗り", ("Anime Shading", "Japanese anime style. Shadow colors are derived from the base color (more saturated, slightly blue-violet), crisp borders, a red terminator line on skin, colors not dulled by lighting, constant screen-space outline width. For skin, set Shadow Hue Shift to + for reddish shadows.") },
            { "髪の影を落とす", ("Cast Hair Shadow", "For hair materials. This hair casts bangs shadows on the face and eyes (face materials use Receive Hair Shadow).") },
            { "髪の影を受ける", ("Receive Hair Shadow", "For face and eye materials. Hair shadows fall according to the light direction (length 1.2 cm).") },
            { "瞳 くっきり", ("Eyes Crisp", "Adds depth and large/small highlights to the iris. Set the iris area automatically with the menu Tools/GalaxyToon/瞳の範囲を探す (find iris area).") },
            { "瞳 うるうる", ("Eyes Teary", "Teary eyes. Highlights tremble and water shimmer ripples below the iris. Use together with tears (GalaxyTears).") },
            { "瞳 きらめき", ("Eyes Sparkle", "Cross-shaped stars twinkle in the eyes (for admiration, emotion, determination).") },
            { "照れ顔", ("Embarrassed", "For the face (skin) material. Cheeks turn slightly red with diagonal blush lines. Cheek positions via the menu Tools/GalaxyToon/頬の位置を探す (find cheek positions). The amount can also be driven by blush on GalaxyToonEffects.") },
            { "ラメ", ("Glitter", "Tiny mirror flakes twinkle with the light and view direction (idol costumes, nails, decorations). Limit where it appears with the Glitter Mask.") },
            { "近づくと透ける", ("Fade When Close", "Dithers out when the camera is closer than 30 cm and disappears at 10 cm (so you don't see inside when a close camera clips into hair or body).") },
        };
    }
}
