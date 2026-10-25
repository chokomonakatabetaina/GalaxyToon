// GalaxyToon  Author: @matechagame
// GalaxyToon: トゥーンシェーダ(URP)。段影 + リム + マットキャップ + 輪郭線。
// 主光源は GalaxyToonLight の「キャラ専用ライト」(未設定ならシーンのメインライト)。追加ライトは強さを絞って足す。
Shader "GalaxyShader/Toon"
{
    Properties
    {
        // 見出し・並び・折りたたみは GalaxyToonGUI が決める(ここでは順番だけ)
        // ---- 基本(地の色・切り抜き)
        [MainTexture] _BaseMap("メインテクスチャ", 2D) = "white" {}
        [MainColor] _BaseColor("メインカラー", Color) = (1, 1, 1, 1)
        _Cutoff("切り抜きのしきい値", Range(0, 1)) = 0.5
        // ---- 描画設定(GalaxyToonGUI の「描画モード」「描画する面」が書き換える)
        [HideInInspector] _Cull("描画する面", Float) = 2
        [HideInInspector] _RenderMode("描画モード", Float) = 0
        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1
        [HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
        [HideInInspector] _BangFadeMode("前髪の毛先の透け (0=なし 1=不透明側 2=毛先)", Float) = 0
        [HideInInspector] _BangFadeStrength("前髪の毛先の透ける強さ", Range(0, 1)) = 0.6
        _QueueOffset("描画順のずらし (+1 で同じ面に重ねた文字・デカールを上に描く)", Range(-5, 5)) = 0
        [HideInInspector] _AlphaClip("アルファで切り抜く", Float) = 0
        // ---- 立体感(ノーマルマップ 1・2 枚目)・くぼみ(AO)
        [Normal][NoScaleOffset] _BumpMap("立体感 (ノーマルマップ)", 2D) = "bump" {}
        _BumpScale("立体感の強さ", Range(0, 2)) = 1
        [NoScaleOffset] _OcclusionMap("くぼみ (AO マップ)", 2D) = "white" {}
        _OcclusionStrength("くぼみの強さ", Range(0, 1)) = 1
        [Normal] _Bump2Map("立体感 2枚目 (細かい凹凸)", 2D) = "bump" {}
        _Bump2Scale("立体感 2枚目の強さ", Range(0, 2)) = 1
        // ---- 色調補正(_COLORADJUST)
        _HueShift("色相のずらし", Range(-0.5, 0.5)) = 0
        _Saturation("彩度", Range(0, 2)) = 1
        _Brightness("明るさ", Range(0, 2)) = 1
        _Gamma("ガンマ (1=そのまま)", Range(0.1, 3)) = 1
        // ---- 金属・つや・映り込み(_GLOSSY)
        _Metallic("金属 (0=金属でない 1=金属)", Range(0, 1)) = 0
        _Smoothness("つや (0=ざらざら 1=つるつる)", Range(0, 1)) = 0
        [NoScaleOffset] _MetallicGlossMap("金属・つやマップ (R=金属 A=つや)", 2D) = "white" {}
        _ReflectionStrength("映り込みの強さ", Range(0, 2)) = 1
        _ReflectionSteps("映り込みの段数 (0=なめらか)", Range(0, 8)) = 3
        // ---- トライプラナー(UV の無い背景用)
        [Toggle(_TRIPLANAR)] _UseTriplanar("トライプラナー (UV を使わずワールドで貼る)", Float) = 0
        _TriplanarScale("トライプラナーの細かさ (1m あたり)", Range(0.01, 10)) = 1
        _TriplanarBlend("トライプラナーの境目のくっきりさ", Range(1, 16)) = 4

        // ---- ライト(主光源の選び方・明るさの上限下限・環境光・追加ライト)
        [Enum(CharacterLight, 0, SceneMainLight, 1, BakedLightAndShadowOnly, 2)] _LightSource("主光源", Float) = 0
        _LightMinLimit("明るさの下限", Range(0, 1)) = 0.05
        _LightMaxLimit("明るさの上限", Range(0, 10)) = 1
        _MonochromeLighting("ライトの色を無視", Range(0, 1)) = 0
        _AmbientStrength("環境光の強さ", Range(0, 1)) = 0
        _AdditionalLightStrength("追加ライトの強さ", Range(0, 1)) = 0
        _BakedSteps("焼いた光の段数 (0=なめらか)", Range(0, 8)) = 0

        // ---- 影(影1〜3・濃さマスク・ランプ・落ち影)
        _ShadowColor("影1の色 (A=濃さ)", Color) = (0.82, 0.76, 0.85, 1)
        _ShadowBorder("影1の境界", Range(0, 1)) = 0.5
        _ShadowBlur("影1のぼかし", Range(0, 1)) = 0.1
        _Shadow2ndColor("影2の色 (A=濃さ)", Color) = (0.68, 0.6, 0.74, 0)
        _Shadow2ndBorder("影2の境界", Range(0, 1)) = 0.3
        _Shadow2ndBlur("影2のぼかし", Range(0, 1)) = 0.1
        _Shadow3rdColor("影3の色 (A=濃さ)", Color) = (0.5, 0.45, 0.6, 0)
        _Shadow3rdBorder("影3の境界", Range(0, 1)) = 0.15
        _Shadow3rdBlur("影3のぼかし", Range(0, 1)) = 0.1
        [NoScaleOffset] _ShadowStrengthMask("影の濃さマスク (R)", 2D) = "white" {}
        [NoScaleOffset] _ShadowRampTex("影のランプ (左=暗い 右=明るい)", 2D) = "white" {}
        _ShadowRampRows("ランプの段数 (縦に並べた部位ごとの段の数)", Range(1, 16)) = 1
        _ShadowRampRow("ランプの段 (部位IDマップがないとき。0=いちばん上)", Range(0, 15)) = 0
        [NoScaleOffset] _RampIdMap("部位IDマップ (R=使う段。0=いちばん上 … 1=いちばん下)", 2D) = "black" {}
        [HideInInspector] _RampIdOn("", Float) = 0
        _ShadowReceive("ほかの物の影を受ける", Range(0, 1)) = 0
        _AAStrength("境界のアンチエイリアス", Range(0, 1)) = 1

        // ---- 顔(陰影の平ら化・SDF 顔影・髪の影・瞳・頬染め)
        _FaceShading("顔の陰影を平らに", Range(0, 1)) = 0
        _FaceSphere("顔を球として塗る", Range(0, 1)) = 0
        [Toggle(_FACE_SDF)] _UseFaceSdf("SDF 顔影を使う", Float) = 0
        [NoScaleOffset] _FaceSdfTex("SDF 顔影 (R=光が右から G=左から)", 2D) = "white" {}
        [HideInInspector] _SdfFollowHead("SDF を首の向きに合わせる", Float) = 0
        [ToggleUI] _HairShadowCast("髪の影を落とす (この髪の形で顔・瞳に影を作る。髪のマテリアルで ON)", Float) = 0
        _HairShadowReceive("髪の影を受ける (顔・瞳のマテリアルで 1)", Range(0, 1)) = 0
        _HairShadowDistance("髪の影の長さ (cm。光の向きへずらす量)", Range(0, 5)) = 1.2
        _HairShadowBias("髪の影のすき間 (cm。肌に触れている髪で影が出すぎないように)", Range(0, 3)) = 0.3
        [Toggle(_EYE)] _UseEye("瞳の効果を使う (奥行き・ハイライト・うるうる・きらめき)", Float) = 0
        _EyeRect1("瞳1の範囲 (UV: XY=左下 ZW=右上。ツール「瞳の範囲を探す」で自動)", Vector) = (0, 0, 0, 0)
        _EyeRect2("瞳2の範囲 (UV: XY=左下 ZW=右上)", Vector) = (0, 0, 0, 0)
        _EyeParallax("瞳の奥行き (見る角度で瞳が奥に見える)", Range(0, 1)) = 0.4
        [HDR] _EyeHighlightColor("瞳のハイライトの色 (A=強さ)", Color) = (1, 1, 1, 0.9)
        _EyeHighlightPos("瞳のハイライトの位置 (XY=大 ZW=小。-1〜1、画面の右=+X 上=+Y)", Vector) = (-0.35, 0.4, 0.38, -0.32)
        _EyeHighlightSize("瞳のハイライトの大きさ", Range(0, 0.6)) = 0.2
        _EyeWet("うるうる (ハイライトが揺れ、下に水面の光)", Range(0, 1)) = 0
        [HDR] _EyeSparkleColor("きらめきの色 (A=強さ)", Color) = (1, 1, 1, 0)
        _EyeSparkleScale("きらめきの数", Range(1, 8)) = 3
        _EyeSparkleSpeed("きらめきの速さ", Range(0, 6)) = 1.5
        _BlushAmount("頬染め (照れ。GalaxyToonEffects の blush で動かせる)", Range(0, 1)) = 0
        _BlushColor("頬染めの色 (A=濃さ)", Color) = (1, 0.42, 0.48, 0.6)
        _BlushUV("頬の位置 (UV: XY=頬1 ZW=頬2。メニュー「頬の位置を探す」で自動)", Vector) = (0, 0, 0, 0)
        _BlushSize("頬染めの大きさ (UV)", Range(0.005, 0.3)) = 0.06
        _BlushSoft("頬染めのぼかし", Range(0, 1)) = 0.7
        _BlushLines("照れ線 (斜線を重ねる)", Range(0, 1)) = 0
        // ---- 髪の揺れ(値は GalaxyToonHairSway / GalaxyToonFace が毎フレーム入れる)
        _HairSway("髪の揺れ (キャラに GalaxyToonHairSway を付ける)", Range(0, 2)) = 0
        [HideInInspector] _HairSwayOffset("髪の揺れ: 遅れ", Vector) = (0, 0, 0, 0)
        [HideInInspector] _HairSwayPivot("髪の揺れ: 支点", Vector) = (0, 0, 0, 0)
        [HideInInspector] _HairSwayUp("髪の揺れ: 上", Vector) = (0, 1, 0, 0)
        [HideInInspector] _FaceForward("顔の正面", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FaceRight("顔の右", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FaceCenter("頭の中心", Vector) = (0, 0, 0, 0)

        // ---- ハイライト・マットキャップ 1・2・異方性(天使の輪)・濡れ髪
        [HDR] _SpecularColor("ハイライトの色 (A=強さ)", Color) = (1, 1, 1, 0)
        _SpecularSize("ハイライトの大きさ", Range(0, 1)) = 0.05
        _SpecularBlur("ハイライトのぼかし", Range(0, 1)) = 0.02
        [NoScaleOffset] _SpecularRampTex("ハイライトのランプ (左=光の外側 右=光の中心。光の形と色を決める。段は影のランプと同じ)", 2D) = "white" {}
        [HideInInspector] _SpecularRampOn("", Float) = 0
        [NoScaleOffset] _MatCapTex("マットキャップ", 2D) = "black" {}
        [HDR] _MatCapColor("マットキャップの色 (A=強さ)", Color) = (1, 1, 1, 1)
        _MatCapShadowMask("マットキャップを影の側では消す", Range(0, 1)) = 0
        [NoScaleOffset] _MatCap2Tex("マットキャップ 2枚目", 2D) = "black" {}
        [HDR] _MatCap2Color("マットキャップ 2枚目の色 (A=強さ)", Color) = (1, 1, 1, 1)
        [Enum(Add, 0, Multiply, 1)] _MatCap2Blend("マットキャップ 2枚目の合成", Float) = 0
        [NoScaleOffset] _MatCap2Mask("マットキャップ 2枚目のマスク (R)", 2D) = "white" {}
        _MatCap2ShadowMask("マットキャップ 2枚目を影の側では消す", Range(0, 1)) = 0
        [HDR] _AnisoColor("異方性ハイライトの色 (A=強さ)", Color) = (1, 1, 1, 0)
        [Enum(Tangent, 0, Bitangent, 1)] _AnisoDirection("異方性ハイライトの向き (髪の流れ)", Float) = 1
        _AnisoShift("異方性ハイライトの位置ずらし", Range(-1, 1)) = 0
        _AnisoSharpness("異方性ハイライトの細さ", Range(1, 200)) = 40
        _AnisoBorder("異方性ハイライトの境界", Range(0, 1)) = 0.5
        _AnisoBlur("異方性ハイライトのぼかし", Range(0, 1)) = 0.1
        _AnisoShadowMask("異方性ハイライトを影の側では消す", Range(0, 1)) = 0.5
        [ToggleUI] _AnisoUseRamp("天使の輪もハイライトのランプで形を決める", Float) = 0
        _HairWet("濡れ髪 (濡れたとき毛が束になり、束の間が暗く、天使の輪が細く鋭く。量は「濡れ」)", Range(0, 1)) = 0
        _HairWetClumps("濡れ髪 束の細かさ (UV 1 あたり)", Range(2, 200)) = 40

        // ---- リムライト・深度リム・逆光ライト
        [HDR] _RimColor("リムライトの色 (A=強さ)", Color) = (1, 1, 1, 0)
        _RimBorder("リムの境界", Range(0, 1)) = 0.5
        _RimBlur("リムのぼかし", Range(0, 1)) = 0.65
        _RimFresnelPower("リムの細さ", Range(0.01, 50)) = 3.5
        _RimShadowMask("リムを影の側では消す", Range(0, 1)) = 0.5
        [Toggle(_DEPTH_RIM)] _DepthRim("深度リム (シルエットだけ光らせる)", Float) = 0
        _RimDepthWidth("深度リムの幅 (cm)", Range(0, 5)) = 1
        _RimDepthThreshold("深度リムの奥行きしきい値 (m)", Range(0.001, 1)) = 0.1
        [HDR] _BacklightColor("逆光ライトの色 (A=強さ)", Color) = (1, 0.95, 0.9, 0)
        _BacklightBorder("逆光ライトの境界", Range(0, 1)) = 0.35
        _BacklightBlur("逆光ライトのぼかし", Range(0, 1)) = 0.05
        _BacklightDirectivity("逆光ライトの指向性 (光が真後ろのときだけ)", Range(1, 20)) = 5
        _BacklightViewStrength("逆光ライトの視線の影響", Range(0, 1)) = 1
        _BacklightMainStrength("逆光ライトにテクスチャ色を混ぜる", Range(0, 1)) = 0

        // ---- ラメ・屈折
        [HDR] _GlitterColor("ラメの色 (A=強さ)", Color) = (1, 1, 1, 0)
        [NoScaleOffset] _GlitterMask("ラメの出る所 (R)", 2D) = "white" {}
        _GlitterScale("ラメの細かさ (UV 1 あたりの粒の数)", Range(10, 2000)) = 300
        _GlitterSize("ラメの粒の大きさ", Range(0.05, 0.5)) = 0.25
        _GlitterDensity("ラメの量", Range(0, 1)) = 0.6
        _GlitterSharpness("ラメの光る角度の鋭さ (大きいほど一瞬だけ光る)", Range(1, 64)) = 12
        _GlitterShadowMask("ラメを影の側では消す", Range(0, 1)) = 0.7
        _RefractionStrength("屈折 (半透明のとき、後ろがゆがんで見える)", Range(0, 1)) = 0
        _RefractionDispersion("屈折の虹色のにじみ", Range(0, 1)) = 0.25

        // ---- 発光(メイン + レイヤー2)
        _EmissionMap("発光マスク", 2D) = "white" {}
        [HDR] _EmissionColor("発光色", Color) = (0, 0, 0, 1)
        [ToggleUI] _EmissionAnimate("発光 自動スクロール", Float) = 0
        _EmissionScroll("発光 動き (XY=スクロール/秒 Z=回転° W=回転°/秒)", Vector) = (0, 0, 0, 0)
        _EmissionBlink("発光 明滅 (回/秒 0=しない)", Range(0, 10)) = 0
        _Emission2Map("発光レイヤー2 マスク", 2D) = "white" {}
        [HDR] _Emission2Color("発光レイヤー2 色", Color) = (0, 0, 0, 1)
        [ToggleUI] _Emission2Animate("発光レイヤー2 自動スクロール", Float) = 0
        _Emission2Scroll("発光レイヤー2 動き (XY=スクロール/秒 Z=回転° W=回転°/秒)", Vector) = (0, 0, 0, 0)
        _Emission2Blink("発光レイヤー2 明滅 (回/秒 0=しない)", Range(0, 10)) = 0

        // ---- レイヤーテクスチャ 1〜3(模様・光の帯)
        [NoScaleOffset] _Layer1Mask("レイヤー1 マスク (R)", 2D) = "white" {}
        _Layer1Tex("レイヤー1 テクスチャ", 2D) = "white" {}
        _Layer1Color("レイヤー1 色 (A=濃さ)", Color) = (1, 1, 1, 1)
        [Enum(Normal, 0, Add, 1, Screen, 2, Multiply, 3)] _Layer1Blend("レイヤー1 合成", Float) = 0
        [ToggleUI] _Layer1Animate("レイヤー1 自動スクロール", Float) = 0
        _Layer1Scroll("レイヤー1 動き (XY=スクロール/秒 Z=回転° W=回転°/秒)", Vector) = (0, 0, 0, 0)
        _Layer1Fade("レイヤー1 フェード (0=消える 1=出る)", Range(0, 1)) = 1
        _Layer1Blink("レイヤー1 明滅 (回/秒 0=しない)", Range(0, 10)) = 0
        _Layer1BlinkPhase("レイヤー1 明滅のずれ", Range(0, 1)) = 0
        _Layer1Unlit("レイヤー1 光る (影を受けない)", Range(0, 1)) = 0
        [NoScaleOffset] _Layer2Mask("レイヤー2 マスク (R)", 2D) = "white" {}
        _Layer2Tex("レイヤー2 テクスチャ", 2D) = "white" {}
        _Layer2Color("レイヤー2 色 (A=濃さ)", Color) = (1, 1, 1, 1)
        [Enum(Normal, 0, Add, 1, Screen, 2, Multiply, 3)] _Layer2Blend("レイヤー2 合成", Float) = 0
        [ToggleUI] _Layer2Animate("レイヤー2 自動スクロール", Float) = 0
        _Layer2Scroll("レイヤー2 動き (XY=スクロール/秒 Z=回転° W=回転°/秒)", Vector) = (0, 0, 0, 0)
        _Layer2Fade("レイヤー2 フェード (0=消える 1=出る)", Range(0, 1)) = 1
        _Layer2Blink("レイヤー2 明滅 (回/秒 0=しない)", Range(0, 10)) = 0
        _Layer2BlinkPhase("レイヤー2 明滅のずれ", Range(0, 1)) = 0
        _Layer2Unlit("レイヤー2 光る (影を受けない)", Range(0, 1)) = 0
        [NoScaleOffset] _Layer3Mask("レイヤー3 マスク (R)", 2D) = "white" {}
        _Layer3Tex("レイヤー3 テクスチャ", 2D) = "white" {}
        _Layer3Color("レイヤー3 色 (A=濃さ)", Color) = (1, 1, 1, 1)
        [Enum(Normal, 0, Add, 1, Screen, 2, Multiply, 3)] _Layer3Blend("レイヤー3 合成", Float) = 0
        [ToggleUI] _Layer3Animate("レイヤー3 自動スクロール", Float) = 0
        _Layer3Scroll("レイヤー3 動き (XY=スクロール/秒 Z=回転° W=回転°/秒)", Vector) = (0, 0, 0, 0)
        _Layer3Fade("レイヤー3 フェード (0=消える 1=出る)", Range(0, 1)) = 1
        _Layer3Blink("レイヤー3 明滅 (回/秒 0=しない)", Range(0, 10)) = 0
        _Layer3BlinkPhase("レイヤー3 明滅のずれ", Range(0, 1)) = 0
        _Layer3Unlit("レイヤー3 光る (影を受けない)", Range(0, 1)) = 0

        // ---- 表面効果(濡れ・汗・液体・泡・雨・汚れ・傷。量は GalaxyToonEffects から動かせる)
        [Toggle(_GALAXY_FX)] _UseFx("濡れ・汗・液体を使う", Float) = 0
        [NoScaleOffset] _FxMask("出る場所 (R=濡れ G=汗 B=液体 A=泡)", 2D) = "white" {}
        _WetAmount("濡れ", Range(0, 1)) = 0
        _WetReceive("濡れを受ける強さ", Range(0, 1)) = 1
        _WetDarken("濡れると濃く (テクスチャ自身の色で乗算: 0.5=二乗 1=三乗。布・革は水を吸って色が深くなる)", Range(0, 1)) = 0.3
        _WetGloss("濡れのつや", Range(0, 2)) = 0.6
        _WetSoak("染み込み (コンクリート・石・布: 濡れた所が水を吸ってまだらに濃くなる)", Range(0, 1)) = 0
        _WetShrink("濡れて縮む (布・毛: 張りが無くなりふくらみが減る。濡れ 1 で法線の内側へ寄せる量 cm。ふわふわのしっぽは 4〜6)", Range(0, 8)) = 0
        _SweatAmount("汗", Range(0, 1)) = 0
        _SweatReceive("汗を受ける強さ (肌=1 服=0)", Range(0, 1)) = 1
        _SweatScale("汗の粒の細かさ", Range(5, 200)) = 35
        _SweatSize("汗の粒の大きさ", Range(0.05, 0.5)) = 0.3
        _SweatFlow("汗が流れる速さ", Range(0, 1)) = 0
        _LiquidAmount("液体", Range(0, 1)) = 0
        _LiquidReceive("液体を受ける強さ", Range(0, 1)) = 1
        _LiquidColor("液体の色 (A=不透明さ)", Color) = (1, 1, 1, 0.45)
        _LiquidScale("液体の筋の細かさ", Range(1, 100)) = 25
        _LiquidFlow("液体が垂れる速さ", Range(0, 2)) = 0.1
        _LiquidGloss("液体のつや", Range(0, 2)) = 1
        _LiquidViscosity("液体の粘り (0=さらさら 1=ネバネバ)", Range(0, 1)) = 0
        _LiquidFlowDir("液体が流れる向き (物体空間 XYZ。既定は下。口の中なら舌先へ)", Vector) = (0, -1, 0, 0)
        _FoamAmount("泡", Range(0, 1)) = 0
        _FoamReceive("泡を受ける強さ", Range(0, 1)) = 1
        _FoamColor("泡の色 (A=濃さ)", Color) = (1, 1, 1, 0.9)
        _FoamScale("泡の細かさ (UV 1 あたりの大きい泡の数)", Range(3, 200)) = 18
        _FoamSize("泡の大きさ (マスに対する半径)", Range(0.05, 0.6)) = 0.5
        _FoamFlow("泡が滑り落ちる速さ", Range(0, 2)) = 0.15
        _FoamPop("泡が割れる速さ (回/秒)", Range(0, 1)) = 0.12
        _FoamMerge("泡どうしのつながり", Range(0, 1)) = 0.6
        _RainAmount("雨", Range(0, 1)) = 0
        _RainReceive("雨を受ける強さ (屋根の下=0)", Range(0, 1)) = 1
        _RainRipples("雨の波紋 (地面・水たまり=1、服・髪・肌=0)", Range(0, 1)) = 1
        _RainFlowAmount("雨のしずくが流れる量 (服・髪は少量)", Range(0, 1)) = 1
        _RainSplash("表面で雨が跳ねる強さ (GalaxyToonEffects)", Range(0, 1)) = 0
        _RainScale("雨の波紋・しずくの細かさ (1m あたり)", Range(1, 60)) = 8
        _RainSpeed("雨の速さ (波紋の広がり・しずくの流れ)", Range(0.1, 3)) = 1
        _DirtAmount("汚れ・すす", Range(0, 1)) = 0
        _DirtReceive("汚れ・傷を受ける強さ", Range(0, 1)) = 1
        _DirtColor("汚れの色 (A=濃さ)", Color) = (0.12, 0.1, 0.09, 0.85)
        _DirtScale("汚れの細かさ (1m あたり)", Range(1, 100)) = 12
        _ScratchAmount("傷", Range(0, 1)) = 0
        _ScratchColor("傷の色 (A=濃さ)", Color) = (0.85, 0.82, 0.78, 0.8)
        _ScratchScale("傷の細かさ (UV 1 あたり)", Range(1, 100)) = 12
        _FxBump("粒・筋の盛り上がり", Range(0, 1)) = 0.3

        // ---- ディゾルブ(消える・現れる演出)
        [Toggle(_DISSOLVE)] _UseDissolve("ディゾルブを使う", Float) = 0
        _DissolveAmount("ディゾルブの量 (0=そのまま 1=消える)", Range(0, 1)) = 0
        [Enum(Noise, 0, Position, 1)] _DissolveMode("ディゾルブの方式 (ノイズ / 位置)", Float) = 0
        _DissolveDirection("位置: 消えていく向き (XYZ=ワールド W=範囲 m)", Vector) = (0, 1, 0, 2)
        _DissolveStart("位置: 開始位置 (原点からの距離 m。上から消すなら向き 0,-1,0・開始 -身長)", Range(-5, 5)) = 0
        [NoScaleOffset] _DissolveNoise("ノイズテクスチャ (R。無ければシェーダ内のノイズ)", 2D) = "white" {}
        _DissolveNoiseScale("ノイズの細かさ", Range(0.1, 20)) = 1
        _DissolveNoiseStrength("ノイズの強さ (2 以上でテクスチャを使う)", Range(0, 3)) = 0.5
        [ToggleUI] _DissolveInvert("逆にする (消える代わりに現れる。服の入れ替えの新しい服)", Float) = 0
        [ToggleUI] _DissolveWorld("位置: ワールドの高さで測る (体の全部品で同じ線。テレポート演出用)", Float) = 0
        _DissolveEdgeWidth("境目の幅", Range(0, 0.5)) = 0.05
        [HDR] _DissolveEdgeColor("境目の色 (A=強さ)", Color) = (2, 1, 0.3, 1)

        // ---- ステンシル(GUI の「書き込む」「透かす」ボタンが設定)
        [HideInInspector] _StencilRef("ステンシルの値", Range(0, 255)) = 0
        [HideInInspector] _StencilReadMask("ステンシル 読みマスク", Range(0, 255)) = 255
        [HideInInspector] _StencilWriteMask("ステンシル 書きマスク", Range(0, 255)) = 255
        [HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("ステンシル 比較", Float) = 8
        [HideInInspector][Enum(UnityEngine.Rendering.StencilOp)] _StencilPass("ステンシル 通ったとき", Float) = 0
        [HideInInspector][Enum(UnityEngine.Rendering.StencilOp)] _StencilFail("ステンシル 通らなかったとき", Float) = 0
        [HideInInspector][Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail("ステンシル 奥にあるとき", Float) = 0

        // ---- カメラが近いと透ける(_NEAR_FADE)
        [ToggleUI] _UseNearFade("カメラが近いと透ける (めり込み防止)", Float) = 0
        _NearFadeStart("透け: これより近いと消える (m)", Range(0, 2)) = 0.1
        _NearFadeEnd("透け: これより遠いと消えない (m)", Range(0, 3)) = 0.3

        // ---- 輪郭線(Outline パス)
        _OutlineWidth("輪郭線の太さ (cm)", Range(0, 1)) = 0.08
        [ToggleUI] _OutlineSmoothNormal("輪郭線になめらか法線を使う (ツールで焼いたメッシュのとき。角で線が割れない)", Float) = 1
        _OutlineColor("輪郭線の色", Color) = (0.25, 0.2, 0.25, 1)
        _OutlineTexBlend("輪郭線にテクスチャ色を混ぜる", Range(0, 1)) = 0.5
        _OutlineLit("輪郭線をライトの明るさに合わせる", Range(0, 1)) = 1
        _OutlineFixWidth("近いと細くする", Range(0, 1)) = 0.5
        _AnimeOutlinePixel("輪郭線を画面上で同じ太さに (アニメの線。1=遠くても細くならない)", Range(0, 1)) = 0

        // アニメ塗り (日本のアニメ調。OFF のときは今までと同じ)
        [Toggle(_ANIME)] _AnimeMode("アニメ塗り", Float) = 0
        _AnimeHardness("影の境界をくっきり (セル塗り)", Range(0, 1)) = 0.85
        _AnimeShadowHue("影色の色相ずらし (- 青紫へ / + 赤みへ)", Range(-0.2, 0.2)) = -0.03
        _AnimeShadowSat("影色の鮮やかさ (地の色から自動で影色を作る)", Range(0, 1)) = 0.35
        _AnimeShadowValue("影色の明るさ", Range(0.3, 1)) = 0.72
        _AnimeEdgeColor("影の境界の色 (A=強さ。肌の赤い境界線など)", Color) = (1, 0.5, 0.42, 0.5)
        _AnimeEdgeWidth("影の境界の色の幅", Range(0, 0.2)) = 0.035
        _AnimeFlatLight("ライトで色をくすませない (塗った色を保つ)", Range(0, 1)) = 0.8
        _AnimeFlatNormal("細かい凹凸を消す (ノーマルマップを弱めて面をベタ塗りに)", Range(0, 1)) = 0.8
        _AnimeFlatTexture("テクスチャの細かい模様を消す (汚れ・ムラを均して色面に)", Range(0, 1)) = 0
        _AnimeFlatMip("模様を消す強さ (ぼかしの段)", Range(1, 8)) = 4
        _AnimeNoGloss("写実的なつや・映り込みを消す", Range(0, 1)) = 0.85
        [NoScaleOffset] _OutlineWidthMask("輪郭線の太さマスク (R)", 2D) = "white" {}

        // 「オフ」中のテクスチャの退避先(GalaxyToonGUI が使う。シェーダでは読まない)
        [HideInInspector] _BaseMap_Stash("", 2D) = "white" {}
        [HideInInspector] _BumpMap_Stash("", 2D) = "white" {}
        [HideInInspector] _OcclusionMap_Stash("", 2D) = "white" {}
        [HideInInspector] _Bump2Map_Stash("", 2D) = "white" {}
        [HideInInspector] _MetallicGlossMap_Stash("", 2D) = "white" {}
        [HideInInspector] _ShadowStrengthMask_Stash("", 2D) = "white" {}
        [HideInInspector] _ShadowRampTex_Stash("", 2D) = "white" {}
        [HideInInspector] _RampIdMap_Stash("", 2D) = "white" {}
        [HideInInspector] _SpecularRampTex_Stash("", 2D) = "white" {}
        [HideInInspector] _FaceSdfTex_Stash("", 2D) = "white" {}
        [HideInInspector] _MatCapTex_Stash("", 2D) = "white" {}
        [HideInInspector] _MatCap2Tex_Stash("", 2D) = "white" {}
        [HideInInspector] _MatCap2Mask_Stash("", 2D) = "white" {}
        [HideInInspector] _EmissionMap_Stash("", 2D) = "white" {}
        [HideInInspector] _Emission2Map_Stash("", 2D) = "white" {}
        [HideInInspector] _Layer1Mask_Stash("", 2D) = "white" {}
        [HideInInspector] _Layer1Tex_Stash("", 2D) = "white" {}
        [HideInInspector] _Layer2Mask_Stash("", 2D) = "white" {}
        [HideInInspector] _Layer2Tex_Stash("", 2D) = "white" {}
        [HideInInspector] _Layer3Mask_Stash("", 2D) = "white" {}
        [HideInInspector] _Layer3Tex_Stash("", 2D) = "white" {}
        [HideInInspector] _FxMask_Stash("", 2D) = "white" {}
        [HideInInspector] _DissolveNoise_Stash("", 2D) = "white" {}
        [HideInInspector] _OutlineWidthMask_Stash("", 2D) = "white" {}
        [HideInInspector] _GlitterMask_Stash("", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" "UniversalMaterialType" = "Unlit" }

        // パスの一覧:
        //   ForwardLit   … 本体の色(GalaxyToonForward.hlsl)
        //   Outline      … 輪郭線(裏面を法線方向へ押し出す)
        //   ShadowCaster … ほかの物へ影を落とす
        //   DepthOnly    … 深度テクスチャ(深度リム・ポストエフェクト用)
        //   Meta         … ライトマップを焼くときの色と発光
        //   DepthNormals … 深度と法線(SSAO・髪の影用。A に「髪の影を落とす物」の印)
        // 頂点を動かす処理(髪の揺れ・濡れて縮む)とディゾルブ・切り抜きは全パスで同じにする(影や輪郭線が本体とずれないように)。

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_SrcBlend] [_DstBlend]
            Stencil { Ref [_StencilRef] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] Comp [_StencilComp] Pass [_StencilPass] Fail [_StencilFail] ZFail [_StencilZFail] }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GalaxyToonVert
            #pragma multi_compile_instancing // GPU インスタンシング(RenderMeshInstanced)用
            #pragma fragment GalaxyToonFrag

            // キーワードの種類:
            //   multi_compile         … URP がシーン・カメラの設定で切り替える(影・追加ライト・ライトマップ・フォグ等)。全組み合わせをビルドする
            //   shader_feature_local  … マテリアルごとの機能の ON/OFF。使っているマテリアルの組み合わせだけビルドする
            // 組み合わせが増えるとエディタの初回コンパイル(仮の水色表示)が長くなるので、multi_compile はむやみに足さない。
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS // 頂点ライト(_ADDITIONAL_LIGHTS_VERTEX)は使わない(組み合わせを減らす)
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            // 下はプロパティのトグル・テクスチャの有無で GalaxyToonGUI が ON/OFF する
            #pragma shader_feature_local_fragment _SHADOW_RAMP
            #pragma shader_feature_local_fragment _DEPTH_RIM
            #pragma shader_feature_local_fragment _FACE_SDF
            #pragma shader_feature_local_fragment _GALAXY_FX
            #pragma shader_feature_local_fragment _NORMALMAP
            #pragma shader_feature_local_fragment _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _TRIPLANAR
            #pragma shader_feature_local_fragment _EMISSION2
            // 下は GalaxyToonGUI が値を見て自動で ON/OFF する(使っていない機能は計算もビルドもしない)
            #pragma shader_feature_local_fragment _FACE
            #pragma shader_feature_local_fragment _SHADOW2ND
            #pragma shader_feature_local_fragment _GLOSSY
            #pragma shader_feature_local_fragment _ADDLIGHT
            #pragma shader_feature_local_fragment _SPECULAR
            #pragma shader_feature_local_fragment _RIMLIGHT
            #pragma shader_feature_local_fragment _MATCAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _COLORADJUST
            #pragma shader_feature_local_fragment _SHADOW3RD
            #pragma shader_feature_local_fragment _ANISOTROPY
            #pragma shader_feature_local_fragment _BACKLIGHT
            #pragma shader_feature_local_fragment _MATCAP2
            #pragma shader_feature_local_fragment _NORMALMAP2
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local_fragment _HAIR_SHADOW
            #pragma shader_feature_local_fragment _ANIME
            #pragma shader_feature_local_fragment _EYE
            #pragma shader_feature_local_fragment _GLITTER
            #pragma shader_feature_local_fragment _REFRACTION
            #pragma shader_feature_local_fragment _NEAR_FADE
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING      // 映り込み(_GLOSSY)用
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ LIGHTMAP_ON                               // 焼いた光(背景)
            #pragma shader_feature_local_fragment _LAYER1
            #pragma shader_feature_local_fragment _LAYER2
            #pragma shader_feature_local_fragment _LAYER3
            #pragma multi_compile_fog
            #include "GalaxyToonForward.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]
            Stencil { Ref [_StencilRef] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] Comp [_StencilComp] Pass [_StencilPass] Fail [_StencilFail] ZFail [_StencilZFail] }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GalaxyToonOutlineVert
            #pragma multi_compile_instancing // GPU インスタンシング(RenderMeshInstanced)用
            #pragma fragment GalaxyToonOutlineFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local_fragment _NEAR_FADE
            #pragma multi_compile_fog
            #include "GalaxyToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GalaxyToonShadowVert
            #pragma multi_compile_instancing // GPU インスタンシング(RenderMeshInstanced)用
            #pragma fragment GalaxyToonShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DISSOLVE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "GalaxyToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GalaxyToonDepthVert
            #pragma multi_compile_instancing // GPU インスタンシング(RenderMeshInstanced)用
            #pragma fragment GalaxyToonDepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local_fragment _NEAR_FADE
            #include "GalaxyToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            // ライトマップを焼くとき用(地の色と発光を渡す。発光した看板・ランプの光が周りに焼き込まれる)
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GalaxyToonMetaVert
            #pragma fragment GalaxyToonMetaFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "GalaxyToonPasses.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct MetaAttributes { float4 positionOS : POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct MetaVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            MetaVaryings GalaxyToonMetaVert(MetaAttributes input)
            {
                MetaVaryings output;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2, unity_LightmapST, unity_DynamicLightmapST);
                output.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
                return output;
            }

            half4 GalaxyToonMetaFrag(MetaVaryings input) : SV_Target
            {
                half4 albedo = GalaxyToonAlbedo(input.uv);
                GalaxyToonClip(albedo.a);
                UnityMetaInput meta = (UnityMetaInput)0;
                meta.Albedo = albedo.rgb;
                meta.Emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_linear_repeat, input.uv * _EmissionMap_ST.xy + _EmissionMap_ST.zw).rgb * _EmissionColor.rgb
                              + SAMPLE_TEXTURE2D(_Emission2Map, sampler_linear_repeat, input.uv * _Emission2Map_ST.xy + _Emission2Map_ST.zw).rgb * _Emission2Color.rgb;
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GalaxyToonDepthVert
            #pragma multi_compile_instancing // GPU インスタンシング(RenderMeshInstanced)用
            #pragma fragment GalaxyToonDepthNormalsFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local_fragment _NEAR_FADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "GalaxyToonPasses.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "GalaxyToon.Editor.GalaxyToonGUI"
}
