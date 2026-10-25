// GalaxyShader/Toon の共通部分(マテリアルの値・テクスチャ・トゥーンの計算)。全パスで同じ CBUFFER を使い、SRP Batcher に載せる。
#ifndef GALAXY_TOON_INPUT_INCLUDED
#define GALAXY_TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
SAMPLER(sampler_linear_repeat); // マスク類はこれで引く(_BaseMap を使わないパスでも使える Unity のインラインサンプラー)
TEXTURE2D(_ShadowStrengthMask);
TEXTURE2D(_MatCapTex);
SAMPLER(sampler_linear_clamp);  // マットキャップ用(球の縁で繰り返さない)
SAMPLER(sampler_point_clamp);   // 髪の影(法線テクスチャの印をぼかさずに読む)
TEXTURE2D(_EmissionMap);
TEXTURE2D(_Emission2Map);
TEXTURE2D(_OutlineWidthMask);
TEXTURE2D(_ShadowRampTex);      SAMPLER(sampler_ShadowRampTex);
TEXTURE2D(_RampIdMap);
TEXTURE2D(_SpecularRampTex);
TEXTURE2D(_FaceSdfTex);
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

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    half _BumpScale;
    half _OcclusionStrength;
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
    half _TriplanarScale;
    half _TriplanarBlend;

    half _LightSource;
    half _LightMinLimit;
    half _LightMaxLimit;
    half _MonochromeLighting;
    half _AmbientStrength;
    half _AdditionalLightStrength;
    half _BakedSteps;

    half4 _ShadowColor;
    half _ShadowBorder;
    half _ShadowBlur;
    half4 _Shadow2ndColor;
    half _Shadow2ndBorder;
    half _Shadow2ndBlur;
    half4 _Shadow3rdColor;
    half _Shadow3rdBorder;
    half _Shadow3rdBlur;
    half _ShadowReceive;
    half _AAStrength;
    half _FaceShading;
    float4 _FaceForward; // GalaxyToonFace が頭の向きを入れる(xyz)。0 ならモデルの正面(+Z)
    float4 _FaceRight;   // 同じく頭の右(xyz)。0 ならモデルの右(+X)
    float4 _FaceCenter;  // 頭の中心(xyz)、w=1 で有効
    half _FaceSphere;
    half _HairSway;
    float4 _HairSwayOffset; // GalaxyToonHairSway が入れる: 毛先の遅れ(ワールド, m)
    float4 _HairSwayPivot;  // 揺れの支点(頭の上, ワールド) w=毛先までの長さ(m)
    float4 _HairSwayUp;     // 頭の上向き(ワールド)

    half4 _RimColor;
    half _RimBorder;
    half _RimBlur;
    half _RimFresnelPower;
    half _RimShadowMask;
    half _RimDepthWidth;
    half _RimDepthThreshold;

    half4 _SpecularColor;
    half _SpecularSize;
    half _SpecularBlur;

    half4 _MatCapColor;
    half _MatCapShadowMask;
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

    half4 _BacklightColor;
    half _BacklightBorder;
    half _BacklightBlur;
    half _BacklightDirectivity;
    half _BacklightViewStrength;
    half _BacklightMainStrength;

    half _DissolveAmount;
    half _DissolveMode;
    float4 _DissolveDirection; // xyz=消えていく向き(ワールド) w=範囲(m)
    half _DissolveStart;
    half _DissolveNoiseScale;
    half _DissolveNoiseStrength;
    half _DissolveInvert;
    half _DissolveWorld;
    half _DissolveEdgeWidth;
    half4 _DissolveEdgeColor;

    half4 _EmissionColor;
    float4 _EmissionMap_ST;
    float4 _EmissionScroll;  // xy=スクロール z=回転° w=回転°/秒
    half _EmissionAnimate;
    half _EmissionBlink;
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
    half _OutlineFixWidth;
    half _OutlineTexBlend;
    half _OutlineLit;

    half _WetAmount;
    half _WetDarken;
    half _WetGloss;
    half _WetSoak;
    half _WetShrink;
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
    half _BlushAmount;
    half4 _BlushColor;
    float4 _BlushUV;
    half _BlushSize;
    half _BlushSoft;
    half _BlushLines;
    half4 _GlitterColor;
    half _GlitterScale;
    half _GlitterSize;
    half _GlitterDensity;
    half _GlitterSharpness;
    half _GlitterShadowMask;
    half _RefractionStrength;
    half _RefractionDispersion;
    half _UseNearFade;
    half _NearFadeStart;
    half _NearFadeEnd;
    half _OutlineSmoothNormal;
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

// 髪の影: GalaxyHairShadowFeature が「法線プリパスあり」のカメラで 1 にする(URP の深度・法線テクスチャを読む)
float _GalaxyHairShadowOn;

// ---- カメラが近いと透ける: 距離で 0→1、4x4 の網目(ディザ)で抜く。不透明のまま透けて見える
void GalaxyNearFade(float3 positionWS, float4 positionCS)
{
    #if defined(_NEAR_FADE)
        float d = distance(positionWS, GetCameraPositionWS());
        float fade = saturate((d - _NearFadeStart) / max(_NearFadeEnd - _NearFadeStart, 1e-3));
        static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        uint2 p = (uint2)positionCS.xy % 4u;
        clip(fade - (bayer[p.y * 4 + p.x] + 0.5) / 16.0);
    #endif
}

half4 GalaxyToonAlbedo(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
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
half GalaxyBangFade(float3 positionWS, out half region)
{
    region = 0;
    if (_BangFadeMode < 0.5h || _GalaxyBangCenter.w < 0.5) return 0;
    float3 d = positionWS - _GalaxyBangCenter.xyz;
    float h = dot(d, _GalaxyBangUp.xyz);
    float f = dot(d, _GalaxyBangForward.xyz);
    float side = length(d - _GalaxyBangUp.xyz * h - _GalaxyBangForward.xyz * f);
    region = (f > _GalaxyBangRange.w && side < _GalaxyBangRange.z && h < _GalaxyBangRegion.x) ? 1.0h : 0.0h;
    half t = smoothstep(0.0, 1.0, saturate((_GalaxyBangRange.x - h) / max(_GalaxyBangRange.x - _GalaxyBangRange.y, 1e-4)));
    half sideFade = saturate((_GalaxyBangRange.z - side) / 0.03); // 横へいくほど透けない(横髪との境目もなめらかに)
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
        noise = lerp(procedural, noise, _DissolveNoiseStrength > 1.5h ? 1.0h : 0.0h); // 「ノイズの強さ」が 1.5 を超えるときだけテクスチャを使う(既定の白では使わない)
        float value;
        if (_DissolveMode < 0.5h) value = noise;
        else
        {
            // 位置: 向きに沿って 0→1。ノイズで境目をギザギザにする
            float3 fromOrigin = TransformObjectToWorld(positionOS) - (_DissolveWorld > 0.5h ? 0 : UNITY_MATRIX_M._m03_m13_m23);
            float along = (dot(fromOrigin, normalize(_DissolveDirection.xyz)) - _DissolveStart) / max(_DissolveDirection.w, 1e-3);
            value = saturate(along + (noise - 0.5) * _DissolveNoiseStrength);
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
half GalaxyTooning(half value, half border, half blur)
{
    half borderMin = saturate(border - blur * 0.5h);
    half borderMax = saturate(border + blur * 0.5h);
    return saturate((value - borderMin) / max(saturate(borderMax - borderMin + fwidth(value) * _AAStrength), 1e-4h));
}

// ループの中用(fwidth はループ内で使えない): ぼかし幅だけで切る
half GalaxyTooningNoAA(half value, half border, half blur)
{
    half borderMin = saturate(border - blur * 0.5h);
    half borderMax = saturate(border + blur * 0.5h);
    return saturate((value - borderMin) / max(borderMax - borderMin, 0.02h));
}

#endif
