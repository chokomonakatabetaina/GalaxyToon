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
        // 切り抜きの縁(髪の毛先など)を、MSAA のサンプルの数だけ段にしてなめらかにする(MSAA がオフなら今までどおりの切り抜き)。描画モードが「切り抜き」のとき自動でオン
        [HideInInspector] _AlphaToMask("切り抜きの縁をなめらかに (アルファ・トゥ・カバレッジ)", Float) = 0
        // ---- 描画設定(GalaxyToonGUI の「描画モード」「描画する面」が書き換える)
        [HideInInspector] _Cull("描画する面", Float) = 2
        [HideInInspector] _RenderMode("描画モード", Float) = 0
        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1
        [HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
        [HideInInspector] _BangFadeMode("前髪の毛先の透け (0=なし 1=不透明側 2=毛先)", Float) = 0
        [HideInInspector] _BangFadeStrength("前髪の毛先の透ける強さ", Range(0, 1)) = 0.6
        _QueueOffset("描画順のずらし (+1 で同じ面に重ねた文字・デカールを上に描く)", Range(-9, 9)) = 0
        [HideInInspector] _AlphaClip("アルファで切り抜く", Float) = 0
        // ---- 立体感(ノーマルマップ 1・2 枚目)・くぼみ(AO)
        [Normal][NoScaleOffset] _BumpMap("立体感 (ノーマルマップ)", 2D) = "bump" {}
        _BumpScale("立体感の強さ", Range(0, 2)) = 1
        [NoScaleOffset] _OcclusionMap("くぼみ (AO マップ)", 2D) = "white" {}
        _OcclusionStrength("くぼみの強さ", Range(0, 1)) = 1
        [Toggle(_CREVICE_AO)] _CreviceAO("奥ほど暗く (画面の深度で、くぼみ・重なりの奥を影へ。ポーズ・カメラに合わせて変わる)", Float) = 0
        _CreviceStrength("奥ほど暗くの強さ", Range(0, 1)) = 0.5
        _CreviceRadius("奥ほど暗くの半径 (cm)", Range(0.2, 10)) = 2
        [Normal] _Bump2Map("立体感 2枚目 (細かい凹凸)", 2D) = "bump" {}
        _Bump2Scale("立体感 2枚目の強さ", Range(0, 2)) = 1
        // ---- 色調補正(_COLORADJUST)
        _HueShift("色相のずらし", Range(-0.5, 0.5)) = 0
        _Saturation("彩度", Range(0, 2)) = 1
        _Brightness("明るさ", Range(0, 2)) = 1
        _Gamma("ガンマ (1=そのまま)", Range(0.1, 3)) = 1
        // ---- 金属・つや・映り込み・クリアコート・ベルベット(_GLOSSY)
        _Metallic("金属 (0=金属でない 1=金属)", Range(0, 1)) = 0
        _Smoothness("つや (0=ざらざら 1=つるつる)", Range(0, 1)) = 0
        [NoScaleOffset] _MetallicGlossMap("金属・つやマップ (R=金属 A=つや)", 2D) = "white" {}
        _ReflectionStrength("映り込みの強さ", Range(0, 2)) = 1
        _ReflectionSteps("映り込みの段数 (0=なめらか)", Range(0, 8)) = 3
        _ClearCoat("クリアコート (二層塗装・エナメルの透明ニス層の強さ)", Range(0, 1)) = 0
        _ClearCoatSmoothness("クリアコートのつや (ニス層の鋭さ)", Range(0, 1)) = 0.9
        [HDR] _VelvetColor("ベルベット・布の毛羽立ち色 (A=強さ。輪郭が柔らかく白む起毛布)", Color) = (1, 1, 1, 0)
        _VelvetPower("ベルベットの広がり (小さいほど面全体が起毛、大きいほど輪郭だけ)", Range(1, 8)) = 3
        // ---- トライプラナー(UV の無い背景用)
        [Toggle(_TRIPLANAR)] _UseTriplanar("トライプラナー (UV を使わずワールドで貼る)", Float) = 0
        _TriplanarScale("トライプラナーの細かさ (1m あたり)", Range(0.01, 10)) = 1
        _TriplanarBlend("トライプラナーの境目のくっきりさ", Range(1, 16)) = 4

        // ---- ライト(主光源の選び方・明るさの上限下限・環境光・追加ライト)
        [Enum(CharacterLight, 0, SceneMainLight, 1, BakedLightAndShadowOnly, 2)] _LightSource("主光源", Float) = 0
        _LightFloor("明るさの下限", Range(0, 1)) = 0.05
        _LightCeiling("明るさの上限", Range(0, 10)) = 1
        _LightGray("ライトの色を無視", Range(0, 1)) = 0
        _AmbientStrength("環境光の強さ", Range(0, 1)) = 0
        _AdditionalLightStrength("追加ライトの強さ", Range(0, 1)) = 0
        _BakedSteps("焼いた光の段数 (0=なめらか)", Range(0, 8)) = 0

        // ---- 影(影1〜3・濃さマスク・ランプ・落ち影)
        _Shade1Tint("影1の色 (A=濃さ)", Color) = (0.82, 0.76, 0.85, 1)
        _Shade1Edge("影1の境界", Range(0, 1)) = 0.5
        _Shade1Soft("影1のぼかし", Range(0, 1)) = 0.1
        _Shade2Tint("影2の色 (A=濃さ)", Color) = (0.68, 0.6, 0.74, 0)
        _Shade2Edge("影2の境界", Range(0, 1)) = 0.3
        _Shade2Soft("影2のぼかし", Range(0, 1)) = 0.1
        _Shade3Tint("影3の色 (A=濃さ)", Color) = (0.5, 0.45, 0.6, 0)
        _Shade3Edge("影3の境界", Range(0, 1)) = 0.15
        _Shade3Soft("影3のぼかし", Range(0, 1)) = 0.1
        [NoScaleOffset] _ShadeAmountMap("影の濃さマスク (R)", 2D) = "white" {}
        [NoScaleOffset] _ShadeMap("影色テクスチャ (手で塗った影の色。無ければ地の色×影色)", 2D) = "white" {}
        [HideInInspector] _ShadeMapOn("影色テクスチャが入っている (GUI が入れる)", Float) = 0
        [NoScaleOffset] _ShadowBiasMask("影の出やすさマスク (R: 灰 0.5=そのまま 白=影が出にくい 黒=出やすい)", 2D) = "linearGrey" {}
        _ShadowBiasStrength("影の出やすさマスクの強さ", Range(0, 1)) = 0.5
        [NoScaleOffset] _ShadowRampTex("影のランプ (左=暗い 右=明るい)", 2D) = "white" {}
        _ShadowRampRows("ランプの段数 (縦に並べた部位ごとの段の数)", Range(1, 16)) = 1
        _ShadowRampRow("ランプの段 (部位IDマップがないとき。0=いちばん上)", Range(0, 15)) = 0
        [NoScaleOffset] _RampIdMap("部位IDマップ (R=使う段。0=いちばん上 … 1=いちばん下)", 2D) = "black" {}
        [HideInInspector] _RampIdOn("", Float) = 0
        _CastShadowAmount("ほかの物の影を受ける", Range(0, 1)) = 0
        _ShadowTerminatorFade("落ち影を明暗境界の近くで弱める (低ポリの面の形の帯を防ぐ。0=しない)", Range(0, 1)) = 1
        _EdgeSmooth("境界のアンチエイリアス", Range(0, 1)) = 1

        // ---- 顔(陰影の平ら化・SDF 顔影・髪の影・瞳・頬染め)
        _FaceShading("顔の陰影を平らに", Range(0, 1)) = 0
        _FaceSphere("顔を球として塗る", Range(0, 1)) = 0
        [Toggle(_FACE_SDF)] _UseFaceSdf("SDF 顔影を使う", Float) = 0
        [NoScaleOffset] _FaceSdfTex("SDF 顔影 (R=光が右から G=左から)", 2D) = "white" {}
        _FaceSdfPitch("SDF 上下の光の効き (太陽が高い・低いときの影の変化)", Range(0, 1)) = 0.35
        // カメラからの補助光: 影の側の、カメラを向いた面だけを少し持ち上げる(逆光でも顔が沈まない。全体を明るくする環境光とちがい、明暗の形は残る)
        // 影の模様: 影の所に、モデルに貼りついた斜線を入れる(画面に貼りつく漫画の線とちがい、動いても模様が体についてくる)
        _ShadowPattern("影の模様の強さ", Range(0, 1)) = 0
        _ShadowPatternColor("影の模様の色", Color) = (0.55, 0.45, 0.6, 1)
        _ShadowPatternDensity("影の模様の細かさ (1m あたりの本数)", Range(20, 600)) = 160
        _ShadowPatternWidth("影の模様の線の太さ", Range(0.05, 0.9)) = 0.35
        _FillLightColor("カメラからの補助光 (A=強さ)", Color) = (1, 0.96, 0.94, 0)
        _FaceSdfIgnoreShadow("SDF 顔影では落ち影を使わない (正面から見るほど。横顔では戻す。鼻などの影が描いた顔影に重ならない)", Range(0, 1)) = 1
        [HideInInspector] _SdfFollowHead("SDF を首の向きに合わせる", Float) = 0
        [ToggleUI] _HairShadowCast("髪の影を落とす (この髪の形で顔・瞳に影を作る。髪のマテリアルで ON)", Float) = 0
        _HairShadowReceive("髪の影を受ける (顔・瞳のマテリアルで 1)", Range(0, 1)) = 0
        _HairShadowDistance("髪の影の長さ (cm。光の向きへずらす量)", Range(0, 5)) = 1.2
        _HairShadowBias("髪の影のすき間 (cm。肌に触れている髪で影が出すぎないように)", Range(0, 3)) = 0.3
        _HairShadowSoftness("髪の影のぼかし (px。境界のギザギザをなめらかに)", Range(0, 4)) = 1.5
        // ---- SSS(表面下散乱・肌の血色と逆光透け)
        [Toggle(_SSS)] _UseSss("SSS (肌・薄肉の血色と透け) を使う", Float) = 0
        _SssColor("SSS 血色カラー (A=強さ。影の境目と逆光で透ける赤み)", Color) = (1, 0.32, 0.25, 0.65)
        _SssEdgeWidth("SSS 影の境目の血色幅 (ターミネーターのにじみ)", Range(0, 0.5)) = 0.12
        _SssShadowBleed("SSS 影の中の血色残り", Range(0, 1)) = 0.15
        _SssTranslucency("SSS 逆光・斜光の透け強さ (耳・鼻・指先が光を通す)", Range(0, 2)) = 0.8
        _SssDistortion("SSS 透け光の法線ゆがみ", Range(0, 1)) = 0.4
        _SssPower("SSS 透け光の鋭さ", Range(1, 16)) = 3.5
        _SssFrontScatter("SSS 正面・斜めの光でも透かす (輪郭と明暗境界。0=逆光のときだけ。厚みマップで肌の薄い所に限ってから上げる)", Range(0, 1)) = 0
        [NoScaleOffset] _ThicknessMap("厚み・SSSマスク (R: 白=薄く透ける肌 黒=透けない。ツール「厚みマップを作る」で自動)", 2D) = "white" {}
        // ---- シアー・ストッキング(薄布の肌透け: 正面は肌が透け、輪郭ほど布の色が濃くなる)
        [Toggle(_SHEER)] _UseSheer("ストッキング・薄布の透け (シアー素材) を使う", Float) = 0
        _SheerAmount("ストッキングの透け強さ (正面で素肌が透ける量)", Range(0, 1)) = 0.65
        _SheerSkinColor("透けて見える素肌の色", Color) = (1, 0.86, 0.82, 1)
        _SheerPower("輪郭の濃さ (デニール感。小さいほど縁まで肌が透け、大きいほど縁が濃く締まる)", Range(0.5, 8)) = 2.2
        _SheerSheen("繊維の微細つや (ストッキング特有の光沢)", Range(0, 1)) = 0.25
        [NoScaleOffset] _SheerMask("ストッキングの範囲マスク (R: 白=透ける所)", 2D) = "white" {}
        // 網タイツの食い込み: 地のテクスチャの網の線で肌が押され、網目の間がふくらんで見える(法線と色)
        [Toggle(_NET_DENT)] _UseNetDent("網タイツの食い込み (網の線で肌が押され、網目の間がふくらむ)", Float) = 0
        [Enum(DarkLines, 0, Alpha, 1)] _NetSource("網の見分け方 (0=地のテクスチャの暗い線 1=アルファ)", Float) = 0
        _NetThreshold("網の線のしきい値 (これより暗い所を網とみなす)", Range(0, 1)) = 0.35
        [NoScaleOffset] _NetDentMask("食い込みを出す範囲 (R。白い所だけ。無ければ全体)", 2D) = "white" {}
        _NetDentWidth("食い込みの幅 (テクセル)", Range(0.5, 8)) = 2
        _NetDentStrength("食い込みの深さ (法線の傾き)", Range(0, 2)) = 0.6
        _NetDentShadow("網の際の影 (押されてできる陰)", Range(0, 1)) = 0.35
        _NetDentColor("網の際の影の色 (地の色に掛ける)", Color) = (0.72, 0.5, 0.5, 1)
        _NetDentHighlight("網目のふくらみの明るさ", Range(0, 1)) = 0.12
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
        // まぶたの影: 瞳の上の縁に、まぶたが落とす柔らかい暗い帯(目が貼りついて見えない)
        _EyeLidShadowColor("まぶたの影の色 (A=強さ)", Color) = (0.28, 0.2, 0.32, 0.45)
        _EyeIrisRadius("瞳の大きさ (瞳の UV の範囲の半分を 1。まぶたの影・光の溜まりの位置)", Range(0.1, 1)) = 0.5
        _EyeLidShadowWidth("まぶたの影の幅 (瞳の大きさの割合)", Range(0, 1)) = 0.38
        _EyeLidShadowSoft("まぶたの影のぼかし", Range(0.01, 1)) = 0.45
        // 瞳の光の溜まり: 光と反対の側の瞳の下が明るくなる(レンズのように光が集まる)
        [HDR] _EyeCausticColor("瞳の光の溜まりの色 (A=強さ)", Color) = (1, 0.95, 0.85, 0.35)
        _EyeCausticSize("瞳の光の溜まりの大きさ", Range(0.1, 1)) = 0.55
        _BlushAmount("頬染め (照れ。GalaxyToonEffects の blush で動かせる)", Range(0, 1)) = 0
        _BlushColor("頬染めの色 (A=濃さ)", Color) = (1, 0.42, 0.48, 0.6)
        _BlushUV("頬の位置 (UV: XY=頬1 ZW=頬2。メニュー「頬の位置を探す」で自動)", Vector) = (0, 0, 0, 0)
        _BlushSize("頬染めの大きさ (UV)", Range(0.005, 0.3)) = 0.06
        _BlushSoft("頬染めのぼかし", Range(0, 1)) = 0.7
        _BlushLines("照れ線 (斜線を重ねる)", Range(0, 1)) = 0
        // ---- 毛先のグラデーション(髪の先へ別の色。毛先を白っぽく抜くなど)
        _TipColor("毛先の色 (A=強さ)", Color) = (1, 1, 1, 0)
        _TexLineKeep("テクスチャの線を遠くでも残す", Range(0, 1)) = 0
        [HideInInspector] _ObjectId("物の番号（画面の物ごとの線）", Float) = 0
        [Enum(UVV_RootTop, 0, UVV_RootBottom, 1, ObjectHeight, 2)] _TipAxis("毛先の向き (0=UV の V・上が根元 1=UV の V・下が根元 2=物体の高さ)", Float) = 0
        _TipStart("毛先のグラデーションの始まり", Range(-2, 2)) = 0.4
        _TipEnd("毛先のグラデーションの終わり (毛先)", Range(-2, 2)) = 0
        // ---- 髪の揺れ(値は GalaxyToonHairSway / GalaxyToonFace が毎フレーム入れる)
        _HairSway("髪の揺れ (キャラに GalaxyToonHairSway を付ける)", Range(0, 2)) = 0
        [HideInInspector] _HairSwayOffset("髪の揺れ: 遅れ", Vector) = (0, 0, 0, 0)
        [HideInInspector] _HairSwayOffsetPrev("髪の揺れ: 前のフレームの遅れ", Vector) = (0, 0, 0, 0)
        [HideInInspector] _HairSwayPivotPrev("髪の揺れ: 前のフレームの支点", Vector) = (0, 0, 0, 0)
        [HideInInspector] _HairSwayPivot("髪の揺れ: 支点", Vector) = (0, 0, 0, 0)
        [HideInInspector] _HairSwayUp("髪の揺れ: 上", Vector) = (0, 1, 0, 0)
        // 平面化(2D アニメ風の平らな見え方。使う側(GalaxyToon Studio など)が Renderer ごと・カメラごとに MaterialPropertyBlock で入れる)
        [HideInInspector] _Flatten0("平面化 範囲1 (xyz=中心 w=半径 m)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Flatten1("平面化 範囲2", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Flatten2("平面化 範囲3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FlattenAmount("平面化の強さ (xyz=範囲1〜3)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FlattenCamera("平面化のカメラ (xyz=位置か向き w: 0=なし 1=透視 2=平行)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FaceForward("顔の正面", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FaceRight("顔の右", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FaceUp("顔の上", Vector) = (0, 1, 0, 0)
        [HideInInspector] _FaceCenter("頭の中心", Vector) = (0, 0, 0, 0)
        [HideInInspector] _BangCenter("前髪の基準点", Vector) = (0, 0, 0, 0)
        [HideInInspector] _BangUp("前髪の上", Vector) = (0, 1, 0, 0)
        [HideInInspector] _BangForward("前髪の正面", Vector) = (0, 0, 1, 0)
        [HideInInspector] _BangRange("前髪の範囲", Vector) = (0, 0, 0, 0)
        [HideInInspector] _BangRegion("前髪の生え際", Vector) = (0, 0, 0, 0)

        // ---- ハイライト・マットキャップ 1・2・異方性(天使の輪)・濡れ髪
        [HDR] _SpecularColor("ハイライトの色 (A=強さ)", Color) = (1, 1, 1, 0)
        _SpecularSize("ハイライトの大きさ", Range(0, 1)) = 0.05
        _SpecularSoft("ハイライトのぼかし", Range(0, 1)) = 0.02
        [NoScaleOffset] _SpecularMask("ハイライトのマスク (R: 白=光る 黒=消す。ベルトの金具や革だけ光らせる)", 2D) = "white" {}
        _SpecularShadowMask("ハイライトを影の側では消す (0=影の中でも金属や革が鈍く光る 1=影では消す)", Range(0, 1)) = 1
        [NoScaleOffset] _SpecularRampTex("ハイライトのランプ (左=光の外側 右=光の中心。光の形と色を決める。段は影のランプと同じ)", 2D) = "white" {}
        [HideInInspector] _SpecularRampOn("", Float) = 0
        [NoScaleOffset] _MatCapTex("マットキャップ", 2D) = "black" {}
        [HDR] _MatCapColor("マットキャップの色 (A=強さ)", Color) = (1, 1, 1, 1)
        _MatCapShadeMask("マットキャップを影の側では消す", Range(0, 1)) = 0
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
        _AnisoJitterStrength("天使の輪の毛束ギザギザ強さ (毛束に沿って上下に波打つ)", Range(0, 0.5)) = 0
        _AnisoJitterScale("天使の輪の毛束ギザギザ細かさ (UV 1 あたりの束数)", Range(5, 150)) = 35
        [HDR] _Aniso2Color("天使の輪 2本目の色 (A=強さ。サブの細いハイライト帯)", Color) = (1, 1, 1, 0)
        _Aniso2Shift("天使の輪 2本目の位置ずらし", Range(-1, 1)) = 0.18
        _Aniso2Sharpness("天使の輪 2本目の細さ", Range(1, 200)) = 90
        [Enum(Anisotropic, 0, MatCap, 1, UV2Band, 2, HairMapR, 3)] _RingMode("天使の輪の作り方 (異方性=髪の流れと光 / マットキャップ=見る向きで決まる輪 / UV2 の帯=メッシュの 2 番目の UV で帯を引く / 髪の陰影マップの R=異方性の輪を描いた範囲だけに出す)", Float) = 0
        [ToggleUI] _HairMapOn("髪の陰影マップ (HM) を使う", Float) = 0
        [NoScaleOffset] _HairMap("髪の陰影マップ (HM。G=束の影: 暗いほど影 / R=天使の輪の範囲。リニアで読む)", 2D) = "gray" {}
        _HairMapNeutral("HM の中立点 (G がこの値で光のまま。上げると影が増える)", Range(0, 1)) = 0.4
        _HairMapStrength("HM の強さ (描いた束が光の向きにどれだけ勝つか)", Range(0, 2)) = 1
        [NoScaleOffset] _RingTex("天使の輪の画像 (マットキャップ: 輪を描いた球 / UV2 の帯: 縦に帯の濃さ)", 2D) = "black" {}
        _RingOffset("天使の輪の位置 (マットキャップ・UV2 の帯)", Range(-1, 1)) = 0
        _RingViewShift("見る高さで帯をずらす量 (UV2 の帯)", Range(-1, 1)) = 0.3
        [ToggleUI] _AnisoUseRamp("天使の輪もハイライトのランプで形を決める", Float) = 0
        _HairWet("濡れ髪 (濡れたとき毛が束になり、束の間が暗く、天使の輪が細く鋭く。量は「濡れ」)", Range(0, 1)) = 0
        _HairWetClumps("濡れ髪 束の細かさ (UV 1 あたり)", Range(2, 200)) = 40

        // ---- リムライト・深度リム・逆光ライト
        [HDR] _RimColor("リムライトの色 (A=強さ)", Color) = (1, 1, 1, 0)
        _RimEdge("リムの境界", Range(0, 1)) = 0.5
        _RimSoft("リムのぼかし", Range(0, 1)) = 0.65
        _RimFalloff("リムの細さ", Range(0.01, 50)) = 3.5
        _RimShadeFade("リムを影の側では消す", Range(0, 1)) = 0.5
        _RimLightAlign("リムを光の向きへ絞る (-1=逆側 0=全周 1=光が差す側の輪郭だけ)", Range(-1, 1)) = 0
        [HDR] _RimOppositeColor("反対側(影側)の環境リム色 (A=強さ。照り返しの別色リム)", Color) = (0.45, 0.65, 1, 0)
        [Toggle(_DEPTH_RIM)] _DepthRim("深度リム (シルエットだけ光らせる)", Float) = 0
        _RimDepthWidth("深度リムの幅 (cm)", Range(0, 5)) = 1
        _RimDepthThreshold("深度リムの奥行きしきい値 (m)", Range(0.001, 1)) = 0.1
        [HDR] _BackLightTint("逆光ライトの色 (A=強さ)", Color) = (1, 0.95, 0.9, 0)
        _BackLightEdge("逆光ライトの境界", Range(0, 1)) = 0.35
        _BackLightSoft("逆光ライトのぼかし", Range(0, 1)) = 0.05
        _BackLightFocus("逆光ライトの指向性 (光が真後ろのときだけ)", Range(1, 20)) = 5
        _BackLightViewMix("逆光ライトの視線の影響", Range(0, 1)) = 1
        _BackLightAlbedoMix("逆光ライトにテクスチャ色を混ぜる", Range(0, 1)) = 0

        // ---- ラメ・屈折
        [HDR] _GlitterTint("ラメの色 (A=強さ)", Color) = (1, 1, 1, 0)
        [NoScaleOffset] _GlitterMask("ラメの出る所 (R)", 2D) = "white" {}
        _GlitterScale("ラメの細かさ (UV 1 あたりの粒の数)", Range(10, 2000)) = 300
        _GlitterSize("ラメの粒の大きさ", Range(0.05, 0.5)) = 0.25
        _GlitterDensity("ラメの量", Range(0, 1)) = 0.6
        _GlitterSharpness("ラメの光る角度の鋭さ (大きいほど一瞬だけ光る)", Range(1, 64)) = 12
        _GlitterShadeMask("ラメを影の側では消す", Range(0, 1)) = 0.7
        _RefractionStrength("屈折 (半透明のとき、後ろがゆがんで見える)", Range(0, 1)) = 0
        _RefractionDispersion("屈折の虹色のにじみ", Range(0, 1)) = 0.25

        // ---- 発光(メイン + レイヤー2)
        _EmissionMap("発光マスク", 2D) = "white" {}
        [HDR] _EmissionColor("発光色", Color) = (0, 0, 0, 1)
        [ToggleUI] _EmissionAnimate("発光 自動スクロール", Float) = 0
        _EmissionScroll("発光 動き (XY=スクロール/秒 Z=回転° W=回転°/秒)", Vector) = (0, 0, 0, 0)
        _EmissionFlicker("発光 明滅 (回/秒 0=しない)", Range(0, 10)) = 0
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
        [Header(Toon Plus)] _LightNormalize("光の色の正規化 (明るさをそろえて色味だけ残す)", Range(0, 1)) = 0
        _LightSoftLimit("明るさの上限をなめらかに (1 を超えたら徐々に抑える)", Range(0, 1)) = 0
        _AmbientHeadSplit("環境光を頭の向きで前と後ろに分ける (陰影の向きが落ち着く)", Range(0, 1)) = 0
        _ShadowAmbientTint("影の側を空の色に寄せる (環境光の SH)", Range(0, 1)) = 0
        _ReceiveShadowOffset("落ち影を受ける位置のずらし (cm、光の向きへ)", Range(0, 10)) = 0
        _AddLightIgnoreNormal("追加ライトは距離だけで明るく (面の向きを見ない)", Range(0, 1)) = 0
        _AddLightBorder("追加ライトの境界 (負=影1の境界と同じ)", Range(-1, 1)) = -1
        _AddLightMax("追加ライトの明るさの上限", Range(0, 10)) = 10
        _AddLightMono("追加ライトの色を灰色に", Range(0, 1)) = 0
        _RimDarkTint("リムシェード (輪郭の内側を掛け算で締める。A=強さ)", Color) = (0.55, 0.45, 0.55, 0)
        _RimShadePower("リムシェードの鋭さ", Range(0.5, 10)) = 3
        _RimDarkEdge("リムシェードの境界", Range(0, 1)) = 0.5
        _RimDarkSoft("リムシェードのぼかし", Range(0.001, 1)) = 0.15
        _RimDepthSoft("深度リムのやわらかさ (0=今までどおりの 2 値)", Range(0, 1)) = 0
        [ToggleUI] _MatCapViewFix("マットキャップの歪みを補正 (画面の端でも正しい)", Float) = 0
        [ToggleUI] _MatCapNoRoll("マットキャップをカメラの傾きに合わせない", Float) = 0
        [NoScaleOffset] _MatCapMask("マットキャップのマスク (R)", 2D) = "white" {}
        _IridescenceStrength("虹色の薄膜の強さ", Range(0, 2)) = 0
        _IridescenceScale("虹色の薄膜の色の回る速さ", Range(0, 5)) = 1.5
        _IridescenceShift("虹色の薄膜の色相のずらし", Range(0, 1)) = 0
        _ZOffsetCm("深度だけ手前へ (cm。眉・目のふちを髪の手前に)", Range(-5, 5)) = 0
        [NoScaleOffset] _ZOffsetMask("深度のずらしのマスク (R)", 2D) = "white" {}
        _PerspectiveRemoval("遠近の除去 (頭の中心の奥行きで描く。GalaxyToonFace が中心を入れる)", Range(0, 1)) = 0
        _DitherAlpha("全体のディザ透明度 (1=不透明。全パス同じ抜き方)", Range(0, 1)) = 1
        [ToggleUI] _RampDayNight("ランプの上半分を昼・下半分を夜にする (時刻で混ぜる)", Float) = 0
        _RampNightAmount("夜のランプの量 (グローバルの _GalaxyNight も足す)", Range(0, 1)) = 0
        _AnisoCenterShape("髪のハイライトの形を、正面ほど丸く・縁ほど細長く", Range(0, 1)) = 0
        _SpecularAA("ハイライトのチラつきを抑える (画面の変化でぼかす)", Range(0, 4)) = 0
        _EmissionHueSpeed("発光の色の移り変わり (1 秒あたりの色相の回転)", Range(0, 2)) = 0
        _EmissionViewColor("発光の、見る角度で変わる色 (A=強さ。縁ほど)", Color) = (0.4, 0.8, 1, 0)
        _VertexColorShadow("頂点カラー R で影を出やすく (R が黒いほど影)", Range(0, 1)) = 0
        _VertexColorBias("頂点カラー G で影の境界をずらす (灰=そのまま)", Range(0, 1)) = 0
        _SkirtShadowReceive("スカートの影を受ける (スカートの中と裾の下の脚を影に)", Range(0, 1)) = 0
        [Enum(Fill,0,Hatching,1,Halftone,2)] _MangaStyle("影の塗り方 (ベタ・ハッチング・網点)", Float) = 0
        _MangaSpacing("ハッチング・網点の間隔 (画面の px)", Range(2, 24)) = 6
        _MangaAngle("ハッチング・網点の角度 (度)", Range(0, 180)) = 45
        _MangaWidth("ハッチングの線の太さ (間隔に対する割合)", Range(0.05, 0.9)) = 0.35
        _OutlineVertexDepth("頂点カラー B で輪郭線を奥へ (cm)", Range(0, 2)) = 0
        _OutlineVertexAlpha("頂点カラー A で輪郭線の太さ", Range(0, 1)) = 0
        _OutlineLightWidth("輪郭線を光の側で太く", Range(0, 2)) = 0
        _OutlineFadeStart("輪郭線が細くなりはじめる距離 (m。終わりより大きいと使わない)", Float) = 20
        _OutlineFadeEnd("輪郭線が消える距離 (m)", Float) = 0
        [ToggleUI] _VolumeReceive("Volume の調整を受ける (Galaxy Toon の Volume。瞳など外したい所はオフ)", Float) = 1
        [Enum(Low, 0, Mid, 1, High, 2, All, 3)] _AudioBand("音に合わせる帯域 (低音・中音・高音・全体。Galaxy Audio React が入れる)", Float) = 0
        _AudioEmission("音で発光を強める量", Range(0, 4)) = 0
        _AudioRim("音でリムを強める量", Range(0, 4)) = 0
        _AudioOutline("音で輪郭線を太くする量", Range(0, 2)) = 0
        [Toggle(_CONTACT_SHADOW)] _ContactShadow("接地影 (画面の深度を光の向きへたどる。指・襟の接点に細い影)", Float) = 0
        _ContactShadowStrength("接地影の濃さ", Range(0, 1)) = 0.8
        _ContactShadowLength("接地影の長さ (cm)", Range(0.2, 10)) = 2
        _SelfShadowStrength("体の自己影の濃さ (接地影と同じたどり方で長く。顎の下・腕から胴。接地影をオンにすると効く)", Range(0, 1)) = 0
        _SelfShadowLength("体の自己影の長さ (cm)", Range(2, 40)) = 15
        _SelfShadowThickness("体の自己影で数える厚み (cm。これより奥は遠くの物)", Range(1, 30)) = 10
        _StarryStrength("星空 (画面に貼りつく星と星雲。マスクの所だけ)", Range(0, 4)) = 0
        [NoScaleOffset] _StarryMask("星空のマスク (R)", 2D) = "white" {}
        [HDR] _StarryColor("星の色", Color) = (1, 0.95, 0.85, 1)
        _StarryNebula("星雲の色 (A=強さ)", Color) = (0.35, 0.2, 0.8, 0.6)
        _StarryScale("星の細かさ", Range(4, 120)) = 40
        _StarryParallax("星の奥行き (視差)", Range(0, 1)) = 0.3
        [ToggleUI] _UseIlm("パック型マスク (R=ハイライトの強さ G=影のしきい値 B=ハイライトのマスク A=素材 ID)", Float) = 0
        [NoScaleOffset] _IlmTex("パック型マスク", 2D) = "white" {}
        _IlmShadowBias("パック型マスク G の効き (影の出やすさ)", Range(0, 1)) = 0.5
        [NoScaleOffset] _IlmLut("素材 ID ごとの色 (横 4 マス：ハイライト・リム・輪郭線・光る色。縦 8 段：素材 ID)", 2D) = "white" {}
        [NoScaleOffset] _NoseLineMask("鼻線のマスク (R。横顔のときだけ出る)", 2D) = "black" {}
        _NoseLineColor("鼻線の色 (A=強さ)", Color) = (0.55, 0.35, 0.35, 0)
        _NoseLineSide("鼻線が出はじめる横向きの量", Range(0, 1)) = 0.2
        // ふくらむ: 頂点を法線の向きへ押し出して、胸のように丸くふくらませる(マスクの所だけ・球の中ほど強く)。
        // 全部のパス(影・深度・輪郭線)で同じに動かすので、形がずれない
        // 脚に押される布: スカートなどの頂点が太もも(グローバルのカプセル _GalaxyLegCapsule*)の中に入ったら、カプセルの外へ出す(揺れの骨の列の間を脚が抜けない)
        _LegPush("脚に押される布 (0=オフ 1=オン。スカート・裾)", Float) = 0
        [Header(Inflate)] _Inflate("ふくらむ (0=オフ 1=オン)", Float) = 0
        _InflateAmount("ふくらむ量 (m。負でへこむ)", Range(-0.03, 0.06)) = 0.01
        [NoScaleOffset] _InflateMask("ふくらむ所のマスク (R=強さ。白で全部)", 2D) = "white" {}
        _InflateCenter("ふくらむ球 (xyz=中心(ワールド) w=半径。w=0 でマスクだけ)", Vector) = (0, 0, 0, 0)
        _InflateRound("球の丸さ (1=なだらか 0=平ら)", Range(0, 1)) = 0.7
        [Header(Soft Touch)] _SoftTouch("触れるとへこむ (0=オフ 1=オン。触れるものは GalaxyToon の外から MaterialPropertyBlock で入る)", Float) = 0
        _Softness("やわらかさ (0〜1)", Range(0, 1)) = 1
        [NoScaleOffset] _SoftMask("やわらかさのマスク (R=強さ)", 2D) = "white" {}
        _SoftDepth("いちばん深いへこみ (m)", Range(0, 0.5)) = 0.03
        _SoftSpread("まわりの沈む幅 (触れるものの太さの倍)", Range(0, 4)) = 1.5
        _SoftBulge("まわりの盛り上がり (体積を保つ)", Range(0, 1)) = 0.25
        _SoftBlush("押した所の血色 (A=強さ)", Color) = (1, 0.62, 0.6, 0.5)
        _SoftShadow("近づくものの影", Range(0, 1)) = 0.35
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
        _DissolveNoiseAmount("ノイズの強さ (向きで消すときの境目の乱れ)", Range(0, 3)) = 0.5
        [HideInInspector] _DissolveNoiseTexOn("ノイズテクスチャが入っている (GUI が入れる)", Float) = 0
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
        _OutlineColorTrace("色トレス (地の色の彩度を上げて少し暗くしたイラスト風の線色にする)", Range(0, 1)) = 0
        _OutlineZOffset("輪郭線の奥ずらし (cm。鼻筋や口元・襟元の内側の線を奥へ隠して消す)", Range(0, 5)) = 0
        _OutlineLit("輪郭線をライトの明るさに合わせる", Range(0, 1)) = 1
        _OutlineNearClamp("近いと細くする", Range(0, 1)) = 0.5
        _AnimeOutlinePixel("輪郭線を画面上で同じ太さに (アニメの線。1=遠くても細くならない)", Range(0, 1)) = 0
        // 手描きの線のゆらぎ: 輪郭線の太さを、場所ごとに少しずつ、コマ送り(1 秒に決めた回数)で変える(アニメの線のふるえ)
        _OutlineBoil("輪郭線のゆらぎ (手描きのふるえ)", Range(0, 1)) = 0
        _OutlineBoilFps("輪郭線のゆらぎのコマ数 (1 秒に)", Range(1, 24)) = 8
        _OutlineFovKeep("輪郭線をズーム(画角)でも同じ太さに (望遠で太く・広角で細くならない)", Range(0, 1)) = 1
        _OutlineCenterBoost("画面の中心ほど太く (中心で太さに足す割合。0=しない)", Range(0, 2)) = 0
        _OutlineCenterRadius("画面の中心ほど太くの範囲 (画面の半分の長さに対する割合)", Range(0.1, 1.5)) = 0.8

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
        [NoScaleOffset] _OutlineThicknessMap("輪郭線の太さマスク (R)", 2D) = "white" {}
        [Toggle] _OutlineVertexColor("頂点カラー (R) で線の太さを変える (黒=線なし)", Float) = 0

        // 「オフ」中のテクスチャの退避先(GalaxyToonGUI が使う。シェーダでは読まない)
        [HideInInspector] _BaseMap_Stash("", 2D) = "white" {}
        [HideInInspector] _BumpMap_Stash("", 2D) = "white" {}
        [HideInInspector] _OcclusionMap_Stash("", 2D) = "white" {}
        [HideInInspector] _Bump2Map_Stash("", 2D) = "white" {}
        [HideInInspector] _MetallicGlossMap_Stash("", 2D) = "white" {}
        [HideInInspector] _ShadeAmountMap_Stash("", 2D) = "white" {}
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
        [HideInInspector] _OutlineThicknessMap_Stash("", 2D) = "white" {}
        [HideInInspector] _GlitterMask_Stash("", 2D) = "white" {}
        [HideInInspector] _ThicknessMap_Stash("", 2D) = "white" {}
        [HideInInspector] _SpecularMask_Stash("", 2D) = "white" {}
        [HideInInspector] _SheerMask_Stash("", 2D) = "white" {}
        [HideInInspector] _NetDentMask_Stash("", 2D) = "white" {}
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
            AlphaToMask [_AlphaToMask]
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
            #pragma shader_feature_local_fragment _CREVICE_AO
            #pragma shader_feature_local_fragment _CONTACT_SHADOW
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
            #pragma shader_feature_local_fragment _SSS
            #pragma shader_feature_local_fragment _SHEER
            #pragma shader_feature_local_fragment _NET_DENT
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

        // 動きのベクトル(TAA・STP・モーションブラーで、動くキャラがぶれない)。形は描いた形と同じ
        // (触れるとへこむ・濡れて縮む・髪の揺れと平面化・深度だけ手前へ・遠近の除去を掛ける)。切り抜き・前髪の抜き・ディゾルブで抜いた所は書かない
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GalaxyToonMotionVert
            #pragma fragment GalaxyToonMotionFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local_fragment _NEAR_FADE
            #define GALAXY_MOTION_VECTORS
            #include "GalaxyToonPasses.hlsl"
            ENDHLSL
        }

        // 物の番号(画面の線の「物ごとの線」が使う): 物ごとの番号(_ObjectId。0 なら地の色から)を色にして書く。深度は書かず、手前の物だけ残る
        Pass
        {
            Name "GalaxyObjectId"
            Tags { "LightMode" = "GalaxyObjectId" }
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GalaxyToonObjectIdVert
            #pragma fragment GalaxyToonObjectIdFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local_fragment _NEAR_FADE
            #define GALAXY_OBJECT_ID
            #include "GalaxyToonPasses.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "GalaxyToon.Editor.GalaxyToonGUI"
}
