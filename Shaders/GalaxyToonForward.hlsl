// GalaxyShader/Toon の本体(ForwardLit パス)。段影トゥーン。
//
// GalaxyToonFrag の処理順(各節の見出し「==== n. ====」と対応):
//   1. 地の色   : テクスチャ(瞳の奥行き・トライプラナー) → 切り抜き・前髪の透け・ディゾルブ → 色調補正・アニメの色面化・頬染め
//   2. 法線     : ノーマルマップ 1・2 枚目 → 顔(頭を球とみなす・正面へ寄せる)
//   3. 表面効果 : 濡れ・汗・液体・泡・雨・汚れ・傷(法線と地の色を変える) → レイヤーテクスチャ
//                 (「シェーダー OFF」のときはここで URP 標準の PBR 計算をして終わる)
//   4. 光       : 主光源 + 環境光(焼いた光) → 明るさの上限・下限で挟む
//   5. 影       : N・L(または SDF 顔影) × 落ち影 × 髪の影 × AO → 影1〜3 / ランプ / アニメ影色
//   6. 足す光   : 金属 → 追加ライト → ハイライト・天使の輪・逆光・つや・映り込み・ラメ・濡れのつや → リム → マットキャップ
//   7. 仕上げ   : 発光・光るレイヤー・瞳 → ディゾルブの境目 → フォグ → 屈折
//
// 機能はほぼすべてキーワード(_SPECULAR など)で切り替える。OFF の機能は #if で消えるので計算もビルドもされない。
#ifndef GALAXY_TOON_FORWARD_INCLUDED
#define GALAXY_TOON_FORWARD_INCLUDED

#include "GalaxyToonInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(_DEPTH_RIM) && !defined(_HAIR_SHADOW)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl" // _HAIR_SHADOW のときは GalaxyToonExtra.hlsl が読む
#endif
#if defined(_REFRACTION)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#endif
#include "GalaxyToonFx.hlsl"    // 濡れ・汗・液体・泡・雨・汚れ・傷(ハッシュ関数もここ)
#include "GalaxyToonExtra.hlsl" // 髪の影・頬染め・瞳・ラメ(Fx のハッシュ関数を使う)
#include "GalaxyToonLayer.hlsl" // レイヤーテクスチャ・発光

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID // GPU インスタンシング(RenderMeshInstanced)用
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;         // _BaseMap のタイリング・オフセット済み
    float3 positionWS : TEXCOORD1; // 髪の揺れ・濡れの縮みを反映した位置
    float3 normalWS : TEXCOORD2;
    half fogFactor : TEXCOORD3;
    float3 positionOS : TEXCOORD4; // 液体の筋・汚れ・ディゾルブ(物体空間のノイズ)用
    float4 tangentWS : TEXCOORD5;  // ノーマルマップ用(w=従法線の向き)
    #if defined(LIGHTMAP_ON)
        float2 lightmapUV : TEXCOORD6;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings GalaxyToonVert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    // 頂点を動かす処理(濡れて縮む → 髪の揺れ)。影・深度・輪郭線のパスも同じ順で動かすので、形がずれない
    float3 positionWS = GalaxyHairSway(GalaxyWetShrink(TransformObjectToWorld(input.positionOS.xyz), normalWS));
    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionWS = positionWS;
    output.normalWS = normalWS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    output.positionOS = input.positionOS.xyz;
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    #if defined(LIGHTMAP_ON)
        output.lightmapUV = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    #endif
    return output;
}

// ================================================================ 光

/// ライトの色を整える: 環境光を足し、モノクロ化して、明るさの下限・上限で挟む(白飛び・真っ暗を防ぐ)。
/// ambient は環境光(焼いた光があればそれ、無ければシーンの環境光)。主光源と一緒に挟むので、焼いたランプの光でも白飛びしない。
half3 GalaxyToonLightColor(half3 lightColor, half3 ambient)
{
    lightColor += ambient * _AmbientStrength;
    lightColor = lerp(lightColor, Luminance(lightColor).xxx, _MonochromeLighting);
    return clamp(lightColor, _LightMinLimit, _LightMaxLimit);
}

/// 主光源: キャラ専用ライト(GalaxyToonLight)が設定されていればそれ、無ければシーンのメインライト。
/// 落ち影(shadow)はどちらの場合もシーンのメインライトのシャドウマップから読む。
void GalaxyToonMainLight(float3 positionWS, out half3 direction, out half3 color, out half shadow)
{
    Light main = GetMainLight(TransformWorldToShadowCoord(positionWS), positionWS, half4(1, 1, 1, 1));
    shadow = main.shadowAttenuation;
    direction = main.direction;
    color = main.color;
    bool useToonLight = _LightSource < 0.5h && dot(_AdvToonLightDir.xyz, _AdvToonLightDir.xyz) > 1e-4;
    if (useToonLight)
    {
        direction = normalize(_AdvToonLightDir.xyz);
        color = _AdvToonLightColor.rgb;
    }
    if (_LightSource > 1.5h) color = 0; // 「焼いた光+影だけ」: メインライトの光は足さず、落ちる影だけ使う(焼き込んだ背景用)
}

/// 追加ライト1つぶんの明るさ。当たる/当たらないを影1の境界で切り(トゥーン)、明るさは N・L で落とす(全面ベタに明るくしない)
half3 GalaxyToonAddLightDiffuse(Light light, half3 normal)
{
    half ndl = dot(normal, light.direction);
    half lit = GalaxyTooningNoAA(ndl * 0.5h + 0.5h, _ShadowBorder, _ShadowBlur) * saturate(ndl);
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    #if defined(_ANIME)
        // アニメ塗り: 照明の明るさも段にする(なだらかな光だまりを作らない)
        atten = GalaxyTooningNoAA(atten, 0.25h, 0.08h) * 0.6h;
        lit = step(0.5h, lit);
    #endif
    return light.color * (atten * lit);
}

/// 追加ライト1つぶんの、濡れた面・しずくに出る小さく鋭いきらめき(照明で雨粒が光る)
half3 GalaxyToonAddLightGloss(Light light, half3 normal, half3 viewDir)
{
    half nh = saturate(dot(normal, normalize(light.direction + viewDir)));
    return light.color * (light.distanceAttenuation * light.shadowAttenuation * GalaxyTooningNoAA(nh, 0.985h, 0.03h));
}

/// 追加ライト(点光源・スポット)を全部回して、明るさ(diffuse)ときらめき(gloss)を1回のループで集める。
/// 使わない方は呼び出し側で捨てればコンパイラが計算ごと消す。
void GalaxyToonAdditionalLights(float3 positionWS, half3 normal, half3 viewDir, float4 positionCS, out half3 diffuse, out half3 gloss)
{
    diffuse = 0;
    gloss = 0;
    #if defined(_ADDITIONAL_LIGHTS)
        // LIGHT_LOOP_BEGIN(クラスターライティング)は inputData という名前の変数を読む
        InputData inputData = (InputData)0;
        inputData.positionWS = positionWS;
        inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
        half4 shadowMask = half4(1, 1, 1, 1);
        uint pixelLightCount = GetAdditionalLightsCount();
        #if USE_CLUSTER_LIGHT_LOOP
            // クラスター方式では、メイン以外の平行光源はライトループの外で別に回す(URP の決まり)
            [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
            {
                Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
                diffuse += GalaxyToonAddLightDiffuse(light, normal);
                gloss += GalaxyToonAddLightGloss(light, normal, viewDir);
            }
        #endif
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
            diffuse += GalaxyToonAddLightDiffuse(light, normal);
            gloss += GalaxyToonAddLightGloss(light, normal, viewDir);
        LIGHT_LOOP_END
    #endif
    diffuse *= _AdditionalLightStrength;
    gloss *= _AdditionalLightStrength;
}

// ================================================================ 地の色・法線

/// トライプラナー: UV の無い(または伸びる)建物・岩・コンクリートに、ワールドの3方向から模様を貼る
half3 GalaxyTriplanarAlbedo(float3 positionWS, float3 normalWS)
{
    float3 p = positionWS * _TriplanarScale;
    half3 w = pow(abs(normalize(normalWS)), _TriplanarBlend); // 面の向きに近い方向ほど強く。指数が大きいほど境目がくっきり
    w /= max(w.x + w.y + w.z, 1e-4h);
    return (SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy).rgb * w.x
          + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz).rgb * w.y
          + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy).rgb * w.z) * _BaseColor.rgb;
}

/// 立体感(ノーマルマップ): 凹凸の向きで陰影・ハイライト・リムが変わる。2枚目は細かい凹凸を重ねる
half3 GalaxyToonApplyNormalMaps(float2 uv, half3 normal, float3 tangent, float3 bitangent)
{
    half3 normalTS = half3(0, 0, 1);
    #if defined(_NORMALMAP)
        normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
    #endif
    #if defined(_NORMALMAP2)
        half3 normalTS2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Bump2Map, sampler_BumpMap, uv * _Bump2Map_ST.xy + _Bump2Map_ST.zw), _Bump2Scale);
        normalTS = normalize(half3(normalTS.xy + normalTS2.xy, normalTS.z * normalTS2.z)); // 2枚の傾きを足す(Whiteout 合成)
    #endif
    #if defined(_ANIME)
        normalTS = normalize(lerp(normalTS, half3(0, 0, 1), _AnimeFlatNormal)); // アニメ塗り: 細かい凹凸で影がザラつかないように
    #endif
    return normalize(mul(normalTS, half3x3(tangent, bitangent, normal)));
}

// ================================================================ 影

/// SDF 顔影: 光が顔の正面から何度横にあるか(0=正面 1=真後ろ)を、テクスチャのしきい値(R=光が右から / G=左から)と比べる。
/// 戻り値は N・L の代わりに使う「光の当たり具合」(0=影 1=光)
half GalaxyFaceSdfLambert(float2 uv, half3 lightDir, float3 faceForward)
{
    float3 faceRight = dot(_FaceRight.xyz, _FaceRight.xyz) > 1e-4 ? _FaceRight.xyz : mul((float3x3)UNITY_MATRIX_M, float3(1, 0, 0));
    float2 lightXZ = normalize(float2(dot(lightDir, normalize(faceRight)), dot(lightDir, faceForward)) + 1e-5); // 顔の水平面に写した光の向き
    half lightAngle = acos(clamp(lightXZ.y, -1.0, 1.0)) / PI;
    half2 sdf = SAMPLE_TEXTURE2D(_FaceSdfTex, sampler_linear_repeat, uv).rg;
    half threshold = lightXZ.x >= 0 ? sdf.r : sdf.g;
    return saturate((threshold - lightAngle) * 4.0h + 0.5h);
}

/// 影を塗る。lambert(0=影 1=光)を影1〜3の境界で切り、地の色に影色を重ねる。
/// lit1(影1の当たり具合 0=影 1=光)は後のハイライト・リム・マットキャップで「影の側では消す」に使う。
half3 GalaxyToonShade(half3 albedo, half lambert, half strength, float rampRowV, out half lit1)
{
    half blur1 = _ShadowBlur, blur2 = _Shadow2ndBlur;
    #if defined(_ANIME)
        // アニメ塗り: 影の境界をくっきり(ぼかしを細く)
        blur1 = lerp(blur1, 0.004h, _AnimeHardness);
        blur2 = lerp(blur2, 0.004h, _AnimeHardness);
    #endif
    lit1 = GalaxyTooning(lambert, _ShadowBorder, blur1);
    half lit2 = GalaxyTooning(lambert, _Shadow2ndBorder, blur2);
    half shadow1 = (1.0h - lit1) * _ShadowColor.a * strength;    // 影1の濃さ(0〜1)
    half shadow2 = (1.0h - lit2) * _Shadow2ndColor.a * strength; // 影2の濃さ(0〜1)
    half3 color = albedo;

    #if defined(_SHADOW_RAMP)
        // ランプ: 左(暗い)→右(明るい)のグラデーション画像で影の色を決める。縦に並べた段を部位ごとに使い分ける。濃さマスクで明るい側へ寄せる
        color *= SAMPLE_TEXTURE2D(_ShadowRampTex, sampler_linear_clamp, float2(lerp(1.0h, lambert, strength), rampRowV)).rgb;
    #elif defined(_ANIME)
        // アニメ塗り: 影色を地の色から作る(色相を少しずらし、鮮やかに、暗く)。灰色をかけたような濁った影にしない
        half3 animeShadow = GalaxyAnimeShadowColor(albedo) * _ShadowColor.rgb;
        color = lerp(color, animeShadow, shadow1);
        #if defined(_SHADOW2ND)
            color = lerp(color, GalaxyAnimeShadowColor(animeShadow) * _Shadow2ndColor.rgb, shadow2);
        #endif
        // 影の境界の色(肌の赤い境界線など): 境界のまわりの細い帯
        half edgeBand = saturate(1.0h - abs(lambert - _ShadowBorder) / max(_AnimeEdgeWidth, 1e-3h));
        color = lerp(color, albedo * _AnimeEdgeColor.rgb, edgeBand * _AnimeEdgeColor.a * strength * step(1e-3h, _AnimeEdgeWidth));
    #else
        // ふつうの段影: 地の色 × 影色
        color = lerp(color, albedo * _ShadowColor.rgb, shadow1);
        #if defined(_SHADOW2ND)
            color = lerp(color, albedo * _Shadow2ndColor.rgb, shadow2);
        #endif
    #endif

    #if defined(_SHADOW3RD) && !defined(_SHADOW_RAMP)
        half lit3 = GalaxyTooning(lambert, _Shadow3rdBorder, _Shadow3rdBlur);
        color = lerp(color, albedo * _Shadow3rdColor.rgb, (1.0h - lit3) * _Shadow3rdColor.a * strength);
    #endif
    return color;
}

// ================================================================ 見比べ用「シェーダー OFF」

/// 同じテクスチャ・法線・金属・つやで、URP 標準の PBR 計算をする(グローバル _GalaxyToonOff = 1 のとき)
half4 GalaxyToonLitFallback(Varyings input, half4 albedo, half3 normal, half3 viewDir)
{
    InputData litInput = (InputData)0;
    litInput.positionWS = input.positionWS;
    litInput.normalWS = normal;
    litInput.viewDirectionWS = viewDir;
    litInput.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    litInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    litInput.shadowMask = half4(1, 1, 1, 1);
    #if defined(LIGHTMAP_ON)
        litInput.bakedGI = SampleLightmap(input.lightmapUV, normal) * max(0.0, 1.0 + _GalaxyBakedLightOffset);
    #else
        litInput.bakedGI = SampleSH(normal);
    #endif
    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo.rgb;
    surface.alpha = albedo.a;
    surface.metallic = _Metallic;
    surface.smoothness = _Smoothness;
    surface.occlusion = 1;
    surface.normalTS = half3(0, 0, 1);
    surface.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_linear_repeat, input.uv * _EmissionMap_ST.xy + _EmissionMap_ST.zw).rgb * _EmissionColor.rgb;
    half3 lit = UniversalFragmentPBR(litInput, surface).rgb;
    // キャラ専用ライトで照らしているマテリアルは、そのライトもふつうのランバートで足す(シーンに太陽が無いと真っ暗になるので)
    if (_LightSource < 0.5h && dot(_AdvToonLightDir.xyz, _AdvToonLightDir.xyz) > 1e-4)
        lit += albedo.rgb * _AdvToonLightColor.rgb * saturate(dot(normal, normalize(_AdvToonLightDir.xyz))) * (1.0h - _Metallic);
    return half4(MixFog(lit, input.fogFactor), albedo.a);
}

// ================================================================ フラグメント本体

half4 GalaxyToonFrag(Varyings input, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input); // フラグメントでも物体の行列(TransformObjectToWorld)を使うので
    GalaxyNearFade(input.positionWS, input.positionCS); // カメラが近いと網目で抜く

    // 面の向き(裏面は法線を反転)・接空間・視線
    half3 normal = normalize(input.normalWS);
    normal = IS_FRONT_VFACE(isFront, normal, -normal);
    float3 tangent = SafeNormalize(input.tangentWS.xyz); // 接線の無いメッシュでも NaN(真っ黒)にしない
    float3 bitangent = cross(normal, tangent) * input.tangentWS.w;
    half3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

    // ==== 1. 地の色 ====
    #if defined(_EYE)
        GalaxyEyeData eye = GalaxyEyeSetup(input.uv, viewDir, tangent, bitangent, normal); // 瞳の中は奥行きでずらした UV で引く
        half4 albedo = GalaxyToonAlbedo(eye.uv);
    #else
        half4 albedo = GalaxyToonAlbedo(input.uv);
    #endif
    #if defined(_TRIPLANAR)
        albedo.rgb = GalaxyTriplanarAlbedo(input.positionWS, input.normalWS); // アルファは UV で引いたものを使う
    #endif
    GalaxyToonClip(albedo.a);
    GalaxyBangFadeAlpha(input.positionWS, albedo.a);
    half dissolveEdge = GalaxyDissolve(input.uv, input.positionOS); // 消す所はここで捨て、境目の光る強さを返す
    #if defined(_COLORADJUST)
        albedo.rgb = GalaxyColorAdjust(albedo.rgb);
    #endif
    #if defined(_ANIME) && !defined(_TRIPLANAR) && !defined(_EYE)
        // アニメ塗り: テクスチャの細かい模様(汚れ・ムラ)を、ぼかした色(低いミップ)へ寄せてベタの色面にする
        if (_AnimeFlatTexture > 0.001h)
        {
            half3 flatColor = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, input.uv, _AnimeFlatMip).rgb * _BaseColor.rgb;
            #if defined(_COLORADJUST)
                flatColor = GalaxyColorAdjust(flatColor);
            #endif
            albedo.rgb = lerp(albedo.rgb, flatColor, _AnimeFlatTexture);
        }
    #endif
    if (_BlushAmount > 0.001h) albedo.rgb = GalaxyBlush(albedo.rgb, input.uv); // 頬染め(照れ)

    // ==== 2. 法線 ====
    #if defined(_NORMALMAP) || defined(_NORMALMAP2)
        normal = GalaxyToonApplyNormalMaps(input.uv, normal, tangent, bitangent);
    #endif
    // 顔の正面: GalaxyToonFace が入れた頭の向き。無ければモデルの +Z(SDF 顔影でも使う)
    float3 faceForward = dot(_FaceForward.xyz, _FaceForward.xyz) > 1e-4 ? _FaceForward.xyz : mul((float3x3)UNITY_MATRIX_M, float3(0, 0, 1));
    faceForward = normalize(faceForward);
    #if defined(_FACE)
        // 顔: 頭を球とみなした法線へ寄せ(なめらかな丸い陰影)、さらに顔の正面へ寄せる(鼻・頬の細かい影を消す)
        if (_FaceCenter.w > 0.5 && _FaceSphere > 0.0h)
            normal = normalize(lerp(normal, (half3)normalize(input.positionWS - _FaceCenter.xyz), _FaceSphere));
        normal = normalize(lerp(normal, (half3)faceForward, _FaceShading));
    #endif

    // ==== 3. 表面効果(濡れ・汗・液体・泡・雨・汚れ・傷): 法線をずらし、色を変え、つやを足す ====
    half hairWet = 0.0h, hairClump = 1.0h; // 濡れ髪: 濡れの強さ / 束の山=1 溝=0(天使の輪で使う)
    #if defined(_GALAXY_FX)
        GalaxyToonFxResult fx = GalaxyToonFx(input.uv, input.positionWS, input.positionOS, normal, tangent, bitangent);
        normal = fx.normal;
        albedo.rgb = lerp(albedo.rgb, _LiquidColor.rgb, fx.liquid * _LiquidColor.a);
        // 濡れ: 水を吸うとテクスチャ自身の色が掛かる(自分の色で乗算 = 濃く深く。汚れではない)。
        // 多孔質(染み込み > 0)は染みの所だけが濃くなり、濡れが進むと染みが広がって全面に
        half wetMix = max(lerp(fx.wet, max(fx.soak, fx.wet * 0.25h), _WetSoak), fx.liquid * 0.5h);
        // 濃さ: 0.5 で自分の色の二乗、1 で三乗(テクスチャの色が暗いほど強く沈む)
        half3 wetColor = albedo.rgb * pow(max(albedo.rgb, 1e-3h), _WetDarken * 2.0h);
        albedo.rgb = lerp(albedo.rgb, wetColor, wetMix);
        hairWet = _HairWet * fx.wet;
        if (hairWet > 0.001h)
        {
            // 濡れ髪: 水で毛がまとまって束になる。毛の流れ(異方性の向き)と直交する向きに細い縞を作り、束と束の間の溝を暗く沈める
            float2 huv = _AnisoDirection < 0.5h ? input.uv.yx : input.uv.xy; // x=流れと直交(縞の向き) y=流れに沿う(ゆっくり変わる)
            float n = GalaxyValueNoise2(float2(huv.x * _HairWetClumps, huv.y * _HairWetClumps * 0.12)) * 0.6
                    + GalaxyValueNoise2(float2(huv.x * _HairWetClumps * 2.7 + 5.3, huv.y * _HairWetClumps * 0.3)) * 0.4;
            hairClump = smoothstep(0.35h, 0.6h, (half)n);
            albedo.rgb *= 1.0h - hairWet * 0.35h * (1.0h - hairClump);
        }
        albedo.rgb *= 1.0h - 0.12h * fx.sweat; // 汗の粒は少し濃く(水の厚みで色が沈む)
        // 泡: 中はうっすら白く透け、膜(縁・泡どうしの境目)ははっきり白い
        albedo.rgb = lerp(albedo.rgb, _FoamColor.rgb, saturate(fx.foam * _FoamColor.a * 0.25h + fx.foamEdge * _FoamColor.a));
        albedo.rgb = lerp(albedo.rgb, _DirtColor.rgb, fx.dirt * _DirtColor.a);         // 汚れ・すす
        albedo.rgb = lerp(albedo.rgb, _ScratchColor.rgb, fx.scratch * _ScratchColor.a); // 傷(下地が見える)
    #endif

    // レイヤーテクスチャ(模様・光の帯など)。影を受けない(光る)ものは layerGlow に分け、最後に発光として足す
    half3 layerGlow;
    GalaxyApplyLayers(albedo.rgb, layerGlow, input.uv);

    if (_GalaxyToonOff > 0.5) return GalaxyToonLitFallback(input, albedo, normal, viewDir); // 見比べ用「シェーダー OFF」

    // ==== 4. 光 ====
    half3 lightDir, lightColor; half receivedShadow;
    GalaxyToonMainLight(input.positionWS, lightDir, lightColor, receivedShadow);
    #if defined(LIGHTMAP_ON)
        // 焼いた光(建物など)。段数を指定すると、焼いた光もトゥーン風にベタ塗りになる
        half3 ambient = SampleLightmap(input.lightmapUV, normal) * max(0.0, 1.0 + _GalaxyBakedLightOffset); // 焼き直さずに明るさを変えられる
        if (_BakedSteps > 0.5h) ambient = floor(ambient * _BakedSteps + 0.5h) / _BakedSteps;
    #else
        half3 ambient = SampleSH(half3(0, 0, 0)); // 向きに関係なく一律(トゥーンのベタ塗りを崩さない)
    #endif
    lightColor = GalaxyToonLightColor(lightColor, ambient);

    // ==== 5. 影 ====
    // 光の当たり具合 lambert(0=影 1=光)を作り、落ち影・髪の影・AO を掛けてから、影1〜3の境界で切る
    half lambert = _LightSource > 1.5h ? 1.0h : saturate(dot(normal, lightDir) * 0.5h + 0.5h); // 「影だけ」は面の向きで影にしない(落ち影だけ)
    #if defined(_FACE_SDF)
        lambert = GalaxyFaceSdfLambert(input.uv, lightDir, faceForward);
    #endif
    #if defined(_ANIME)
        receivedShadow = lerp(receivedShadow, step(0.5h, receivedShadow), _AnimeHardness); // アニメ塗り: 落ち影もくっきり
    #endif
    lambert *= lerp(1.0h, receivedShadow, _ShadowReceive);
    #if defined(_HAIR_SHADOW)
        // 髪の影: 影1の境界より確実に暗い所まで下げる
        lambert = lerp(lambert, min(lambert, saturate(_ShadowBorder - _ShadowBlur - 0.02h)), GalaxyHairShadow(input.positionCS, lightDir) * _HairShadowReceive);
    #endif
    // AO マップ(くぼみ・重なり): 暗い所ほど影になりやすくする(影の境界を手前にずらす)
    lambert *= lerp(1.0h, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_linear_repeat, input.uv).r, _OcclusionStrength);
    half strength = SAMPLE_TEXTURE2D(_ShadowStrengthMask, sampler_linear_repeat, input.uv).r; // 影の濃さマスク
    float rampRowV = GalaxyRampRowV(input.uv); // 部位ごとのランプの段(影・ハイライトのランプ共通)
    half lit1;
    half3 color = GalaxyToonShade(albedo.rgb, lambert, strength, rampRowV, lit1);
    #if defined(_ANIME)
        // アニメ塗り: ライトの色味は残し、明るさのムラで塗った色をくすませない(暗い場面でも色が沈みすぎない)
        half lightMax = max(max(lightColor.r, lightColor.g), max(lightColor.b, 1e-3h));
        lightColor = lerp(lightColor, lightColor / lightMax * clamp(lightMax * 1.15h, 0.55h, 1.0h), _AnimeFlatLight);
    #endif
    color *= lightColor;

    // ==== 6. 足す光 ====
    // 金属・つや(URP 標準の金属・つやと同じ値の意味)。金属ほど地の色が暗くなり、映り込みが地の色に染まる
    #if defined(_GLOSSY)
        half metallic = _Metallic, smoothness = _Smoothness;
        #if defined(_METALLICGLOSSMAP)
            half4 mg = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_linear_repeat, input.uv);
            metallic *= mg.r; smoothness *= mg.a;
        #endif
        #if defined(_ANIME)
            metallic *= 1.0h - _AnimeNoGloss; // アニメ塗り: 金属で地の色を暗く沈めない(塗った色のまま)
        #endif
        color *= 1.0h - metallic * 0.75h;
    #endif

    // 追加ライト(点光源・スポット)。きらめき(addGloss)は濡れのつやで使う
    half3 addGloss = 0;
    #if defined(_ADDLIGHT)
        half3 addDiffuse;
        GalaxyToonAdditionalLights(input.positionWS, normal, viewDir, input.positionCS, addDiffuse, addGloss);
        color += albedo.rgb * addDiffuse;
    #endif
    color = min(color, albedo.rgb * _LightMaxLimit); // 追加ライトを足しても上限を超えない。光沢・リムはこの後に足す

    half3 halfDir = normalize(lightDir + viewDir);
    half nh = saturate(dot(normal, halfDir));
    half3 normalVS = mul((float3x3)UNITY_MATRIX_V, normal); // ビュー空間の法線(深度リム・マットキャップ・屈折)

    #if defined(_SPECULAR)
        // ハイライト(段のついた光沢。髪・瞳・金属など)
        if (_SpecularRampOn > 0.5h)
        {
            // ランプで光の形と色を決める: 光の外側(0)→中心(1)の位置でランプを引く(段・縞・色の移り変わりを絵で作れる)
            half u = saturate((nh - (1.0h - _SpecularSize)) / max(_SpecularSize, 1e-3h));
            half3 specRamp = SAMPLE_TEXTURE2D(_SpecularRampTex, sampler_linear_clamp, float2(u, rampRowV)).rgb;
            color += specRamp * _SpecularColor.rgb * lightColor * (_SpecularColor.a * lit1);
        }
        else
        {
            half spec = GalaxyTooning(nh, 1.0h - _SpecularSize, _SpecularBlur) * _SpecularColor.a * lit1;
            color += _SpecularColor.rgb * lightColor * spec;
        }
    #endif

    #if defined(_ANISOTROPY)
    {
        // 異方性ハイライト(髪の天使の輪): 髪の流れ(接線か従法線)に沿った細長い光。向きを法線側へずらして位置を動かす
        float3 strand = _AnisoDirection < 0.5h ? tangent : bitangent;
        float3 shifted = normalize(strand + normal * _AnisoShift);
        half th = dot(shifted, halfDir);
        // 濡れ髪: 輪は細く鋭く・強く、束の山にだけ乗る(溝では消える)
        half aniso = pow(sqrt(saturate(1.0h - th * th)), _AnisoSharpness * lerp(1.0h, 2.5h, hairWet));
        half anisoStrength = lerp(_AnisoColor.a, max(_AnisoColor.a, 0.7h) * hairClump, hairWet);
        half anisoMask = anisoStrength * lerp(1.0h, lit1, _AnisoShadowMask);
        if (_AnisoUseRamp > 0.5h && _SpecularRampOn > 0.5h)
        {
            // 天使の輪の形をランプで: 輪の外側(0)→中心(1)
            half3 ringRamp = SAMPLE_TEXTURE2D(_SpecularRampTex, sampler_linear_clamp, float2(saturate(aniso), rampRowV)).rgb;
            color += ringRamp * _AnisoColor.rgb * lightColor * anisoMask;
        }
        else
        {
            color += _AnisoColor.rgb * lightColor * GalaxyTooning(aniso, _AnisoBorder, _AnisoBlur) * anisoMask;
        }
    }
    #endif

    #if defined(_BACKLIGHT)
    {
        // 逆光ライト: 光がカメラの反対側(キャラの後ろ)にあるとき、縁が光る
        half directivity = pow(saturate(-dot(viewDir, lightDir) * 0.5h + 0.5h), _BacklightDirectivity);
        half ln = dot(normalize(-viewDir * _BacklightViewStrength + lightDir), normal) * 0.5h + 0.5h;
        half back = GalaxyTooning(ln, _BacklightBorder, _BacklightBlur) * directivity * _BacklightColor.a;
        color += _BacklightColor.rgb * lerp(half3(1, 1, 1), albedo.rgb, _BacklightMainStrength) * lightColor * back;
    }
    #endif

    #if defined(_GLOSSY)
    {
        // つやのハイライト: つやつやなほど小さく鋭く、強く
        half3 specTint = lerp(half3(1, 1, 1), albedo.rgb, metallic);
        half animeGloss = 1.0h;
        #if defined(_ANIME)
            animeGloss = 1.0h - _AnimeNoGloss; // アニメ塗り: 写実的なつや・映り込みを消す
        #endif
        half glossSpec = GalaxyTooning(nh, 1.0h - lerp(0.3h, 0.004h, smoothness), 0.01h + 0.05h * (1.0h - smoothness)) * smoothness * smoothness;
        color += specTint * lightColor * glossSpec * lerp(0.4h, 1.0h, lit1) * animeGloss;
        // 映り込み(反射プローブ)を段階に分けてトゥーンらしくする
        half3 reflectDir = reflect(-viewDir, normal);
        half3 refl = GlossyEnvironmentReflection(reflectDir, input.positionWS, 1.0h - smoothness, 1.0h, GetNormalizedScreenSpaceUV(input.positionCS));
        if (_ReflectionSteps > 0.5h) refl = floor(refl * _ReflectionSteps + 0.5h) / _ReflectionSteps;
        half fresnel = lerp(0.04h, 1.0h, metallic) + (1.0h - metallic) * pow(1.0h - saturate(dot(normal, viewDir)), 5.0h) * smoothness;
        color += refl * specTint * fresnel * _ReflectionStrength * animeGloss * max(0.0, 1.0 + _GalaxyBakedLightOffset); // 映り込みも焼いた光なので一緒に明るさを変える
    }
    #endif

    #if defined(_GLITTER)
        color += GalaxyGlitter(input.uv, normal, tangent, bitangent, viewDir, lightDir) * lightColor * lerp(1.0h, lit1, _GlitterShadowMask);
    #endif

    #if defined(_GALAXY_FX)
    {
        // 濡れ・汗・液体のつや: 小さく鋭いハイライト + 縁のてかり(影の側でも少し光る)
        // 水の膜の反射は正面では弱く、斜めから見るほど強い(フレネル)。正面の平らな面に白い帯がべったり出ないように
        half fxNdv = saturate(1.0h - dot(normal, viewDir));
        half fresnel = lerp(0.25h, 1.0h, fxNdv * fxNdv);
        half fxSpec = GalaxyTooning(nh, 0.985h, 0.03h) * fresnel;
        half fxEdge = pow(fxNdv, 4.0h);
        color += lightColor * fx.gloss * (fxSpec * lerp(0.3h, 1.0h, lit1) + fxEdge * 0.35h);
        color += fx.gloss * addGloss * fresnel; // 照明(点光源・スポット)でも、しずく・濡れた面がきらめく
    }
    #endif

    #if defined(_RIMLIGHT)
    {
        // リムライト(縁の光)。影の側では弱める
        half rim = pow(saturate(1.0h - abs(dot(normal, viewDir))), _RimFresnelPower);
        rim = GalaxyTooning(rim, _RimBorder, _RimBlur);
        #if defined(_DEPTH_RIM)
            // 深度リム: 法線の向きへ少しずらした画面の深度が、自分より十分奥なら「輪郭のすぐ内側」として光らせる(シルエットだけが光る)
            float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
            float selfDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
            // 幅は cm(ワールド)。透視投影で画面上の UV のずれに直す。
            // 絶対値: テクスチャ描画で投影が上下反転しても(画面 UV 側で補正済み)向きを変えない
            float2 offset = normalVS.xy * (_RimDepthWidth * 0.01 / max(selfDepth, 0.1)) * abs(float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11)) * 0.5;
            float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV + offset), _ZBufferParams);
            rim *= step(_RimDepthThreshold, sceneDepth - selfDepth);
        #endif
        rim *= _RimColor.a * lerp(1.0h, lit1, _RimShadowMask);
        color += _RimColor.rgb * lightColor * rim;
    }
    #endif

    // マットキャップ: ビュー空間の法線で球テクスチャを引く
    half2 matCapUV = normalVS.xy * 0.5h + 0.5h;
    #if defined(_MATCAP)
        half3 matCap = SAMPLE_TEXTURE2D(_MatCapTex, sampler_linear_clamp, matCapUV).rgb * _MatCapColor.rgb * _MatCapColor.a;
        color += matCap * lightColor * lerp(1.0h, lit1, _MatCapShadowMask);
    #endif
    #if defined(_MATCAP2)
    {
        half3 matCap2 = SAMPLE_TEXTURE2D(_MatCap2Tex, sampler_linear_clamp, matCapUV).rgb * _MatCap2Color.rgb;
        half mask2 = _MatCap2Color.a * SAMPLE_TEXTURE2D(_MatCap2Mask, sampler_linear_repeat, input.uv).r * lerp(1.0h, lit1, _MatCap2ShadowMask);
        color = _MatCap2Blend < 0.5h ? color + matCap2 * lightColor * mask2 : lerp(color, color * matCap2, mask2); // 加算 / 乗算
    }
    #endif

    // ==== 7. 仕上げ ====
    // 発光(ライトに関係なく足す)
    #if defined(_EMISSION)
        color += GalaxyEmission(input.uv);
    #endif
    color += layerGlow;
    #if defined(_EYE)
        color += GalaxyEyeGlow(eye) * lerp(half3(1, 1, 1), lightColor, 0.5h); // 瞳のハイライトは暗い場面で少し落とす
    #endif
    #if defined(_DISSOLVE)
        color = lerp(color, _DissolveEdgeColor.rgb, dissolveEdge * _DissolveEdgeColor.a); // ディゾルブの境目
    #endif

    color = MixFog(color, input.fogFactor);
    #if defined(_REFRACTION)
    {
        // 屈折(半透明): 後ろの景色を法線でずらして引き、地の色で染めて混ぜる。RGB をずらして虹色ににじませる
        float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
        float2 offset = normalVS.xy * _RefractionStrength * 0.08;
        half3 behind;
        behind.r = SampleSceneColor(screenUV + offset * (1.0 + _RefractionDispersion)).r;
        behind.g = SampleSceneColor(screenUV + offset).g;
        behind.b = SampleSceneColor(screenUV + offset * (1.0 - _RefractionDispersion)).b;
        return half4(lerp(behind * albedo.rgb, color, albedo.a), 1.0h);
    }
    #endif
    return half4(color, albedo.a);
}

#endif
