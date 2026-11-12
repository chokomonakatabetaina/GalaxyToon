// GalaxyShader/Toon の共通部分(マテリアルの値・テクスチャ・トゥーンの計算)。全パスで同じ CBUFFER を使い、SRP Batcher に載せる。
#ifndef GALAXY_TOON_INPUT_INCLUDED
#define GALAXY_TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
SAMPLER(sampler_linear_repeat); // マスク類はこれで引く(_BaseMap を使わないパスでも使える Unity のインラインサンプラー)
TEXTURE2D(_ShadeAmountMap);
TEXTURE2D(_ShadeMap);
TEXTURE2D(_ShadowBiasMask);
TEXTURE2D(_MatCapTex);
SAMPLER(sampler_linear_clamp);  // マットキャップ用(球の縁で繰り返さない)
SAMPLER(sampler_point_clamp);   // 髪の影(法線テクスチャの印をぼかさずに読む)
TEXTURE2D(_EmissionMap);
TEXTURE2D(_Emission2Map);
TEXTURE2D(_OutlineThicknessMap);
TEXTURE2D(_ShadowRampTex);      SAMPLER(sampler_ShadowRampTex);
TEXTURE2D(_RampIdMap);
TEXTURE2D(_SpecularRampTex);
TEXTURE2D(_FaceSdfTex);
TEXTURE2D(_SoftMask);
TEXTURE2D(_InflateMask);
TEXTURE2D(_MatCapMask);
TEXTURE2D(_ZOffsetMask);
TEXTURE2D(_StarryMask);
TEXTURE2D(_NoseLineMask);
TEXTURE2D(_RingTex);
TEXTURE2D(_HairMap);
TEXTURE2D(_IlmTex);
TEXTURE2D(_IlmLut);
// 遠近の除去の中心(GalaxyToonFace がキャラの全部の Renderer に入れる。w=1 で有効)
float4 _PerspectiveCenter;
// 高さの毛先: ワールドの位置 → 最初の姿勢でのメッシュの高さ、の行(GalaxyToonFace が Renderer ごとに入れる。頭の今の姿勢の逆 × 頭の最初の姿勢)。
// 0 なら物体の高さを使う
float4 _TipRestRow;
// 頭の前の向き(GalaxyToonFace が、環境光を頭の向きで分けるマテリアルの Renderer に入れる。w=1 で有効)
float4 _HeadForward;
// 音の大きさ(Galaxy Audio React が入れる。x=低音 y=中音 z=高音 w=全体。0〜1 くらい。入れなければ 0)
float4 _GalaxyAudio;
// 昼／夜(0=昼 1=夜)。時刻のスクリプトや GalaxyToonLight が入れる(グローバル。入れなければ 0)
float _GalaxyNight;
// 触れるとへこむ: 触れるもの(カプセル。A.xyz・B.xyz が両端、A.w が半径、B.w が強さ)。キャラごとに MaterialPropertyBlock で入る(最大 32)
float4 _SoftContactA[32];
float4 _SoftContactB[32];
float _SoftContactCount;
TEXTURE2D(_FxMask);             // R=濡れ G=汗 B=液体 の出る場所
TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
TEXTURE2D(_MetallicGlossMap);   // R=金属 A=つや(URP 標準と同じ並び)
TEXTURE2D(_OcclusionMap);
TEXTURE2D(_MatCap2Tex);         TEXTURE2D(_MatCap2Mask);
TEXTURE2D(_Bump2Map);
TEXTURE2D(_DissolveNoise);
TEXTURE2D(_Layer1Tex); TEXTURE2D(_Layer1Mask);
TEXTURE2D(_Layer2Tex); TEXTURE2D(_Layer2Mask);
TEXTURE2D(_Layer3Tex); TEXTURE2D(_Layer3Mask);
TEXTURE2D(_GlitterMask);
TEXTURE2D(_NetDentMask);        // 網タイツの食い込みを出す範囲(R)
TEXTURE2D(_ThicknessMap);       // 厚み・SSSマスク(R: 白=薄く透ける肌 黒=透けない)
TEXTURE2D(_SpecularMask);       // ハイライトのマスク(R: 白=光る 黒=消す)
TEXTURE2D(_SheerMask);          // ストッキングの範囲マスク(R: 白=透ける所)

// スカートの影（グローバル。使う側のスクリプトが毎フレーム設定）：x 裾の高さ・y 裾から下の帯の幅・z 強さ・w 半径(m)、center：x=中心の X・y=中心の Z・z=腰の高さ
float4 _GalaxySkirtShadow;
float4 _GalaxySkirtShadowCenter;

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    half _AlphaToMask;
    half _BumpScale;
    half _OcclusionStrength;
    half _CreviceStrength;
    half _CreviceRadius;
    float4 _Bump2Map_ST;
    half _Bump2Scale;
    half _HueShift;
    half _Saturation;
    half _Brightness;
    half _Gamma;
    half _Metallic;
    half _Smoothness;
    half _ReflectionStrength;
    half _ReflectionSteps;
    half _ClearCoat;
    half _ClearCoatSmoothness;
    half4 _VelvetColor;
    half _VelvetPower;
    half _TriplanarScale;
    half _TriplanarBlend;

    half _LightSource;
    half _LightFloor;
    half _LightCeiling;
    half _LightGray;
    half _AmbientStrength;
    half _AdditionalLightStrength;
    half _BakedSteps;

    half4 _Shade1Tint;
    half _ShadeMapOn;        // 影色テクスチャが入っているか(GUI が入れる)
    half _ShadowBiasStrength;
    half _Shade1Edge;
    half _Shade1Soft;
    half4 _Shade2Tint;
    half _Shade2Edge;
    half _Shade2Soft;
    half4 _Shade3Tint;
    half _Shade3Edge;
    half _Shade3Soft;
    half _CastShadowAmount;
    half _EdgeSmooth;
    half _FaceShading;
    float4 _FaceForward; // GalaxyToonFace が頭の向きを入れる(xyz)。0 ならモデルの正面(+Z)
    float4 _FaceRight;   // 同じく頭の右(xyz)。0 ならモデルの右(+X)
    float4 _FaceUp;      // 同じく頭の上(xyz)。シェーダーでは正面と右から求めるので使っていない(互換のため残す)
    float4 _FaceCenter;  // 頭の中心(xyz)、w=1 で有効
    half _FaceSphere;
    half _FaceSdfPitch;
    half4 _FillLightColor;   // カメラからの補助光(A=強さ)
    half _ShadowPattern;
    half4 _ShadowPatternColor;
    half _ShadowPatternDensity;
    half _ShadowPatternWidth;
    half _FaceSdfIgnoreShadow;
    half _ShadowTerminatorFade;
    float4 _BangCenter;  // GalaxyToonSeeThroughBangs がキャラごとに入れる(w=1 で有効。0 ならグローバルへフォールバック)
    float4 _BangUp;
    float4 _BangForward;
    float4 _BangRange;
    float4 _BangRegion;
    half _HairSway;
    half4 _TipColor;
    half _TipAxis;
    half _TipStart;
    half _TipEnd;
    float4 _HairSwayOffset; // GalaxyToonHairSway が入れる: 毛先の遅れ(ワールド, m)
    half _TexLineKeep;      // テクスチャの線を遠くでも残す強さ(0=しない)
    float _ObjectId;        // 物の番号(画面の物ごとの線。0 なら地の色から作る)
    float4 _HairSwayOffsetPrev; // 前のフレームの毛先の遅れ(動きのベクトルで、揺れそのものの動きも出す。w=1 で入っている)
    float4 _HairSwayPivot;  // 揺れの支点(頭の上, ワールド) w=毛先までの長さ(m)
    float4 _HairSwayPivotPrev; // 前のフレームの支点
    float4 _HairSwayUp;     // 頭の上向き(ワールド)
    float4 _Flatten0;       // 平面化の範囲1〜3: xyz=中心(ワールド) w=半径(m)。使う側がカメラごとに入れる
    float4 _Flatten1;
    float4 _Flatten2;
    float4 _FlattenAmount;  // xyz=範囲1〜3の強さ(0〜1)
    float4 _FlattenCamera;  // xyz=カメラの位置(透視)か向き(平行) w: 0=平面化しない 1=透視 2=平行

    half4 _RimColor;
    half _RimEdge;
    half _RimSoft;
    half _RimFalloff;
    half _RimShadeFade;
    half _RimLightAlign;
    half4 _RimOppositeColor;
    half _RimDepthWidth;
    half _RimDepthThreshold;

    half4 _SpecularColor;
    half _SpecularSize;
    half _SpecularSoft;
    half _SpecularShadowMask;

    half4 _MatCapColor;
    half _MatCapShadeMask;
    half4 _MatCap2Color;
    half _MatCap2Blend;
    half _MatCap2ShadowMask;

    half4 _AnisoColor;
    half _AnisoShift;
    half _AnisoSharpness;
    half _AnisoBorder;
    half _AnisoBlur;
    half _AnisoDirection;
    half _AnisoShadowMask;
    half _AnisoJitterStrength;
    half _AnisoJitterScale;
    half4 _Aniso2Color;
    half _Aniso2Shift;
    half _Aniso2Sharpness;

    half4 _BackLightTint;
    half _BackLightEdge;
    half _BackLightSoft;
    half _BackLightFocus;
    half _BackLightViewMix;
    half _BackLightAlbedoMix;

    half _DissolveAmount;
    half _DissolveMode;
    float4 _DissolveDirection; // xyz=消えていく向き(ワールド) w=範囲(m)
    half _DissolveStart;
    half _DissolveNoiseScale;
    half _DissolveNoiseAmount;
    half _DissolveNoiseTexOn; // ノイズテクスチャが入っているか(GUI が入れる)
    half _DissolveInvert;
    half _DissolveWorld;
    half _DissolveEdgeWidth;
    half4 _DissolveEdgeColor;

    half4 _EmissionColor;
    float4 _EmissionMap_ST;
    float4 _EmissionScroll;  // xy=スクロール z=回転° w=回転°/秒
    half _EmissionAnimate;
    half _EmissionFlicker;
    half4 _Emission2Color;
    float4 _Emission2Map_ST;
    float4 _Emission2Scroll;
    half _Emission2Animate;
    half _Emission2Blink;
    half _Layer1Animate;
    half _Layer2Animate;
    half _Layer3Animate;

    half4 _OutlineColor;
    float _OutlineWidth;
    half _OutlineNearClamp;
    half _OutlineTexBlend;
    half _OutlineColorTrace;
    float _OutlineZOffset;
    half _OutlineLit;

    half _WetAmount;
    half _WetDarken;
    half _WetGloss;
    half _WetSoak;
    half _WetShrink;
    half _LightNormalize;
    half _LightSoftLimit;
    half _ShadowAmbientTint;
    half _ReceiveShadowOffset;
    half _AddLightIgnoreNormal;
    half _AddLightBorder;
    half _AddLightMax;
    half _AddLightMono;
    half4 _RimDarkTint;
    half _RimShadePower;
    half _RimDarkEdge;
    half _RimDarkSoft;
    half _RimDepthSoft;
    half _MatCapViewFix;
    half _MatCapNoRoll;
    half _IridescenceStrength;
    half _IridescenceScale;
    half _IridescenceShift;
    half _ZOffsetCm;
    half _PerspectiveRemoval;
    half _DitherAlpha;
    half _RampDayNight;
    half _RampNightAmount;
    half _AnisoCenterShape;
    half _SpecularAA;
    half _EmissionHueSpeed;
    half4 _EmissionViewColor;
    half _VertexColorShadow;
    half _VertexColorBias;
    half _SkirtShadowReceive;
    half _MangaStyle;
    half _MangaSpacing;
    half _MangaAngle;
    half _MangaWidth;
    half _OutlineVertexDepth;
    half _OutlineVertexAlpha;
    half _OutlineLightWidth;
    float _OutlineFadeStart;
    float _OutlineFadeEnd;
    half _ContactShadow;
    half _ContactShadowStrength;
    half _ContactShadowLength;
    half _SelfShadowStrength;
    half _SelfShadowLength;
    half _SelfShadowThickness;
    half _StarryStrength;
    half4 _StarryColor;
    half4 _StarryNebula;
    half _StarryScale;
    half _StarryParallax;
    half4 _NoseLineColor;
    half _NoseLineSide;
    half _UseIlm;
    half _RingMode;
    half _HairMapOn;
    half _HairMapNeutral;
    half _HairMapStrength;
    half _AudioBand;
    half _VolumeReceive;
    half _AmbientHeadSplit;
    half _AudioEmission;
    half _AudioRim;
    half _AudioOutline;
    half _RingOffset;
    half _RingViewShift;
    half _IlmShadowBias;
    half _Inflate;
    half _LegPush;
    half _InflateAmount;
    float4 _InflateCenter;
    half _InflateRound;
    half _SoftTouch;
    half _Softness;
    half _SoftDepth;
    half _SoftSpread;
    half _SoftBulge;
    half4 _SoftBlush;
    half _SoftShadow;
    half _HairWet;
    half _HairWetClumps;
    half _SweatAmount;
    half _SweatScale;
    half _SweatSize;
    half _SweatFlow;
    half _LiquidAmount;
    half4 _LiquidColor;
    half _LiquidScale;
    half _LiquidFlow;
    half _LiquidGloss;
    half _LiquidViscosity;
    half _FoamAmount;
    half _FoamReceive;
    half4 _FoamColor;
    half _FoamScale;
    half _FoamSize;
    half _FoamFlow;
    half _FoamPop;
    half _FoamMerge;
    half _RainAmount;
    half _RainReceive;
    half _RainRipples;
    half _RainFlowAmount;
    half _RainSplash;
    half _RainScale;
    half _RainSpeed;
    half _FxBump;
    half _DirtAmount;
    half _DirtReceive;
    half4 _DirtColor;
    half _DirtScale;
    half _ScratchAmount;
    half4 _ScratchColor;
    half _ScratchScale;
    half _HairShadowCast;
    half _HairShadowReceive;
    half _HairShadowDistance;
    half _HairShadowBias;
    half _HairShadowSoftness;
    half4 _SssColor;
    half _SssEdgeWidth;
    half _SssShadowBleed;
    half _SssTranslucency;
    half _SssDistortion;
    half _SssPower;
    half _SssFrontScatter;
    half _SheerAmount;
    half4 _SheerSkinColor;
    half _SheerPower;
    half _SheerSheen;
    half _NetSource;
    half _NetThreshold;
    half _NetDentWidth;
    half _NetDentStrength;
    half _NetDentShadow;
    half4 _NetDentColor;
    half _NetDentHighlight;
    float4 _BaseMap_TexelSize;
    float4 _EyeRect1;
    float4 _EyeRect2;
    half _EyeParallax;
    half4 _EyeHighlightColor;
    float4 _EyeHighlightPos;
    half _EyeHighlightSize;
    half _EyeWet;
    half4 _EyeSparkleColor;
    half _EyeSparkleScale;
    half _EyeSparkleSpeed;
    half4 _EyeLidShadowColor;
    half _EyeLidShadowWidth;
    half _EyeIrisRadius;
    half _EyeLidShadowSoft;
    half4 _EyeCausticColor;
    half _EyeCausticSize;
    half _BlushAmount;
    half4 _BlushColor;
    float4 _BlushUV;
    half _BlushSize;
    half _BlushSoft;
    half _BlushLines;
    half4 _GlitterTint;
    half _GlitterScale;
    half _GlitterSize;
    half _GlitterDensity;
    half _GlitterSharpness;
    half _GlitterShadeMask;
    half _RefractionStrength;
    half _RefractionDispersion;
    half _UseNearFade;
    half _NearFadeStart;
    half _NearFadeEnd;
    half _OutlineSmoothNormal;
    half _OutlineVertexColor;
    half _WetReceive;
    half _SweatReceive;
    half _LiquidReceive;
    float4 _LiquidFlowDir;

    float4 _Layer1Tex_ST;
    half4 _Layer1Color;
    half _Layer1Blend;
    float4 _Layer1Scroll; // xy=スクロール(UV/秒) z=回転(度) w=回転の速さ(度/秒)
    half _Layer1Fade;
    half _Layer1Blink;
    half _Layer1BlinkPhase;
    half _Layer1Unlit;

    float4 _Layer2Tex_ST;
    half4 _Layer2Color;
    half _Layer2Blend;
    float4 _Layer2Scroll; // xy=スクロール(UV/秒) z=回転(度) w=回転の速さ(度/秒)
    half _Layer2Fade;
    half _Layer2Blink;
    half _Layer2BlinkPhase;
    half _Layer2Unlit;

    float4 _Layer3Tex_ST;
    half4 _Layer3Color;
    half _Layer3Blend;
    float4 _Layer3Scroll; // xy=スクロール(UV/秒) z=回転(度) w=回転の速さ(度/秒)
    half _Layer3Fade;
    half _Layer3Blink;
    half _Layer3BlinkPhase;
    half _Layer3Unlit;
    half _BangFadeMode;     // 前髪の毛先の透け: 0=使わない 1=毛先以外(不透明側) 2=毛先だけ(半透明側)
    half _BangFadeStrength; // 毛先の透ける強さ
    half _ShadowRampRows;   // ランプの段数(部位ごと)
    half _ShadowRampRow;    // 部位IDマップがないときの段
    half _RampIdOn;         // 部位IDマップあり
    half _SpecularRampOn;   // ハイライトのランプあり
    half _AnisoUseRamp;     // 天使の輪もランプで描く
    half _AnimeOutlinePixel; // 輪郭線を画面上で同じ太さに
    half _OutlineFovKeep;    // 輪郭線を画角でも同じ太さに
    half _OutlineBoil;       // 輪郭線のゆらぎ
    half _OutlineBoilFps;
    half _OutlineCenterBoost; // 画面の中心ほど太く
    half _OutlineCenterRadius;
    half _AnimeHardness;     // アニメ塗り: 影の境界をくっきり
    half _AnimeShadowHue;
    half _AnimeShadowSat;
    half _AnimeShadowValue;
    half4 _AnimeEdgeColor;
    half _AnimeEdgeWidth;
    half _AnimeFlatLight;
    half _AnimeFlatNormal;
    half _AnimeFlatTexture;
    half _AnimeFlatMip;
    half _AnimeNoGloss;
CBUFFER_END

// GalaxyToonLight が毎フレーム設定する「キャラ専用の固定ライト」。未設定(0)ならシーンのメインライトを使う。
float4 _AdvToonLightDir;
float _GalaxyToonOff; // 見比べ用(グローバル): 1 なら URP 標準の PBR 計算で描き、輪郭線を消す(「シェーダー OFF」)
float _GalaxyBakedLightOffset; // 焼いた光の明るさ - 1(グローバル。使う側のスクリプトが設定。未設定 0 = そのまま)
half4 _AdvToonLightColor;
// 脚のカプセル(グローバル。使う側のスクリプトが毎フレーム設定): 0・1 が左右。Start.xyz=付け根 w=半径、End.xyz=先(半径 0 なら無し)
float4 _GalaxyLegStart0, _GalaxyLegEnd0, _GalaxyLegStart1, _GalaxyLegEnd1;
// 後ろからの光(逆光のリム。グローバル。使う側のスクリプトが設定。a=0 なら何もしない)。xyz=光の来る向き(ワールド)
float4 _GalaxyBackLightDir;
half4 _GalaxyBackLightColor; // rgb=色 a=強さ
// キャラ 1 体だけのライト(GalaxyToonLight の「対象」。Renderer ごとの MaterialPropertyBlock で入る。xyz=向き w=夜の量。0 ならグローバルの値)
float4 _GalaxyLocalLightDir;
half4 _GalaxyLocalLightColor;

// Volume でまとめて調整(Galaxy Toon Volume Feature が、カメラごとに Volume の値を入れる。入っていなければ 0 = 何もしない)
float4 _GalaxyVolumeA; // x=リムの倍率 - 1 y=影を暗くする量 z=彩度(-1〜1) w=明るさの倍率 - 1
float4 _GalaxyVolumeB; // rgb=影の色 - 1(白 = 0) w=輪郭線の太さの倍率 - 1

/// Volume の調整: 影の側を影の色で染めて暗くし、彩度・明るさを変える(lit は光の当たり具合 0〜1)
half3 GalaxyVolumeAdjust(half3 color, half lit)
{
    if (_VolumeReceive < 0.5h) return color;
    half shade = 1.0h - saturate(lit);
    color *= lerp(half3(1, 1, 1), (1.0h + (half3)_GalaxyVolumeB.rgb) * (1.0h - (half)_GalaxyVolumeA.y), shade);
    half luma = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
    color = lerp(luma.xxx, color, 1.0h + (half)_GalaxyVolumeA.z);
    return max(color * (1.0h + (half)_GalaxyVolumeA.w), 0.0h);
}
half GalaxyVolumeRim() { return _VolumeReceive > 0.5h ? max(1.0h + (half)_GalaxyVolumeA.x, 0.0h) : 1.0h; }
float GalaxyVolumeOutline() { return _VolumeReceive > 0.5h ? max(1.0 + _GalaxyVolumeB.w, 0.0) : 1.0; }

/// マテリアルで選んだ帯域の音の大きさ
half GalaxyAudioLevel()
{
    return _AudioBand < 0.5h ? _GalaxyAudio.x : (_AudioBand < 1.5h ? _GalaxyAudio.y : (_AudioBand < 2.5h ? _GalaxyAudio.z : _GalaxyAudio.w));
}

bool GalaxyHasLocalLight() { return dot(_GalaxyLocalLightDir.xyz, _GalaxyLocalLightDir.xyz) > 1e-4; }
/// キャラ専用の固定ライトの向き(キャラ 1 体だけのライトがあればそちら。0 なら未設定)
float3 GalaxyToonLightDir() { return GalaxyHasLocalLight() ? _GalaxyLocalLightDir.xyz : _AdvToonLightDir.xyz; }
half3 GalaxyToonLightColor() { return GalaxyHasLocalLight() ? _GalaxyLocalLightColor.rgb : _AdvToonLightColor.rgb; }

// 髪の影: GalaxyHairShadowFeature が「法線プリパスあり」のカメラで 1 にする(URP の深度・法線テクスチャを読む)
float _GalaxyHairShadowOn;

// ---- カメラが近いと透ける: 距離で 0→1、4x4 の網目(ディザ)で抜く。不透明のまま透けて見える
// 全体のディザ透明度も同じ網目で抜く(出入り・遮蔽物の向こう。全パスで同じ抜き方)
void GalaxyNearFade(float3 positionWS, float4 positionCS)
{
    float fade = _DitherAlpha;
    #if defined(_NEAR_FADE)
        float d = distance(positionWS, GetCameraPositionWS());
        fade *= saturate((d - _NearFadeStart) / max(_NearFadeEnd - _NearFadeStart, 1e-3));
    #endif
    if (fade < 0.999)
    {
        static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        uint2 p = (uint2)positionCS.xy % 4u;
        clip(fade - (bayer[p.y * 4 + p.x] + 0.5) / 16.0);
    }
}

/// パック型マスク: 素材 ID の段で、横 4 マス(0 ハイライト・1 リム・2 輪郭線・3 光る色)の 1 つを引く。使わないときは白
half4 GalaxyIlmLut(float2 uv, int column)
{
    if (_UseIlm < 0.5h) return half4(1, 1, 1, 0);
    half id = round(SAMPLE_TEXTURE2D_LOD(_IlmTex, sampler_point_clamp, uv, 0).a * 7.0h);
    return SAMPLE_TEXTURE2D_LOD(_IlmLut, sampler_point_clamp, float2((column + 0.5) / 4.0, 1.0 - (id + 0.5) / 8.0), 0);
}

/// クリップ空間の仕上げ: 深度だけ手前へずらす(xy は変えない)と、遠近の除去(頭の中心の奥行きで画面に写す)。影のパスでは使わない
float4 GalaxyClipAdjust(float4 positionCS, float3 positionWS, float2 uv)
{
    if (_PerspectiveRemoval > 0.001h && _PerspectiveCenter.w > 0.5)
    {
        float centerW = TransformWorldToHClip(_PerspectiveCenter.xyz).w;
        if (centerW > 1e-4) positionCS.xy = lerp(positionCS.xy, positionCS.xy * (positionCS.w / centerW), _PerspectiveRemoval);
    }

    if (abs(_ZOffsetCm) > 1e-4h)
    {
        half mask = SAMPLE_TEXTURE2D_LOD(_ZOffsetMask, sampler_linear_repeat, uv, 0).r;
        float3 viewPos = TransformWorldToView(positionWS);
        viewPos.z += _ZOffsetCm * 0.01 * mask; // ビュー空間はカメラの前が -z。+ でカメラへ近づく
        float4 shifted = TransformWViewToHClip(viewPos);
        if (shifted.w > 1e-5) positionCS.z = shifted.z / shifted.w * positionCS.w;
    }

    return positionCS;
}

half4 GalaxyToonAlbedo(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
}

/// テクスチャに描いた線を、遠くでも残す: 遠いほど、ミップを細かくした(くっきりした)見本も引いて、暗い方を残す
/// (遠くでは粗いミップで線がまわりと混ざって薄く消えるため)。近く(1.5m まで)は何もしない
half3 GalaxyTexLineKeep(half3 albedo, float2 uv, float3 positionWS)
{
    if (_TexLineKeep <= 0.001h) return albedo;
    float dist = length(GetCameraPositionWS() - positionWS);
    half k = (half)saturate((dist - 1.5) / 4.0) * _TexLineKeep;
    if (k <= 0.001h) return albedo;
    half3 sharp = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, uv, -1.5 * k).rgb * _BaseColor.rgb;
    return lerp(albedo, min(albedo, sharp), k);
}

/// ランプ画像の縦位置(使う段の中心)。段は部位IDマップの R、なければマテリアルの段番号。いちばん上の段 = 0
float GalaxyRampRowV(float2 uv)
{
    half rows = max(1.0h, round(_ShadowRampRows));
    half row = _RampIdOn > 0.5h ? round(SAMPLE_TEXTURE2D(_RampIdMap, sampler_point_clamp, uv).r * (rows - 1.0h)) : min(round(_ShadowRampRow), rows - 1.0h);
    return 1.0 - (row + 0.5) / rows; // テクスチャの v=1 が画像のいちばん上
}

void GalaxyToonClip(half alpha)
{
    #if defined(_ALPHATEST_ON)
        clip(alpha - _Cutoff);
    #endif
}

// ---- 前髪の透け(GalaxyToonSeeThroughBangs が頭の位置・向き・範囲をグローバルで渡す)
// 前髪全体(顔の前・生え際より下)を「前髪側」として別に描き、付け根から 3/5 までは透けず、そこから毛先へグラデーションで透ける。
// 前髪の途中で描き方を切り替えないので、境目の線が出ない。
float4 _GalaxyBangCenter;  // xyz=頭の基準点 w=1 で有効
float4 _GalaxyBangUp;      // xyz=頭の上
float4 _GalaxyBangForward; // xyz=顔の正面
float4 _GalaxyBangRange;   // x=透け始める高さ y=いちばん透ける高さ(基準点から上へ m) z=左右の幅の半分 w=顔より前に出ている量のしきい値
float4 _GalaxyBangRegion;  // x=前髪の付け根(生え際)の高さ

/// 前髪の範囲(顔の前・左右の幅の内側・生え際より下)なら region=1。fade は 0=透けない 1=いちばん透ける
/// キャラごとの値(_BangCenter 等、MaterialPropertyBlock 経由)があれば優先し、無ければ従来のグローバル変数を参照する
half GalaxyBangFade(float3 positionWS, out half region)
{
    region = 0;
    if (_BangFadeMode < 0.5h) return 0;
    bool useLocal = _BangCenter.w > 0.5;
    float4 center = useLocal ? _BangCenter : _GalaxyBangCenter;
    if (center.w < 0.5) return 0;
    float3 upVec = useLocal ? _BangUp.xyz : _GalaxyBangUp.xyz;
    float3 fwdVec = useLocal ? _BangForward.xyz : _GalaxyBangForward.xyz;
    float4 range = useLocal ? _BangRange : _GalaxyBangRange;
    float4 reg = useLocal ? _BangRegion : _GalaxyBangRegion;
    float3 d = positionWS - center.xyz;
    float h = dot(d, upVec);
    float f = dot(d, fwdVec);
    float side = length(d - upVec * h - fwdVec * f);
    region = (f > range.w && side < range.z && h < reg.x) ? 1.0h : 0.0h;
    half t = smoothstep(0.0, 1.0, saturate((range.x - h) / max(range.x - range.y, 1e-4)));
    half sideFade = saturate((range.z - side) / 0.03); // 横へいくほど透けない(横髪との境目もなめらかに)
    return t * sideFade * region;
}

/// 不透明側: 前髪の範囲は描かない(前髪側が描く)。全パス共通
void GalaxyBangSolidClip(float3 positionWS)
{
    if (_BangFadeMode < 0.5h || _BangFadeMode > 1.5h) return;
    half region; GalaxyBangFade(positionWS, region);
    if (region > 0.5h) clip(-1);
}

/// 前方・輪郭線パス用: 不透明側は前髪を捨て、前髪側は前髪だけ残して毛先ほどアルファを下げる
void GalaxyBangFadeAlpha(float3 positionWS, inout half alpha)
{
    if (_BangFadeMode < 0.5h) return;
    half region;
    half fade = GalaxyBangFade(positionWS, region);
    if (_BangFadeMode < 1.5h) { if (region > 0.5h) clip(-1); return; }
    if (region < 0.5h) clip(-1);
    #if !defined(_ALPHATEST_ON)
        alpha = 1.0h; // 不透明の髪はテクスチャのアルファを透明度に使わない(アルファを発光マスクに使うモデルがある)
    #endif
    alpha *= 1.0h - fade * _BangFadeStrength;
}

// ---- 頂点を動かす処理(全パスで 濡れて縮む → 髪の揺れ の順に同じく掛ける)

/// 触れるとへこむ: 触れるカプセルごとに、カプセルの中心が面(頂点の接平面)へ入り込んだ深さと、横のずれから、
/// 「カプセルの丸い底に沿うへこみ」と「まわりがなめらかに沈む輪」の深い方だけ法線の内側へ下げる。さらに外側は少し盛り上げる(体積を保つ)。
/// 頂点ごとの連続な式なので、メッシュの細かさに関係なくなめらか。面の裏側にあるカプセル(中心が面の内側へ半径より深い)は数えない(自分の腕・指の皮膚をへこませない)。
/// tilt には、へこみの壁で法線を倒す向き(ワールド)を返す
float GalaxySoftTouchDepth(float3 positionWS, float3 n, float2 uv, out float3 tilt)
{
    tilt = 0;
    if (_SoftTouch < 0.5h || _SoftContactCount < 0.5) return 0;
    // マスク: R=やわらかさ、G=肉の厚み(白=厚い。いちばん深いへこみと、まわりの沈む幅を決める)、B=平らさ(白=平ら。黒ほど丸い=曲がり 50/m)
    half3 mask = SAMPLE_TEXTURE2D_LOD(_SoftMask, sampler_linear_repeat, uv, 0).rgb;
    half soft = _Softness * mask.r;
    if (soft <= 0.001h) return 0;
    float flesh = mask.g;
    float curvature = (1.0 - mask.b) * 50.0;   // 1/m
    float total = 0;
    int count = min((int)_SoftContactCount, 32);
    [loop] for (int i = 0; i < count; i++)
    {
        float3 a = _SoftContactA[i].xyz;
        float3 b = _SoftContactB[i].xyz;
        float r = max(_SoftContactA[i].w, 1e-4);
        float strength = _SoftContactB[i].w;
        float3 ab = b - a;
        float t = saturate(dot(positionWS - a, ab) / max(dot(ab, ab), 1e-8));
        float3 d = a + ab * t - positionWS;
        float h = dot(d, n);                 // カプセルの中心の、面からの高さ(外が +)
        if (h >= r || h < -r) continue;      // 面に届かない / 面の裏側
        float3 lat = d - n * h;
        float l = length(lat);
        // 丸い所に沿う: 面が横へ離れるほど下がる分(l^2 × 曲がり / 2)だけ、カプセルが面から離れているとみなす
        h += l * l * curvature * 0.5;
        if (h >= r) continue;
        // まわりの沈む幅は太さに比例(床などの大きな球は 12cm 相当で頭打ち)
        float reach = min(r, 0.12) * max(_SoftSpread, 0.01) * (0.6 + 0.6 * flesh);
        if (l > r * 1.6 + reach * 2.0) continue;
        // 頂点がカプセルの表面から沈む幅より遠ければ数えない(横を向いた面は接平面が大きな球と交わっても、頂点そのものは離れている)
        if (length(d) - r > reach * 2.0 + 0.002) continue;
        float pen = r - h;                   // 接平面へ入り込んだ深さ
        // 丸い底に沿う: この横のずれで、カプセルの底がいまの面よりどれだけ下にあるか
        float sphere = l < r ? sqrt(max(r * r - l * l, 0)) - h : 0;
        // まわりの沈み: 触れた所から離れるほどなめらかに浅く
        float lx = max(l - r * 0.5, 0) / reach;
        float ring = pen * 0.6 * exp(-2.0 * lx * lx);
        float dent = max(max(sphere, ring), 0) * strength;
        // 体積を保つ盛り上がり: 触れた所の外側に、入り込んだ深さに合わせて
        float bulge = _SoftBulge * pen * 0.35 * smoothstep(r * 0.9, r * 1.5, l) * (1.0 - smoothstep(r * 1.5, r * 1.5 + reach * 1.5, l)) * strength;
        total += dent - bulge;
        if (l > 1e-5 && dent > 0) tilt += (lat / l) * saturate(dent / (r * 0.6));
    }
    // 肉の厚み: 薄い所(すね・ひじ・骨の近く)は浅く
    float maxDepth = _SoftDepth * (0.3 + 0.7 * flesh);
    float depth = clamp(total * soft, -maxDepth * 0.3, maxDepth);
    tilt *= soft;
    return depth;
}

/// 触れるとへこむの見た目。x=近づくものの影(0〜1。面の前にあるものが、太さの 2 倍の距離から近づくほど濃い)、y=押した強さ(深さ ÷ いちばん深いへこみ)
half2 GalaxySoftTouchFx(float3 positionWS, float3 normalWS, float2 uv)
{
    if (_SoftTouch < 0.5h || _SoftContactCount < 0.5) return 0;
    float3 n = normalize(normalWS);
    float3 tilt;
    float depth = GalaxySoftTouchDepth(positionWS, n, uv, tilt);
    half ao = 0;
    int count = min((int)_SoftContactCount, 32);
    [loop] for (int i = 0; i < count; i++)
    {
        if (_SoftContactB[i].w <= 0) continue;
        float3 a = _SoftContactA[i].xyz;
        float3 b = _SoftContactB[i].xyz;
        float r = max(_SoftContactA[i].w, 1e-4);
        if (r > 1.0) continue; // 床などの大きな球は影を落とさない
        float3 ab = b - a;
        float t = saturate(dot(positionWS - a, ab) / max(dot(ab, ab), 1e-8));
        float3 d = a + ab * t - positionWS;
        float len = length(d);
        float facing = saturate(dot(d / max(len, 1e-5), n));
        float near = saturate(1.0 - (len - r) / (r * 2.0));
        ao = max(ao, (half)(near * near * facing * _SoftContactB[i].w));
    }

    half pressure = (half)saturate(depth / max(_SoftDepth, 1e-4));
    return half2(ao, pressure);
}

/// ふくらむ: 法線の向きへ押し出す。量 × マスク(R) × 球の重み(中心で 1、半径で 0。丸さで形を変える)
float3 GalaxyInflate(float3 positionWS, float3 normalWS, float2 uv)
{
    if (_Inflate < 0.5h || abs(_InflateAmount) < 1e-5h) return positionWS;
    half mask = SAMPLE_TEXTURE2D_LOD(_InflateMask, sampler_linear_repeat, uv, 0).r;
    half weight = mask;
    if (_InflateCenter.w > 1e-5)
    {
        half r = (half)saturate(length(positionWS - _InflateCenter.xyz) / _InflateCenter.w);
        // 丸さ 1: 半球のようになだらか(√(1−r²))、0: 縁まで平らに上がる
        half dome = sqrt(saturate(1.0h - r * r));
        half flat = 1.0h - smoothstep(0.85h, 1.0h, r);
        weight *= lerp(flat, dome, _InflateRound);
    }
    return positionWS + normalize(normalWS) * (_InflateAmount * weight);
}

/// 脚に押される布: カプセル(線分＋半径)の中の点を、線分からの向きへ半径の所まで出す
float3 GalaxyLegCapsulePush(float3 positionWS, float4 start, float4 end)
{
    if (start.w <= 1e-4) return positionWS;
    float3 axis = end.xyz - start.xyz;
    float t = saturate(dot(positionWS - start.xyz, axis) / max(dot(axis, axis), 1e-6));
    float3 closest = start.xyz + axis * t;
    float3 away = positionWS - closest;
    float distance = length(away);
    if (distance >= start.w || distance < 1e-5) return positionWS;
    return closest + away / distance * start.w;
}

float3 GalaxyLegPush(float3 positionWS)
{
    if (_LegPush < 0.5h) return positionWS;
    positionWS = GalaxyLegCapsulePush(positionWS, _GalaxyLegStart0, _GalaxyLegEnd0);
    return GalaxyLegCapsulePush(positionWS, _GalaxyLegStart1, _GalaxyLegEnd1);
}

float3 GalaxySoftTouch(float3 positionWS, float3 normalWS, float2 uv)
{
    // ふくらむのは、触れるとへこむより先(へこみはふくらんだ面から測る)
    positionWS = GalaxyInflate(positionWS, normalWS, uv);
    positionWS = GalaxyLegPush(positionWS);
    if (_SoftTouch < 0.5h || _SoftContactCount < 0.5) return positionWS;
    float3 n = normalize(normalWS);
    float3 tilt;
    float depth = GalaxySoftTouchDepth(positionWS, n, uv, tilt);
    return positionWS - n * depth;
}

/// へこみの壁の法線(Forward で使う)。へこみの中心の方へ倒す
float3 GalaxySoftTouchNormal(float3 positionWS, float3 normalWS, float2 uv)
{
    if (_SoftTouch < 0.5h || _SoftContactCount < 0.5) return normalWS;
    float3 n = normalize(normalWS);
    float3 tilt;
    GalaxySoftTouchDepth(positionWS, n, uv, tilt);
    return normalize(n + tilt * 0.8);
}

/// 濡れて縮む: 布が水を吸って張りを失い、ふくらみが減って体に沿う。頂点を法線の内側へ(濡れ量 × cm)
float3 GalaxyWetShrink(float3 positionWS, float3 normalWS)
{
    if (_WetShrink <= 0.0h) return positionWS;
    return positionWS - normalize(normalWS) * (_WetShrink * 0.01 * saturate(_WetAmount * _WetReceive));
}

/// 髪の揺れ: 支点(頭の上)から下へ離れた頂点ほど、遅れぶん大きくずらす(根元は動かない。二乗で毛先ほど大きく)
float3 GalaxyHairSway(float3 positionWS)
{
    if (_HairSway <= 0.0h || _HairSwayPivot.w <= 0.0) return positionWS;
    float below = dot(_HairSwayPivot.xyz - positionWS, normalize(_HairSwayUp.xyz));
    float w = saturate(below / _HairSwayPivot.w);
    return positionWS + _HairSwayOffset.xyz * (w * w * _HairSway);
}

/// 平面化の 1 範囲: 中心を通り、カメラへ向いた面へ奥行きを縮める(中心から離れるほど弱く)。
/// 範囲の中ではカメラの遠近がほぼ消えて正投影に近くなり、2D の絵のような平らな顔・胸・体に見える。法線は変えない(陰影はそのまま)
float3 GalaxyFlattenOne(float3 positionWS, float4 region, half amount)
{
    if (amount <= 0.0h || region.w <= 0.0) return positionWS;
    float3 toCenter = _FlattenCamera.w > 1.5 ? _FlattenCamera.xyz : region.xyz - _FlattenCamera.xyz;
    float len = length(toCenter);
    if (len < 1e-5) return positionWS;
    float3 viewAxis = toCenter / len;
    float3 offset = positionWS - region.xyz;
    float r = length(offset) / region.w;
    half w = 1.0h - smoothstep(0.5h, 1.0h, (half)r); // 半径の半分まではいっぱい、半径でなくなる
    // 強さの上限 0.95: 奥と手前の順が入れ替わらないように(髪の後ろが顔の前へ出ない)
    return positionWS - viewAxis * (dot(offset, viewAxis) * min(amount, 0.95h) * w);
}

/// 平面化(奥行きを縮めて平らに見せる)。3 つの範囲(顔・胸・体など)を順に掛ける
float3 GalaxyFlatten(float3 positionWS)
{
    if (_FlattenCamera.w < 0.5) return positionWS;
    positionWS = GalaxyFlattenOne(positionWS, _Flatten0, (half)_FlattenAmount.x);
    positionWS = GalaxyFlattenOne(positionWS, _Flatten1, (half)_FlattenAmount.y);
    positionWS = GalaxyFlattenOne(positionWS, _Flatten2, (half)_FlattenAmount.z);
    return positionWS;
}

/// 髪の揺れのあとに平面化(全パスで同じ順に掛ける。影・深度も平らにした形で作る)
float3 GalaxyHairSwayFlatten(float3 positionWS)
{
    return GalaxyFlatten(GalaxyHairSway(positionWS));
}

/// 前のフレームの髪の揺れ(動きのベクトル用)。前のフレームの値が入っていなければ、いまの値
float3 GalaxyHairSwayPrev(float3 positionWS)
{
    if (_HairSwayOffsetPrev.w < 0.5) return GalaxyHairSway(positionWS);
    if (_HairSway <= 0.0h || _HairSwayPivotPrev.w <= 0.0) return positionWS;
    float below = dot(_HairSwayPivotPrev.xyz - positionWS, normalize(_HairSwayUp.xyz));
    float w = saturate(below / _HairSwayPivotPrev.w);
    return positionWS + _HairSwayOffsetPrev.xyz * (w * w * _HairSway);
}

// ---- 色調補正(色相・彩度・明るさ・ガンマ)。テクスチャを描き直さずに色味を変える
half3 GalaxyRgbToHsv(half3 c)
{
    half4 K = half4(0.0h, -1.0h / 3.0h, 2.0h / 3.0h, -1.0h);
    half4 p = c.g < c.b ? half4(c.bg, K.wz) : half4(c.gb, K.xy);
    half4 q = c.r < p.x ? half4(p.xyw, c.r) : half4(c.r, p.yzx);
    half d = q.x - min(q.w, q.y);
    half e = 1e-5h;
    return half3(abs(q.z + (q.w - q.y) / (6.0h * d + e)), d / (q.x + e), q.x);
}

half3 GalaxyHsvToRgb(half3 c)
{
    half3 p = abs(frac(c.xxx + half3(0.0h, 2.0h / 3.0h, 1.0h / 3.0h)) * 6.0h - 3.0h);
    return c.z * lerp(half3(1, 1, 1), saturate(p - 1.0h), c.y);
}

// アニメ塗りの影色: 地の色の色相を少しずらし、鮮やかにして、暗くする
half3 GalaxyAnimeShadowColor(half3 c)
{
    half3 hsv = GalaxyRgbToHsv(c);
    hsv.x = frac(hsv.x + _AnimeShadowHue);
    hsv.y = saturate(hsv.y + (1.0h - hsv.y) * _AnimeShadowSat * saturate(hsv.y * 2.0h)); // 色のある所ほど強く(白・灰は濁らせない)
    hsv.z *= _AnimeShadowValue;
    return GalaxyHsvToRgb(hsv);
}

half3 GalaxyColorAdjust(half3 color)
{
    half3 hsv = GalaxyRgbToHsv(color);
    hsv.x = frac(hsv.x + _HueShift);
    hsv.y = saturate(hsv.y * _Saturation);
    hsv.z = hsv.z * _Brightness;
    color = GalaxyHsvToRgb(hsv);
    return pow(max(color, 1e-4h), _Gamma);
}

// ---- ノイズ(ディゾルブ・濡れ髪の束で使う)。0〜1 の乱数と、それをなめらかにつないだ値ノイズ
float GalaxyHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float GalaxyValueNoise2(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = GalaxyHash21(i), b = GalaxyHash21(i + float2(1, 0)), c = GalaxyHash21(i + float2(0, 1)), d = GalaxyHash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// ---- ディゾルブ(消えていく演出)。値 < 量 の所を消し、境目を光らせる。全パス(影・深度・輪郭線)で同じ所を消す
/// 消す判定をして(clip)、境目の強さ(0〜1)を返す。位置方式はワールドの向きで、物体の原点から「開始位置」〜「開始位置+範囲」の間で消えていく
half GalaxyDissolve(float2 uv, float3 positionOS)
{
    #if defined(_DISSOLVE)
        float noise = SAMPLE_TEXTURE2D(_DissolveNoise, sampler_linear_repeat, uv * _DissolveNoiseScale).r;
        float procedural = GalaxyValueNoise2(uv * _DissolveNoiseScale * 8.0) * 0.6 + GalaxyValueNoise2(uv * _DissolveNoiseScale * 23.0) * 0.4;
        // テクスチャが入っていればそれを使う(既定の白では使わない)。「ノイズの強さ」1.5 超えでテクスチャを使う古いマテリアルもそのまま
        noise = lerp(procedural, noise, _DissolveNoiseTexOn > 0.5h || _DissolveNoiseAmount > 1.5h ? 1.0h : 0.0h);
        float value;
        if (_DissolveMode < 0.5h) value = noise;
        else
        {
            // 位置: 向きに沿って 0→1。ノイズで境目をギザギザにする
            float3 fromOrigin = TransformObjectToWorld(positionOS) - (_DissolveWorld > 0.5h ? 0 : UNITY_MATRIX_M._m03_m13_m23);
            float along = (dot(fromOrigin, normalize(_DissolveDirection.xyz)) - _DissolveStart) / max(_DissolveDirection.w, 1e-3);
            value = saturate(along + (noise - 0.5) * _DissolveNoiseAmount);
        }
        if (_DissolveInvert > 0.5h) value = 1.0 - value;                  // 逆: 先に消える所から先に現れる
        float amount = _DissolveAmount * (1.0 + _DissolveEdgeWidth); // 量 1 で境目ごと消える
        clip(value - amount + 1e-4);
        return saturate(1.0 - (value - amount) / max(_DissolveEdgeWidth, 1e-4)) * step(0.001h, _DissolveAmount);
    #else
        return 0;
    #endif
}

// トゥーンの境界: border を中心に blur の幅でぼかし、blur が 0 でも fwidth で1画素ぶんアンチエイリアスする。
half GalaxyTooning(half value, half edge, half soft)
{
    // 境界 edge を中心に、幅 width の間で 0(影)→1(光)へまっすぐ上がる。幅 = ぼかし + この画素で value が変わる量(ジャギー消し)
    half width = max(soft + fwidth(value) * _EdgeSmooth, 1e-4h);
    return saturate((value - edge) / width + 0.5h);
}

// ループの中用(fwidth はループ内で使えない): ぼかし幅だけで切る
half GalaxyTooningNoAA(half value, half edge, half soft)
{
    return saturate((value - edge) / max(soft, 0.02h) + 0.5h);
}

#endif
