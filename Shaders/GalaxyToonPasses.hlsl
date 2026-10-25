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
// 裏面を法線方向へ押し出して描く。太さは cm(ワールド)。カメラが1mより近いと細くする(_OutlineFixWidth)。
// なめらか法線: GalaxyToonOutlineNormalBaker が同じ位置の頂点の法線を平均して UV8(接空間)に焼いたもの。角で線が割れない

struct GalaxyToonOutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float3 smoothNormalTS : TEXCOORD7;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

GalaxyToonSimpleVaryings GalaxyToonOutlineVert(GalaxyToonOutlineAttributes input)
{
    GalaxyToonSimpleVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = GalaxyHairSway(GalaxyWetShrink(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS)));
    float3 normalOS = input.normalOS;
    if (_OutlineSmoothNormal > 0.5 && dot(input.smoothNormalTS, input.smoothNormalTS) > 0.25)
    {
        float3 bitangentOS = cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w >= 0 ? 1.0 : -1.0);
        normalOS = normalize(input.tangentOS.xyz * input.smoothNormalTS.x + bitangentOS * input.smoothNormalTS.y + input.normalOS * input.smoothNormalTS.z);
    }
    float3 normalWS = TransformObjectToWorldNormal(normalOS);
    float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);
    float width = _OutlineWidth * 0.01 * SAMPLE_TEXTURE2D_LOD(_OutlineWidthMask, sampler_linear_repeat, uv, 0).r;
    float cameraDistance = distance(GetCameraPositionWS(), positionWS);
    width *= lerp(1.0, saturate(cameraDistance), _OutlineFixWidth);
    width *= lerp(1.0, max(cameraDistance, 1.0), _AnimeOutlinePixel); // 1m より遠いほど太くして、画面上の線の太さを保つ
    positionWS += normalWS * width;
    output.positionCS = TransformWorldToHClip(positionWS);
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
    half3 color = _OutlineColor.rgb * lerp(half3(1, 1, 1), albedo.rgb, _OutlineTexBlend);
    // 明るさは主光源の色に合わせる(暗い場面で線だけ浮かないように)
    half3 lightColor = _LightSource < 0.5h && dot(_AdvToonLightDir.xyz, _AdvToonLightDir.xyz) > 1e-4 ? _AdvToonLightColor.rgb : _MainLightColor.rgb;
    lightColor = clamp(lightColor + SampleSH(half3(0, 0, 0)) * _AmbientStrength, _LightMinLimit, _LightMaxLimit);
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
    float3 positionWS = GalaxyHairSway(GalaxyWetShrink(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS)));
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
    return 0;
}

// ---------------------------------------------------------------- 深度 / 深度と法線

GalaxyToonSimpleVaryings GalaxyToonDepthVert(GalaxyToonSimpleAttributes input)
{
    GalaxyToonSimpleVaryings output = (GalaxyToonSimpleVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionCS = TransformWorldToHClip(GalaxyHairSway(GalaxyWetShrink(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS))));
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

#endif
