// GalaxyShader/Toon の補助パス: 輪郭線(Outline)・影を落とす(ShadowCaster)・深度(DepthOnly)・深度と法線(DepthNormals)。
#ifndef GALAXY_TOON_PASSES_INCLUDED
#define GALAXY_TOON_PASSES_INCLUDED

#include "GalaxyToonInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct GalaxyToonSimpleAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID // GPU インスタンシング(RenderMeshInstanced)用
};

struct GalaxyToonSimpleVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    half fogFactor : TEXCOORD2;
    float3 positionOS : TEXCOORD3; // ディゾルブ(位置)用
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// ---------------------------------------------------------------- 輪郭線
// 裏面を法線方向へ押し出して描く。太さは cm(ワールド)。カメラが1mより近いと細くする(_OutlineNearClamp)。
// なめらか法線: GalaxyToonOutlineNormalBaker が同じ位置の頂点の法線を平均して UV8(接空間)に焼いたもの。角で線が割れない

struct GalaxyToonOutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float3 smoothNormalTS : TEXCOORD7;
    half4 color : COLOR;   // 頂点カラー(R=線の太さ。メッシュに無ければ白)
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

GalaxyToonSimpleVaryings GalaxyToonOutlineVert(GalaxyToonOutlineAttributes input)
{
    GalaxyToonSimpleVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = GalaxyHairSwayFlatten(GalaxyWetShrink(GalaxySoftTouch(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS), input.uv), TransformObjectToWorldNormal(input.normalOS)));
    float3 normalOS = input.normalOS;
    if (_OutlineSmoothNormal > 0.5 && dot(input.smoothNormalTS, input.smoothNormalTS) > 0.25)
    {
        float3 bitangentOS = cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w >= 0 ? 1.0 : -1.0);
        normalOS = normalize(input.tangentOS.xyz * input.smoothNormalTS.x + bitangentOS * input.smoothNormalTS.y + input.normalOS * input.smoothNormalTS.z);
    }
    float3 normalWS = TransformObjectToWorldNormal(normalOS);
    float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);
    float width = _OutlineWidth * 0.01 * SAMPLE_TEXTURE2D_LOD(_OutlineThicknessMap, sampler_linear_repeat, uv, 0).r;
    width *= lerp(1.0, input.color.r, _OutlineVertexColor); // 頂点カラー R: 髪の内側など、線を消したい所を黒く塗る
    width *= lerp(1.0, input.color.a, _OutlineVertexAlpha); // 頂点カラー A でも太さを決められる(R を影に使うとき)
    // 光の側を太く: 主光源に向いた面ほど線を太くする(光の当たる輪郭が強く見える)
    if (_OutlineLightWidth > 0.0) width *= 1.0 + _OutlineLightWidth * saturate(dot(normalWS, GetMainLight().direction));
    width *= 1.0 + GalaxyAudioLevel() * _AudioOutline; // 音に合わせて太く
    width *= GalaxyVolumeOutline(); // Volume でまとめて太さを変える
    float cameraDistance = distance(GetCameraPositionWS(), positionWS);
    width *= lerp(1.0, saturate(cameraDistance), _OutlineNearClamp);
    width *= lerp(1.0, max(cameraDistance, 1.0), _AnimeOutlinePixel); // 1m より遠いほど太くして、画面上の線の太さを保つ
    // 手描きの線のゆらぎ: 物体の位置(5cm ごと)とコマ番号から決まる乱数で、太さを ±ゆらぎ だけ変える
    if (_OutlineBoil > 0.0)
    {
        float frame = floor(_Time.y * max(_OutlineBoilFps, 1.0));
        float3 cell = floor(input.positionOS.xyz * 20.0);
        float noise = frac(sin(dot(cell + frame * float3(1.7, 9.2, 3.1), float3(12.9898, 78.233, 37.719))) * 43758.5453);
        width *= 1.0 + _OutlineBoil * (noise * 2.0 - 1.0) * 0.6;
    }
    // 画角: 30° を基準に、望遠(画角が狭い)ほど細く・広角ほど太くして、画面上の線の太さを保つ(平行投影ではしない)
    if (_OutlineFovKeep > 0.0 && unity_OrthoParams.w < 0.5)
    {
        float halfTan = 1.0 / max(UNITY_MATRIX_P._m11, 1e-4);
        width *= lerp(1.0, clamp(halfTan / 0.2679, 0.3, 3.0), _OutlineFovKeep);
    }
    // 遠いと細くして消す(遠くのキャラの線がつぶれて黒くならない)
    if (_OutlineFadeEnd > _OutlineFadeStart) width *= 1.0 - smoothstep(_OutlineFadeStart, _OutlineFadeEnd, cameraDistance);
    if (_OutlineCenterBoost > 0.0)
    {
        // 画面の中心ほど太く: 見せたい真ん中の線を強く、端は細く（画面の中心からの距離。縦横比は合わせる）
        float4 centerCS = TransformWorldToHClip(positionWS);
        float2 ndc = centerCS.xy / max(abs(centerCS.w), 1e-4);
        ndc.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
        width *= 1.0 + _OutlineCenterBoost * (1.0 - smoothstep(0.0, max(_OutlineCenterRadius, 0.01), length(ndc)));
    }
    positionWS += normalWS * width;
    if (_OutlineVertexDepth > 0.0)
    {
        // 頂点カラー B で輪郭線を奥へ(顔の内側の線を、白い所だけ手前の面の裏へ隠す)
        positionWS -= SafeNormalize(GetCameraPositionWS() - positionWS) * (_OutlineVertexDepth * 0.01 * input.color.b);
    }
    if (_OutlineZOffset > 0.0)
    {
        // カメラから離れる方向へわずかに押し下げる: 鼻筋や口元・襟元の内側の線が手前の面に隠れてすっきり消える
        float3 toCamDir = SafeNormalize(GetCameraPositionWS() - positionWS);
        positionWS -= toCamDir * (_OutlineZOffset * 0.01);
    }
    output.positionCS = GalaxyClipAdjust(TransformWorldToHClip(positionWS), positionWS, input.uv);
    if (_OutlineWidth <= 0.0) output.positionCS = float4(0, 0, 0, 0); // 輪郭線なし: 全頂点を1点に潰して描かない
    output.uv = uv;
    output.normalWS = normalWS;
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    output.positionOS = input.positionOS.xyz;
    return output;
}

half4 GalaxyToonOutlineFrag(GalaxyToonSimpleVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    clip(0.5 - _GalaxyToonOff); // 「シェーダー OFF」中は輪郭線を描かない
    GalaxyNearFade(TransformObjectToWorld(input.positionOS), input.positionCS);
    half4 albedo = GalaxyToonAlbedo(input.uv);
    GalaxyToonClip(albedo.a);
    half outlineAlpha = 1;
    GalaxyBangFadeAlpha(TransformObjectToWorld(input.positionOS), outlineAlpha); // 毛先側は透け具合に合わせて薄く
    GalaxyDissolve(input.uv, input.positionOS);
    half3 baseTint = lerp(half3(1, 1, 1), albedo.rgb, _OutlineTexBlend);
    half3 color = _OutlineColor.rgb * baseTint * GalaxyIlmLut(input.uv, 2).rgb; // 素材 ID ごとの輪郭線の色
    if (_OutlineColorTrace > 0.001h)
    {
        // 色トレス: 地の色の彩度を上げて少し暗くし、色相をわずかに暖色/深み側へ寄せたイラスト風の輪郭線色を作る
        half3 hsv = GalaxyRgbToHsv(albedo.rgb);
        hsv.y = saturate(hsv.y * 1.45h + 0.15h * step(0.05h, hsv.y));
        hsv.z *= 0.55h;
        half3 traced = GalaxyHsvToRgb(hsv);
        color = lerp(color, traced, _OutlineColorTrace);
    }
    // 明るさは主光源の色に合わせる(暗い場面で線だけ浮かないように)
    float3 toonDir = GalaxyToonLightDir();
    half3 lightColor = _LightSource < 0.5h && dot(toonDir, toonDir) > 1e-4 ? GalaxyToonLightColor() : _MainLightColor.rgb;
    lightColor = clamp(lightColor + SampleSH(half3(0, 0, 0)) * _AmbientStrength, _LightFloor, _LightCeiling);
    color *= lerp(half3(1, 1, 1), lightColor, _OutlineLit);
    color = MixFog(color, input.fogFactor);
    return half4(color, outlineAlpha);
}

// ---------------------------------------------------------------- 影を落とす(URP の ShadowCasterPass と同じ手順)

float3 _LightDirection;
float3 _LightPosition;

GalaxyToonSimpleVaryings GalaxyToonShadowVert(GalaxyToonSimpleAttributes input)
{
    GalaxyToonSimpleVaryings output = (GalaxyToonSimpleVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = GalaxyHairSwayFlatten(GalaxyWetShrink(GalaxySoftTouch(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS), input.uv), TransformObjectToWorldNormal(input.normalOS)));
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.positionOS = input.positionOS.xyz;
    return output;
}

half4 GalaxyToonShadowFrag(GalaxyToonSimpleVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    GalaxyToonClip(GalaxyToonAlbedo(input.uv).a);
    GalaxyBangSolidClip(TransformObjectToWorld(input.positionOS));
    GalaxyDissolve(input.uv, input.positionOS);
    // 全体のディザ透明度は影にも(消したキャラの影が残らない。カメラが近いと透けるのは影には掛けない)
    if (_DitherAlpha < 0.999)
    {
        static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        uint2 p = (uint2)input.positionCS.xy % 4u;
        clip(_DitherAlpha - (bayer[p.y * 4 + p.x] + 0.5) / 16.0);
    }
    return 0;
}

// ---------------------------------------------------------------- 深度 / 深度と法線

GalaxyToonSimpleVaryings GalaxyToonDepthVert(GalaxyToonSimpleAttributes input)
{
    GalaxyToonSimpleVaryings output = (GalaxyToonSimpleVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 depthPositionWS = GalaxyHairSwayFlatten(GalaxyWetShrink(GalaxySoftTouch(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS), input.uv), TransformObjectToWorldNormal(input.normalOS)));
    output.positionCS = GalaxyClipAdjust(TransformWorldToHClip(depthPositionWS), depthPositionWS, input.uv);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.positionOS = input.positionOS.xyz;
    return output;
}

half4 GalaxyToonDepthFrag(GalaxyToonSimpleVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    GalaxyNearFade(TransformObjectToWorld(input.positionOS), input.positionCS);
    GalaxyToonClip(GalaxyToonAlbedo(input.uv).a);
    GalaxyBangSolidClip(TransformObjectToWorld(input.positionOS));
    GalaxyDissolve(input.uv, input.positionOS);
    return input.positionCS.z;
}

half4 GalaxyToonDepthNormalsFrag(GalaxyToonSimpleVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    GalaxyNearFade(TransformObjectToWorld(input.positionOS), input.positionCS);
    GalaxyToonClip(GalaxyToonAlbedo(input.uv).a);
    // 前髪透過で抜いた部分も、ここでは抜かない: 髪の落ち影(顔が法線テクスチャの A と深度で読む)は前髪の形で落とす
    GalaxyDissolve(input.uv, input.positionOS);
    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
        float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
        return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
    #else
        return half4(normalWS, _HairShadowCast > 0.5h ? 1.0h : 0.0h); // A=髪の影を落とす物(顔が GalaxyHairShadow で読む)
    #endif
}

// ---------------------------------------------------------------- 動きのベクトル
// 描いた形と同じ形で動きを出す: 触れるとへこむ・濡れて縮む・髪の揺れと平面化・深度だけ手前へ・遠近の除去を、
// いまの位置と前のフレームの位置(スキニングの前の位置)の両方に掛ける。髪の揺れは、前のフレームには前のフレームの揺れを掛ける(揺れそのものの動きも入る)
#if defined(GALAXY_MOTION_VECTORS)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

struct GalaxyMotionAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float3 positionOld : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GalaxyMotionVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 currentCS : TEXCOORD1;
    float4 previousCS : TEXCOORD2;
    float3 positionOS : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float3 GalaxyMotionDeform(float3 positionWS, float3 normalWS, float2 uv)
{
    return GalaxyHairSwayFlatten(GalaxyWetShrink(GalaxySoftTouch(positionWS, normalWS, uv), normalWS));
}

float3 GalaxyMotionDeformPrev(float3 positionWS, float3 normalWS, float2 uv)
{
    return GalaxyFlatten(GalaxyHairSwayPrev(GalaxyWetShrink(GalaxySoftTouch(positionWS, normalWS, uv), normalWS)));
}

/// 遠近の除去を、指定した行列で写した位置にも掛ける(GalaxyClipAdjust と同じ式。xy だけ)
float4 GalaxyMotionPerspective(float4 cs, float4x4 viewProj)
{
    if (_PerspectiveRemoval > 0.001h && _PerspectiveCenter.w > 0.5)
    {
        float centerW = mul(viewProj, float4(_PerspectiveCenter.xyz, 1.0)).w;
        if (centerW > 1e-4) cs.xy = lerp(cs.xy, cs.xy * (cs.w / centerW), _PerspectiveRemoval);
    }
    return cs;
}

GalaxyMotionVaryings GalaxyToonMotionVert(GalaxyMotionAttributes input)
{
    GalaxyMotionVaryings output = (GalaxyMotionVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float3 currentWS = GalaxyMotionDeform(TransformObjectToWorld(input.positionOS.xyz), normalWS, input.uv);
    output.positionCS = GalaxyClipAdjust(TransformWorldToHClip(currentWS), currentWS, input.uv);
    output.currentCS = GalaxyMotionPerspective(mul(_NonJitteredViewProjMatrix, float4(currentWS, 1.0)), _NonJitteredViewProjMatrix);
    float3 previousOS = unity_MotionVectorsParams.x == 1 ? input.positionOld : input.positionOS.xyz;
    float3 previousWS = GalaxyMotionDeformPrev(mul(UNITY_PREV_MATRIX_M, float4(previousOS, 1.0)).xyz, normalWS, input.uv);
    output.previousCS = GalaxyMotionPerspective(mul(_PrevViewProjMatrix, float4(previousWS, 1.0)), _PrevViewProjMatrix);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.positionOS = input.positionOS.xyz;
    return output;
}

float4 GalaxyToonMotionFrag(GalaxyMotionVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    GalaxyNearFade(TransformObjectToWorld(input.positionOS), input.positionCS);
    GalaxyToonClip(GalaxyToonAlbedo(input.uv).a);
    GalaxyBangSolidClip(TransformObjectToWorld(input.positionOS));
    GalaxyDissolve(input.uv, input.positionOS);
    return float4(CalcNdcMotionVectorFromCsPositions(input.currentCS, input.previousCS), 0, 0);
}
#endif

// ---------------------------------------------------------------- 物の番号
// 画面の線の「物ごとの線」が、となりの画素と番号が違う所に線を引く。描いた形と同じ形(へこみ・揺れ・平面化)で書く
#if defined(GALAXY_OBJECT_ID)
struct GalaxyObjectIdAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GalaxyObjectIdVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionOS : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

GalaxyObjectIdVaryings GalaxyToonObjectIdVert(GalaxyObjectIdAttributes input)
{
    GalaxyObjectIdVaryings output = (GalaxyObjectIdVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float3 positionWS = GalaxyHairSwayFlatten(GalaxyWetShrink(GalaxySoftTouch(TransformObjectToWorld(input.positionOS.xyz), normalWS, input.uv), normalWS));
    output.positionCS = GalaxyClipAdjust(TransformWorldToHClip(positionWS), positionWS, input.uv);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.positionOS = input.positionOS.xyz;
    return output;
}

half4 GalaxyToonObjectIdFrag(GalaxyObjectIdVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    // 深度のパスと同じ所を消す(消えた所に画面の線が出ない)
    GalaxyNearFade(TransformObjectToWorld(input.positionOS), input.positionCS);
    GalaxyToonClip(GalaxyToonAlbedo(input.uv).a);
    GalaxyBangSolidClip(TransformObjectToWorld(input.positionOS));
    GalaxyDissolve(input.uv, input.positionOS);
    // 番号を色にする(0 なら地の色から。同じ色の物は同じ番号になる)
    float id = _ObjectId > 0.5 ? _ObjectId : dot(_BaseColor.rgb, float3(255.0, 65025.0, 16581375.0)) + 1.0;
    float3 h = frac(sin(float3(id * 12.9898, id * 78.233, id * 37.719)) * 43758.5453);
    return half4(h, 1.0);
}
#endif

#endif
