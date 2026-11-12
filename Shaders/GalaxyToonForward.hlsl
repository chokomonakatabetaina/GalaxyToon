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
#if (defined(_DEPTH_RIM) || defined(_CREVICE_AO) || defined(_CONTACT_SHADOW)) && !defined(_HAIR_SHADOW)
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
    float3 shadowNormalTS : TEXCOORD6; // 影だけに使う法線(接空間。UV7。_ShadowNormalOn のときだけ使う)
    half4 color : COLOR;   // 頂点カラー(影の出やすさ R・境界のずらし G。メッシュに無ければ白)
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
    half4 softFxColor : TEXCOORD7; // xy=触れるとへこむの見た目(x=近づくものの影 y=押した強さ) zw=頂点カラーの R(影の出やすさ)・G(境界のずらし)
    float2 uv2 : TEXCOORD8;        // 2 番目の UV(天使の輪の帯)
    float3 shadowNormalWS : TEXCOORD9; // 影だけに使う法線(ワールド。無ければ 0)
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings GalaxyToonVert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float3 softNormalWS = GalaxySoftTouchNormal(TransformObjectToWorld(input.positionOS.xyz), normalWS, input.uv);
    // 頂点を動かす処理(触れるとへこむ → 濡れて縮む → 髪の揺れ)。影・深度・輪郭線のパスも同じ順で動かすので、形がずれない
    float3 positionWS = GalaxyHairSwayFlatten(GalaxyWetShrink(GalaxySoftTouch(TransformObjectToWorld(input.positionOS.xyz), normalWS, input.uv), normalWS));
    output.positionCS = GalaxyClipAdjust(TransformWorldToHClip(positionWS), positionWS, input.uv);
    output.positionWS = positionWS;
    output.normalWS = softNormalWS;
    output.softFxColor = half4(GalaxySoftTouchFx(TransformObjectToWorld(input.positionOS.xyz), normalWS, input.uv), input.color.rg);
    output.uv2 = input.lightmapUV;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    output.positionOS = input.positionOS.xyz;
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    // 影だけに使う法線: 接空間 → ワールド。整えた法線は影の形だけに使い、ハイライト・リムは元の法線のまま
    output.shadowNormalWS = 0;
    if (_ShadowNormalOn > 0.5 && dot(input.shadowNormalTS, input.shadowNormalTS) > 0.25) // メッシュに無い UV は 0 とは限らない(別の値が入る)ので、マテリアルで使うと決めたときだけ
    {
        float3 bitangentWS = cross(normalInputs.normalWS, normalInputs.tangentWS) * (input.tangentOS.w >= 0 ? 1.0 : -1.0) * GetOddNegativeScale();
        output.shadowNormalWS = normalize(normalInputs.tangentWS * input.shadowNormalTS.x + bitangentWS * input.shadowNormalTS.y + normalInputs.normalWS * input.shadowNormalTS.z);
    }
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
    lightColor = lerp(lightColor, Luminance(lightColor).xxx, _LightGray);
    half m = max(max(lightColor.r, lightColor.g), max(lightColor.b, 1e-3h));
    // 光の色の正規化: いちばん明るいチャンネルを 1 にそろえ、色味だけ残す(暗い色のライトでも塗りが暗くならない)
    lightColor = lerp(lightColor, lightColor / m, _LightNormalize);
    // 明るさの上限をなめらかに: 1 を超えた分を 1 + x/(1+x) に寄せる(急に頭打ちにしない)
    m = max(max(lightColor.r, lightColor.g), max(lightColor.b, 1e-3h));
    if (_LightSoftLimit > 0.001h && m > 1.0h)
    {
        half over = m - 1.0h;
        lightColor *= lerp(1.0h, (1.0h + over / (1.0h + over)) / m, _LightSoftLimit);
    }
    return clamp(lightColor, _LightFloor, _LightCeiling);
}

/// 主光源: キャラ専用ライト(GalaxyToonLight)が設定されていればそれ、無ければシーンのメインライト。
/// 落ち影(shadow)はどちらの場合もシーンのメインライトのシャドウマップから読む。
void GalaxyToonMainLight(float3 positionWS, out half3 direction, out half3 color, out half shadow)
{
    // 落ち影を受ける位置を光の向きへ少しずらす(。顔・体の自己影の汚れを減らす)。光の計算は元の位置
    float3 shadowPos = positionWS;
    if (_ReceiveShadowOffset > 0.001h) shadowPos += GetMainLight().direction * (_ReceiveShadowOffset * 0.01);
    Light main = GetMainLight(TransformWorldToShadowCoord(shadowPos), positionWS, half4(1, 1, 1, 1));
    shadow = main.shadowAttenuation;
    direction = main.direction;
    color = main.color;
    float3 toonDir = GalaxyToonLightDir();
    bool useToonLight = _LightSource < 0.5h && dot(toonDir, toonDir) > 1e-4;
    if (useToonLight)
    {
        direction = normalize(toonDir);
        color = GalaxyToonLightColor();
    }
    if (_LightSource > 1.5h) color = 0; // 「焼いた光+影だけ」: メインライトの光は足さず、落ちる影だけ使う(焼き込んだ背景用)
}

/// 追加ライト1つぶんの明るさ。当たる/当たらないを影1の境界で切り(トゥーン)、明るさは N・L で落とす(全面ベタに明るくしない)
half3 GalaxyToonAddLightDiffuse(Light light, half3 normal)
{
    half ndl = dot(normal, light.direction);
    half border = _AddLightBorder < 0.0h ? _Shade1Edge : _AddLightBorder; // 追加ライト専用の境界
    half lit = GalaxyTooningNoAA(ndl * 0.5h + 0.5h, border, _Shade1Soft) * saturate(ndl);
    lit = lerp(lit, 1.0h, _AddLightIgnoreNormal); // 面の向きを見ず、距離だけで明るくする(点光源で影の形を作らない)
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    #if defined(_ANIME)
        // アニメ塗り: 照明の明るさも段にする(なだらかな光だまりを作らない)
        atten = GalaxyTooningNoAA(atten, 0.25h, 0.08h) * 0.6h;
        lit = step(0.5h, lit);
    #endif
    half3 c = lerp(light.color, Luminance(light.color).xxx, _AddLightMono);
    return min(c * (atten * lit), _AddLightMax);
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
        // 1 枚目が無いバリアントでは sampler_BumpMap が消えるので、2 枚目はインラインのサンプラーで引く
        half3 normalTS2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Bump2Map, sampler_linear_repeat, uv * _Bump2Map_ST.xy + _Bump2Map_ST.zw), _Bump2Scale);
        normalTS = normalize(half3(normalTS.xy + normalTS2.xy, normalTS.z * normalTS2.z)); // 2枚の傾きを足す(Whiteout 合成)
    #endif
    #if defined(_ANIME)
        normalTS = normalize(lerp(normalTS, half3(0, 0, 1), _AnimeFlatNormal)); // アニメ塗り: 細かい凹凸で影がザラつかないように
    #endif
    return normalize(mul(normalTS, half3x3(tangent, bitangent, normal)));
}

// ================================================================ 髪の光
/// 毛束の光(Kajiya-Kay): 毛の流れと半分ベクトルの角の sin を exponent 乗。
/// 鋭い光はピクセルより細くなり、動くとちらつく。sin^n はおおよそ分散 1/n のガウスなので、ピクセル内の変化 width(cos の変化)の分を分散に足して太らせ、
/// 面積が変わらないよう高さを下げる(遠い・細かい所ほど淡く太い光になる)
half GalaxyStrandLobe(half cosTH, half exponent, half width)
{
    half scale = rcp(1.0h + 0.25h * exponent * width * width);
    return pow(max(sqrt(saturate(1.0h - cosTH * cosTH)), 1e-4h), exponent * scale) * sqrt(scale);
}

// ================================================================ 影

/// SDF 顔影: 光が顔の正面から何度横にあるか(0=正面 1=真後ろ)を、テクスチャのしきい値(R=光が右から / G=左から)と比べる。
/// 上下の光(太陽が高い・低いとき)は _FaceSdfPitch でしきい値を加減し、真上・真下の光でも自然に影が動くようにする。
/// 戻り値は N・L の代わりに使う「光の当たり具合」(0=影 1=光)
half GalaxyFaceSdfLambert(float2 uv, half3 lightDir, float3 faceForward)
{
    float3 faceRight = dot(_FaceRight.xyz, _FaceRight.xyz) > 1e-4 ? normalize(_FaceRight.xyz) : normalize(mul((float3x3)UNITY_MATRIX_M, float3(1, 0, 0)));
    // 顔の上は、正面と右から求める(GalaxyToonFace が入れる _FaceUp と同じ向き)。_FaceUp の既定値 (0,1,0) はワールドの上なので、
    // GalaxyToonFace が無いとき(保存済みのマテリアルにはその値が入っている)にモデルが傾くと上下の光の判定がずれるため使わない
    float3 faceUp = normalize(cross(faceForward, faceRight));
    float2 lightXZ = float2(dot(lightDir, faceRight), dot(lightDir, faceForward)); // 顔の水平面に写した光の向き
    float horizLen = length(lightXZ);
    float2 dirXZ = horizLen > 1e-4 ? lightXZ / horizLen : float2(0, 1);
    half lightAngle = acos(clamp(dirXZ.y, -1.0, 1.0)) / PI;
    half2 sdf = SAMPLE_TEXTURE2D(_FaceSdfTex, sampler_linear_repeat, uv).rg;
    half threshold = dirXZ.x >= 0 ? sdf.r : sdf.g;
    // 上下の光(ピッチ角): 下からの光では影が増え、斜め上からは明るく、真上・真下に近いほど正面度合いに応じてなめらかに遷移。
    // * 4.0 で急峻に切らずそのまま 0〜1 のソフトな距離値として返すことで、後の GalaxyToonShade の _Shade1Edge / _Shade1Soft がそのまま効くようにする
    half lightY = (half)dot(lightDir, faceUp);
    half pitchOffset = lightY * _FaceSdfPitch * 0.45h;
    // しきい値と光の角度が同じ所を、ちょうど影1の境界(_Shade1Edge)にする。0.5 を足すと、境界が 0.5 でない顔(ほかのシェーダーから移すと 0.86 など)では、
    // 影マップの形と関係なく顔の大半が影になる(境界が低いと逆に影が出ない)
    half raw = saturate(threshold + pitchOffset - lightAngle + _Shade1Edge);
    half verticalFallback = saturate(lightY * 0.5h + 0.5h);
    // 真上・真下に近い光(水平の成分が小さい)は、水平の向き(左右・前後)がほとんど決まらないので、上下の向きだけで決める。
    // _FaceSdfPitch が 0 でも、ほぼ真上・真下の光では前からの光とみなさない(真下からの光で顔が明るくならない)
    half vertical = max(saturate((1.0h - (half)horizLen) * _FaceSdfPitch), smoothstep(0.35h, 0.05h, (half)horizLen));
    return lerp(raw, verticalFallback, vertical);
}

/// 影を塗る。lambert(0=影 1=光)を影1〜3の境界で切り、地の色に影色を重ねる。
/// lit1(影1の当たり具合 0=影 1=光)は後のハイライト・リム・マットキャップで「影の側では消す」に使う。
/// shadeBase: 影の中の地の色(影色テクスチャがあればそれ、無ければ albedo)。影 1〜3 と アニメ塗りの影色はこれから作る
half3 GalaxyToonShade(half3 albedo, half3 shadeBase, half lambert, half strength, float rampRowV, out half lit1)
{
    half blur1 = _Shade1Soft, blur2 = _Shade2Soft;
    #if defined(_ANIME)
        // アニメ塗り: 影の境界をくっきり(ぼかしを細く)
        blur1 = lerp(blur1, 0.004h, _AnimeHardness);
        blur2 = lerp(blur2, 0.004h, _AnimeHardness);
    #endif
    lit1 = GalaxyTooning(lambert, _Shade1Edge, blur1);
    half lit2 = GalaxyTooning(lambert, _Shade2Edge, blur2);
    half shadow1 = (1.0h - lit1) * _Shade1Tint.a * strength;    // 影1の濃さ(0〜1)
    half shadow2 = (1.0h - lit2) * _Shade2Tint.a * strength; // 影2の濃さ(0〜1)
    half3 color = albedo;

    #if defined(_SHADOW_RAMP)
        // ランプ: 左(暗い)→右(明るい)のグラデーション画像で影の色を決める。
        // _Shade1Edge(デフォルト 0.5)と _Shade1Soft(デフォルト 0.05)および _AnimeHardness をランプ引く位置にも反映し、
        // ランプ設定時でもスライダーで影の広さ・硬さ・色味・2段目影・アニメ境界色を自在に調整できるようにする
        half rampScale = lerp(1.0h, 0.05h / max(blur1, 0.004h), 0.75h);
        half rampU = saturate((lambert - _Shade1Edge) * rampScale + 0.5h);
        rampU = lerp(1.0h, rampU, strength * _Shade1Tint.a);
        half3 rampSample = SAMPLE_TEXTURE2D(_ShadowRampTex, sampler_linear_clamp, float2(rampU, rampRowV)).rgb;
        if (_RampDayNight > 0.5h)
        {
            // 昼／夜のランプ: 画像の上半分が昼、下半分が夜。同じ段を両方で引いて、夜の量で混ぜる
            half night = saturate(_RampNightAmount + (GalaxyHasLocalLight() ? _GalaxyLocalLightDir.w : _GalaxyNight));
            half3 dayRamp = SAMPLE_TEXTURE2D(_ShadowRampTex, sampler_linear_clamp, float2(rampU, 0.5 + rampRowV * 0.5)).rgb;
            half3 nightRamp = SAMPLE_TEXTURE2D(_ShadowRampTex, sampler_linear_clamp, float2(rampU, rampRowV * 0.5)).rgb;
            rampSample = lerp(dayRamp, nightRamp, night);
        }
        // 影側ほど _Shade1Tint.rgb の色味も掛け合わせる(白=1,1,1 ならランプそのまま)。影の濃さ(マスク・A)の分だけ
        half3 shadowTint = lerp(half3(1, 1, 1), _Shade1Tint.rgb, shadow1);
        #if defined(_ANIME)
            // アニメ塗り併用時: 影側の色相・彩度・明度シフトもランプ影に反映。
            // 地の色との比(割り算)にすると、0 に近いチャンネル(純色の赤の G など)で何十倍にも跳ねて別の色が出るので、地の色そのものを影の色へ寄せる
            color = lerp(color, GalaxyAnimeShadowColor(shadeBase), (1.0h - lit1) * strength * 0.65h);
        #else
            color = lerp(color, shadeBase, shadow1);
        #endif
        color *= rampSample * shadowTint;
        #if defined(_SHADOW2ND)
            color = lerp(color, color * _Shade2Tint.rgb, shadow2);
        #endif
        #if defined(_ANIME)
            half edgeBand = saturate(1.0h - abs(lambert - _Shade1Edge) / max(_AnimeEdgeWidth, 1e-3h));
            color = lerp(color, albedo * _AnimeEdgeColor.rgb, edgeBand * _AnimeEdgeColor.a * strength * step(1e-3h, _AnimeEdgeWidth));
        #endif
    #elif defined(_ANIME)
        // アニメ塗り: 影色を地の色から作る(色相を少しずらし、鮮やかに、暗く)。灰色をかけたような濁った影にしない
        half3 animeShadow = GalaxyAnimeShadowColor(shadeBase) * _Shade1Tint.rgb;
        color = lerp(color, animeShadow, shadow1);
        #if defined(_SHADOW2ND)
            color = lerp(color, GalaxyAnimeShadowColor(animeShadow) * _Shade2Tint.rgb, shadow2);
        #endif
        // 影の境界の色(肌の赤い境界線など): 境界のまわりの細い帯
        half edgeBand = saturate(1.0h - abs(lambert - _Shade1Edge) / max(_AnimeEdgeWidth, 1e-3h));
        color = lerp(color, albedo * _AnimeEdgeColor.rgb, edgeBand * _AnimeEdgeColor.a * strength * step(1e-3h, _AnimeEdgeWidth));
    #else
        // ふつうの段影: 影色テクスチャ(無ければ地の色) × 影色
        color = lerp(color, shadeBase * _Shade1Tint.rgb, shadow1);
        #if defined(_SHADOW2ND)
            color = lerp(color, shadeBase * _Shade2Tint.rgb, shadow2);
        #endif
    #endif

    #if defined(_SHADOW3RD)
        half lit3 = GalaxyTooning(lambert, _Shade3Edge, _Shade3Soft);
        color = lerp(color, shadeBase * _Shade3Tint.rgb, (1.0h - lit3) * _Shade3Tint.a * strength);
    #endif
    return color;
}

/// SSS(表面下散乱): トゥーンのパキッとした影を保ったまま、明暗境界(ターミネーター)の血色にじみと、影の中のわずかな血色残りを足す。
/// thickness(厚み・SSSマスク R: 白=薄く透ける肌 黒=服・目・金属)で肌だけに限定し、目や服に赤みが漏れないようにする。
half3 GalaxyToonApplySssShade(half3 shadedColor, half3 albedo, half lambert, half lit1, half strength, half thickness)
{
    #if defined(_SSS)
        half sssMask = thickness * _SssColor.a * strength;
        if (sssMask > 0.001h)
        {
            // 1. 明暗境界(ターミネーター)付近の鮮やかな血色帯
            half width = max(_SssEdgeWidth, 1e-3h);
            half edgeDist = abs(lambert - _Shade1Edge);
            half terminator = smoothstep(width, 0.0h, edgeDist);
            // 境界のすぐ影側でいちばん強く血色が乗るように非対称重み付け
            terminator *= saturate(1.2h - (lambert - _Shade1Edge) / width * 0.5h);
            // 2. 影の内部にほんのり残る血色(冷たい灰色影を防ぐ)
            half shadowBleed = (1.0h - lit1) * _SssShadowBleed;
            half bleed = saturate(terminator + shadowBleed) * sssMask;
            // 地の色と現在の影色に _SssColor の鮮やかな血色ティントを掛け合わせ、G/B を適度に落として赤みの彩度をしっかり出す
            half3 warmTint = lerp(shadedColor, albedo, 0.65h) * _SssColor.rgb * 1.35h;
            shadedColor = lerp(shadedColor, warmTint, bleed);
        }
    #endif
    return shadedColor;
}

/// SSS 逆光・斜光の透過光(Approximated Translucency): 耳たぶ・鼻先・指先など薄い部位(thickness が高い所)を光が通り抜けて赤く輝く。
/// 正面・斜めの光での透けは _SssFrontScatter で足す(既定は逆光だけ)
half3 GalaxyToonSssTranslucency(half3 albedo, half3 normal, half3 viewDir, half3 lightDir, half3 lightColor, half receivedShadow, half thickness)
{
    #if defined(_SSS)
        if (_SssTranslucency > 0.001h && thickness > 0.001h)
        {
            half3 transLightDir = normalize(lightDir + normal * _SssDistortion);
            // 逆光成分(カメラの反対側から来る光) + 斜光・輪郭のサブサーフェス Wrap 散乱(正面〜上からのライトでも耳や輪郭の薄い部位が透ける)
            half backDot = saturate(dot(-viewDir, transLightDir));
            half wrapLight = saturate((dot(-normal, transLightDir) + 0.55h) / 1.55h);
            half edgeGrazing = saturate(1.0h - abs(dot(normal, viewDir))) * saturate(1.0h - max(0.0h, dot(normal, lightDir)) * 0.7h);
            // 正面・斜めの光での透け(輪郭と明暗境界)は _SssFrontScatter の分だけ。既定(0)は逆光のときだけ(厚みマップが無いと肌の輪郭全体が赤く光るため)
            half scatterBase = max(backDot, max(wrapLight * 0.65h, edgeGrazing * 0.75h) * _SssFrontScatter);
            half transDot = pow(scatterBase, max(_SssPower * 0.5h, 0.5h));
            // 落ち影が濃く落ちている場所でも少しは散乱しつつ、直射光がある薄い部位ほど強く透ける
            half shadowAtten = lerp(1.0h, receivedShadow, _CastShadowAmount * 0.7h);
            half trans = transDot * thickness * _SssTranslucency * _SssColor.a * shadowAtten;
            return albedo * _SssColor.rgb * lightColor * trans;
        }
    #endif
    return 0;
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
    float3 toonDir = GalaxyToonLightDir();
    if (_LightSource < 0.5h && dot(toonDir, toonDir) > 1e-4)
        lit += albedo.rgb * GalaxyToonLightColor() * saturate(dot(normal, normalize(toonDir))) * (1.0h - _Metallic);
    return half4(MixFog(lit, input.fogFactor), albedo.a);
}

// ================================================================ フラグメント本体

#if defined(_CONTACT_SHADOW)
/// 接地影: 面から光の向きへ 8 歩たどり、途中の点が画面の深度より奥(何かの裏)なら、細い影にする。
/// 厚み 3cm より奥は遠くの物とみなして数えない(背景に影を落とさない)。指・襟・髪の生え際の接点にだけ出る
/// 深度だけ手前へ・遠近の除去で深度バッファがずれているときは、たどる点にも同じずらしを掛けて比べる(自分の面を影と見ない)
/// 体の自己影も同じたどり方で、長さ・厚みを大きくして使う(顎の下・腕から胴・前髪から額。len・thickness は m)
half GalaxyScreenShadow(float3 positionWS, float4 positionCS, half3 lightDir, half3 normal, float2 meshUV, float len, float thickness)
{
    float zShift = abs(_ZOffsetCm) > 1e-4h ? _ZOffsetCm * 0.01 * SAMPLE_TEXTURE2D_LOD(_ZOffsetMask, sampler_linear_repeat, meshUV, 0).r : 0;
    bool perspective = _PerspectiveRemoval > 0.001h && _PerspectiveCenter.w > 0.5;
    float jitter = InterleavedGradientNoise(positionCS.xy, 1);
    float3 start = positionWS + normal * 0.002;
    half occluded = 0.0h;
    [unroll]
    for (int i = 0; i < 8; i++)
    {
        float3 p = start + lightDir * ((i + jitter) / 8.0 * len);
        float4 cs = TransformWorldToHClip(p);
        if (perspective)
        {
            float centerW = TransformWorldToHClip(_PerspectiveCenter.xyz).w;
            if (centerW > 1e-4) cs.xy = lerp(cs.xy, cs.xy * (cs.w / centerW), _PerspectiveRemoval);
        }
        float2 uv = float2(cs.x, cs.y * _ProjectionParams.x) / cs.w * 0.5 + 0.5;
        if (any(uv < 0) || any(uv > 1)) continue;
        float depth = -TransformWorldToView(p).z - zShift;
        float scene = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
        float behind = depth - scene;
        occluded = max(occluded, (half)(smoothstep(0.001, 0.004, behind) * step(behind, thickness)));
    }
    return occluded;
}

half GalaxyContactShadow(float3 positionWS, float4 positionCS, half3 lightDir, half3 normal, float2 meshUV)
{
    half shadow = 1.0h - GalaxyScreenShadow(positionWS, positionCS, lightDir, normal, meshUV, _ContactShadowLength * 0.01, 0.03) * _ContactShadowStrength;
    if (_SelfShadowStrength > 0.001h)
        shadow = min(shadow, 1.0h - GalaxyScreenShadow(positionWS, positionCS, lightDir, normal, meshUV, _SelfShadowLength * 0.01, _SelfShadowThickness * 0.01) * _SelfShadowStrength);
    return shadow;
}
#endif

// キャラ専用のシャドウマップ(Galaxy Character Shadow Feature が入れる。params: x=濃さ y=1/大きさ z=ぼかし(画素) w=1 で有効)
TEXTURE2D_SHADOW(_GalaxyCharShadowMap);
float4x4 _GalaxyCharShadowMatrix;
float4 _GalaxyCharShadowParams;

/// キャラ専用のシャドウマップの影(1=光 0=影)。3×3 で比べてならす。範囲の外・使わないときは 1
half GalaxyCharacterShadow(float3 positionWS, half3 normal)
{
    if (_GalaxyCharShadowParams.w < 0.5) return 1.0h;
    // 自分の影の汚れ(格子・縞)を防ぐ: 法線の向きへのずらしを、影の画像の 1 画素の大きさと、光に対する面の傾きで増やす
    // (光をかすめる頬・首の横ほど大きく。1 画素の大きさ(m)と光の向きは、影の行列の 1 行目の長さと 3 行目から)
    float texel = _GalaxyCharShadowParams.y / max(length(_GalaxyCharShadowMatrix[0].xyz), 1e-5);
    float ndl = abs(dot((float3)normal, normalize(_GalaxyCharShadowMatrix[2].xyz)));
    float offset = max(0.002, texel * (1.5 + 2.5 * max(_GalaxyCharShadowParams.z, 1.0) * sqrt(saturate(1.0 - ndl * ndl)))); // ぼかしが広いほど遠くの画素と比べるので、その分も
    float4 coord = mul(_GalaxyCharShadowMatrix, float4(positionWS + normal * offset, 1.0));
    coord.xyz /= max(coord.w, 1e-5);
    if (any(coord.xy < 0) || any(coord.xy > 1)) return 1.0h;
    float2 spacing = _GalaxyCharShadowParams.y * _GalaxyCharShadowParams.z;
    half sum = 0;
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            sum += SAMPLE_TEXTURE2D_SHADOW(_GalaxyCharShadowMap, sampler_LinearClampCompare, float3(coord.xy + float2(x, y) * spacing, coord.z));
        }
    }
    return lerp(1.0h, sum / 9.0h, (half)_GalaxyCharShadowParams.x);
}

/// 星空(マントの裏など): 画面に貼りつく星を 3 層、層ごとに視線で少しずらし(奥行き)、ゆっくり瞬かせる。星雲の色を薄く重ねる
half3 GalaxyStarry(float4 positionCS, half3 viewDir, float2 uv)
{
    half mask = SAMPLE_TEXTURE2D(_StarryMask, sampler_linear_repeat, uv).r;
    if (mask <= 0.001h) return 0;
    float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
    screenUV.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
    float3 viewVS = mul((float3x3)UNITY_MATRIX_V, -viewDir);
    half3 stars = 0;
    [unroll]
    for (int k = 0; k < 3; k++)
    {
        float2 p = screenUV * _StarryScale * (1.0 + k * 0.7) + viewVS.xy * _StarryParallax * (k + 1) * 3.0 + k * 17.31;
        float2 cell = floor(p);
        float2 f = frac(p) - 0.5;
        float r1 = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
        float r2 = frac(sin(dot(cell, float2(39.3468, 11.1352))) * 24634.6345);
        float2 offset = float2(r1, r2) - 0.5;
        float d = length(f - offset * 0.7);
        float size = lerp(0.02, 0.09, r2 * r2);
        float twinkle = 0.55 + 0.45 * sin(_Time.y * (1.0 + r1 * 3.0) + r2 * 6.2831);
        stars += _StarryColor.rgb * (smoothstep(size, 0.0, d) * twinkle * step(0.55, r1) / (1.0 + k));
    }
    float nebula = 0.5 + 0.5 * sin(screenUV.x * 3.1 + screenUV.y * 2.3 + sin(screenUV.y * 5.0 + _Time.y * 0.05));
    return (stars + _StarryNebula.rgb * nebula * _StarryNebula.a * 0.25) * mask * _StarryStrength;
}

#if defined(_CREVICE_AO)
// 奥ほど暗く: 面の外側の半球に置いた 8 つの点が、画面の深度より奥(何かの後ろ)にあれば、くぼみ・重なりの中として暗くする(SSAO)。
// 1=開けている 0=囲まれている。カメラの深度テクスチャを使うので、ポーズ・カメラ・服の重なりに合わせて毎フレーム変わる。
// 深度の差が半径より大きい点(はるか手前の別の物)は数えない。点の向きは画素ごとに回して、縞を細かいざらつきにする
half GalaxyCreviceOcclusion(float3 positionWS, float4 positionCS, half3 normal)
{
    static const float3 kernel[8] =
    {
        float3( 0.53,  0.21, 0.45), float3(-0.35,  0.55, 0.30), float3(-0.62, -0.28, 0.52), float3( 0.18, -0.70, 0.40),
        float3( 0.80,  0.35, 0.22), float3(-0.25,  0.88, 0.35), float3(-0.90, -0.15, 0.30), float3( 0.30, -0.85, 0.40),
    };
    float radius = _CreviceRadius * 0.01;
    float3 up = abs(normal.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
    float3 t = normalize(cross(up, normal));
    float3 b = cross(normal, t);
    float angle = InterleavedGradientNoise(positionCS.xy, 0) * 6.2831853;
    float2 cs = float2(cos(angle), sin(angle));
    half hidden = 0.0h;
    [unroll]
    for (int i = 0; i < 8; i++)
    {
        float3 k = kernel[i] * lerp(0.25, 1.0, (i + 1) / 8.0);
        float2 r = float2(k.x * cs.x - k.y * cs.y, k.x * cs.y + k.y * cs.x);
        float3 sampleWS = positionWS + (t * r.x + b * r.y + normal * (k.z + 0.05)) * radius;
        float4 sampleCS = TransformWorldToHClip(sampleWS);
        float2 uv = float2(sampleCS.x, sampleCS.y * _ProjectionParams.x) / sampleCS.w * 0.5 + 0.5; // ComputeScreenPos と同じ
        float sampleDepth = -TransformWorldToView(sampleWS).z;
        float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
        float behind = sampleDepth - sceneDepth;
        hidden += (half)(step(0.002, behind) * saturate(radius * 2.0 / max(behind, 1e-4)));
    }

    return 1.0h - hidden / 8.0h;
}
#endif

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
    // 触れるとへこむ: 押した所は、へこんだ形の面の向き(画素ごとに位置の差から作る)へ法線を寄せる(頂点で倒した法線だけだと、へこみの陰がぼやける)
    if (_SoftTouch > 0.5h && input.softFxColor.y > 0.01h)
    {
        float3 faceN = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
        faceN = dot(faceN, (float3)normal) < 0.0 ? -faceN : faceN;
        normal = normalize(lerp(normal, (half3)faceN, saturate(input.softFxColor.y) * 0.5h));
    }

    // ==== 1. 地の色 ====
    #if defined(_EYE)
        GalaxyEyeData eye = GalaxyEyeSetup(input.uv, viewDir, tangent, bitangent, normal); // 瞳の中は奥行きでずらした UV で引く
        half4 albedo = GalaxyToonAlbedo(eye.uv);
        albedo.rgb = GalaxyEyeShade(eye, albedo.rgb, (half3)GetMainLight().direction); // まぶたの影・瞳の光の溜まり
    #else
        half4 albedo = GalaxyToonAlbedo(input.uv);
    #endif
    #if defined(_TRIPLANAR)
        albedo.rgb = GalaxyTriplanarAlbedo(input.positionWS, input.normalWS); // アルファは UV で引いたものを使う
    #else
        albedo.rgb = GalaxyTexLineKeep(albedo.rgb, input.uv, input.positionWS);
    #endif
    // アルファ・トゥ・カバレッジ: しきい値の所で切らず、縁の 1 ピクセルを MSAA のサンプルの割合で覆う(ずっと薄い所だけ切る)
    if (_AlphaToMask > 0.5h) clip(albedo.a - _Cutoff * 0.5h); else GalaxyToonClip(albedo.a);
    // 触れるとへこむの見た目: 押した所は血色を帯び、近づくものの下はやわらかく暗くなる
    if (_SoftTouch > 0.5h)
    {
        albedo.rgb = lerp(albedo.rgb, albedo.rgb * _SoftBlush.rgb, input.softFxColor.y * _SoftBlush.a);
        // 近づくものの影は、この面の影の色(影の色の掛け算・アニメ塗りの影の色)へ寄せる(ただ暗くすると灰色に濁る)
        half3 nearShade = GalaxyAnimeShadowColor(albedo.rgb) * _Shade1Tint.rgb;
        albedo.rgb = lerp(albedo.rgb, nearShade, saturate(input.softFxColor.x * _SoftShadow * 1.6h));
    }
    GalaxyBangFadeAlpha(input.positionWS, albedo.a);
    // 毛先のグラデーション: 始まり → 終わり(毛先)へ、毛先の色へ寄せる。軸は UV の V か高さ。
    // 高さは「最初の姿勢でのメッシュの高さ」: 頭の動き(上下・回転)を戻した位置の高さ(_TipRestRow。GalaxyToonFace が入れる)。
    // 物体の高さ(positionOS)だと、スキンメッシュはポーズで動いた形の高さになり、しゃがむ・寝ると髪じゅうが毛先の色になる。入っていなければ物体の高さ
    if (_TipColor.a > 0.001h)
    {
        half along = _TipAxis < 0.5h ? input.uv.y : (_TipAxis < 1.5h ? 1.0h - input.uv.y : (half)(dot(_TipRestRow, _TipRestRow) > 1e-8 ? dot(_TipRestRow, float4(input.positionWS, 1.0)) : input.positionOS.y));
        half tip = saturate((along - _TipStart) / (abs(_TipEnd - _TipStart) > 1e-4h ? (_TipEnd - _TipStart) : 1e-4h));
        albedo.rgb = lerp(albedo.rgb, _TipColor.rgb, tip * tip * (3.0h - 2.0h * tip) * _TipColor.a);
    }
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
    #if defined(_NET_DENT)
        GalaxyNetDent(input.uv, normal, tangent, bitangent, albedo.rgb); // 網タイツの食い込み(法線と色)
    #endif
    #if defined(_SHEER)
    {
        // シアー・ストッキング(薄布の肌透け): 正面(N・V が大きい所)ほど素肌の色が透け、輪郭(斜め)ほど繊維の色が濃く締まる(デニールの濃さの見え方)
        half sheerMask = SAMPLE_TEXTURE2D(_SheerMask, sampler_linear_repeat, input.uv).r * _SheerAmount;
        if (sheerMask > 0.001h)
        {
            half ndv = saturate(dot(normal, viewDir));
            half skinVisible = pow(ndv, _SheerPower) * sheerMask;
            albedo.rgb = lerp(albedo.rgb, _SheerSkinColor.rgb * lerp(half3(1, 1, 1), albedo.rgb, 0.25h), skinVisible);
        }
    }
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
        // 頭の向きで前と後ろに分ける: 環境光を頭の前・後ろの 2 方向だけで引き、面が前を向くほど前の色。
        // 面ごとの法線で引くより陰影の向きが落ち着き、首を振っても顔の明るさが急に変わらない
        if (_AmbientHeadSplit > 0.001h)
        {
            float3 headForward = _HeadForward.w > 0.5 ? normalize(_HeadForward.xyz) : faceForward;
            half3 front = SampleSH(headForward);
            half3 back = SampleSH(-headForward);
            ambient = lerp(ambient, lerp(back, front, saturate(dot(normal, headForward) * 0.5h + 0.5h)), _AmbientHeadSplit);
        }
    #endif
    lightColor = GalaxyToonLightColor(lightColor, ambient);

    // ==== 5. 影 ====
    // 光の当たり具合 lambert(0=影 1=光)を作り、落ち影・髪の影・AO を掛けてから、影1〜3の境界で切る
    half3 shadeNormal = dot(input.shadowNormalWS, input.shadowNormalWS) > 0.25 ? (half3)normalize(input.shadowNormalWS) : normal; // 影だけに使う法線があればそれ
    half lambert = _LightSource > 1.5h ? 1.0h : saturate(dot(shadeNormal, lightDir) * 0.5h + 0.5h); // 「影だけ」は面の向きで影にしない(落ち影だけ)
    #if defined(_FACE_SDF)
        lambert = GalaxyFaceSdfLambert(input.uv, lightDir, faceForward);
    #endif
    half lambertShape = lambert; // 面の向き(と SDF 顔影)だけの当たり具合。SSS の明暗境界はこれで測る(落ち影・髪の影・AO の縁に血色を出さない)
    #if defined(_ANIME)
        receivedShadow = lerp(receivedShadow, step(0.5h, receivedShadow), _AnimeHardness); // アニメ塗り: 落ち影もくっきり
    #endif
    half castShadow = receivedShadow;
    // 明暗境界のすぐ明るい側では落ち影を弱める: なめらかな法線では明るい所でも、影のマップは平らな三角形の形で影を落とすので、
    // 低ポリの面の形の帯が出る。N・L が 0〜0.25 の間で落ち影を戻し、境界の近くは影のランプだけで塗る(「影だけ」のときはしない)
    if (_ShadowTerminatorFade > 0.001h && _LightSource < 1.5h)
    {
        castShadow = lerp(1.0h, castShadow, saturate(dot(normal, lightDir) / (0.25h * _ShadowTerminatorFade)));
    }
    #if defined(_FACE_SDF)
        // SDF 顔影: 描いた顔影の上に、影のマップの形(鼻・頬)を重ねない。顔を正面から見るほど落ち影を使わず、横顔では戻す
        castShadow = lerp(castShadow, 1.0h, _FaceSdfIgnoreShadow * smoothstep(0.2h, 0.6h, saturate(dot(faceForward, viewDir))));
    #endif
    lambert *= lerp(1.0h, castShadow, _CastShadowAmount);
    #if defined(_CONTACT_SHADOW)
        // 接地影・自己影は使うと決めて足す影なので、シーンの落ち影を受けない設定(_CastShadowAmount=0)でも掛ける
        if (_LightSource < 1.5h) lambert *= GalaxyContactShadow(input.positionWS, input.positionCS, lightDir, normal, input.uv);
    #endif
    // キャラ専用のシャドウマップ(あれば)。使うと決めて足す影なので、シーンの落ち影を受けない設定(_CastShadowAmount=0)でも掛ける。
    // 明暗境界のすぐ明るい側は、シーンの落ち影と同じく弱める
    if (_LightSource < 1.5h)
    {
        half charShadow = GalaxyCharacterShadow(input.positionWS, normal);
        if (_ShadowTerminatorFade > 0.001h) charShadow = lerp(1.0h, charShadow, saturate(dot(normal, lightDir) / (0.25h * _ShadowTerminatorFade)));
        lambert *= charShadow;
    }
    // 頂点カラー: R が黒いほど影になりやすく、G で境界を手前・奥へ(灰=そのまま)
    lambert *= lerp(1.0h, input.softFxColor.z, _VertexColorShadow);
    lambert = saturate(lambert + (input.softFxColor.w - 0.5h) * _VertexColorBias);
    // スカートの影：スカートの中（腰から裾まで）と、裾のすぐ下の帯の脚を、影1の側へ（体のマテリアルだけ）
    if (_SkirtShadowReceive > 0.001h && _GalaxySkirtShadow.z > 0.001)
    {
        float2 skirtD = input.positionWS.xz - _GalaxySkirtShadowCenter.xy;
        if (dot(skirtD, skirtD) < _GalaxySkirtShadow.w * _GalaxySkirtShadow.w && input.positionWS.y < _GalaxySkirtShadowCenter.z)
        {
            half skirtS = smoothstep(_GalaxySkirtShadow.x - _GalaxySkirtShadow.y, _GalaxySkirtShadow.x, input.positionWS.y);
            lambert = lerp(lambert, min(lambert, saturate(_Shade1Edge - _Shade1Soft - 0.02h)), skirtS * (half)_GalaxySkirtShadow.z * _SkirtShadowReceive);
        }
    }
    #if defined(_HAIR_SHADOW)
        // 髪の影: 影1の境界より確実に暗い所まで下げる
        lambert = lerp(lambert, min(lambert, saturate(_Shade1Edge - _Shade1Soft - 0.02h)), GalaxyHairShadow(input.positionCS, lightDir) * _HairShadowReceive);
    #endif
    // AO マップ(くぼみ・重なり): 暗い所ほど影になりやすくする(影の境界を手前にずらす)
    lambert *= lerp(1.0h, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_linear_repeat, input.uv).r, _OcclusionStrength);
    #if defined(_CREVICE_AO)
        // 奥ほど暗く(画面の深度): 首の下・脇・髪の内側・服の重なりの奥を影の側へ
        lambert *= lerp(1.0h, GalaxyCreviceOcclusion(input.positionWS, input.positionCS, normal), _CreviceStrength);
    #endif
    // 影の出やすさマスク(灰=そのまま 白=影が出にくい 黒=出やすい): 影の境界を手前・奥へずらす。頬の張り・鼻の下などを手で決める
    lambert = saturate(lambert + (SAMPLE_TEXTURE2D(_ShadowBiasMask, sampler_linear_repeat, input.uv).r - 0.5h) * _ShadowBiasStrength);
    half strength = SAMPLE_TEXTURE2D(_ShadeAmountMap, sampler_linear_repeat, input.uv).r; // 影の濃さマスク
    // パック型マスク: G で影の出やすさ(灰=そのまま)。R・B はハイライトで使う
    half4 ilm = _UseIlm > 0.5h ? SAMPLE_TEXTURE2D(_IlmTex, sampler_linear_repeat, input.uv) : half4(1, 0.5h, 1, 0);
    lambert = saturate(lambert + (ilm.g - 0.5h) * _IlmShadowBias * 2.0h);
    // 髪の陰影マップ (HM): G で束ごとの影を光の当たり具合に足す(中立点より暗い所は影へ、明るい所は光へ)。R は天使の輪の範囲(下の異方性で使う)
    half hairMapR = 1.0h;
    if (_HairMapOn > 0.5h)
    {
        half4 hairMap = SAMPLE_TEXTURE2D(_HairMap, sampler_linear_repeat, input.uv);
        lambert = saturate(lambert + (hairMap.g - _HairMapNeutral) * _HairMapStrength);
        hairMapR = hairMap.r;
    }
    float rampRowV = GalaxyRampRowV(input.uv); // 部位ごとのランプの段(影・ハイライトのランプ共通)
    // 影色テクスチャ: 手で塗った影の色(暗くした地の色ではなく、色味を変えた影)。無ければ地の色
    half3 shadeBase = _ShadeMapOn > 0.5h ? SAMPLE_TEXTURE2D(_ShadeMap, sampler_linear_repeat, input.uv).rgb : albedo.rgb;
    half lit1;
    half3 color = GalaxyToonShade(albedo.rgb, shadeBase, lambert, strength, rampRowV, lit1);
    // 漫画調の影：影の所を、ベタではなくハッチング（斜線。暗いほど交差の線を足す）か網点（暗いほど大きな点）で塗る。画面の座標で
    if (_MangaStyle > 0.5h)
    {
        float angle = radians(_MangaAngle);
        float2 dir = float2(cos(angle), sin(angle));
        float2 pixel = input.positionCS.xy;
        half darkness = saturate(1.0h - lit1);
        half ink;
        if (_MangaStyle < 1.5h)
        {
            half line1 = step(frac(dot(pixel, dir) / _MangaSpacing), _MangaWidth);
            half line2 = step(frac(dot(pixel, float2(-dir.y, dir.x)) / _MangaSpacing), _MangaWidth);
            ink = max(line1 * step(0.15h, darkness), line2 * step(0.65h, darkness));
        }
        else
        {
            float2 rotated = float2(dot(pixel, dir), dot(pixel, float2(-dir.y, dir.x))) / _MangaSpacing;
            half radius = sqrt(darkness) * 0.62h;
            ink = step(length(frac(rotated) - 0.5), radius);
        }

        // 影の所は、線・点だけ影の色、ほかは地の色（明るい所はそのまま）
        half3 litColor = albedo.rgb;
        color = lerp(color, lerp(litColor, color, ink), step(0.02h, darkness));
    }
    #if defined(_SSS)
        half sssThickness = SAMPLE_TEXTURE2D(_ThicknessMap, sampler_linear_repeat, input.uv).r;
        color = GalaxyToonApplySssShade(color, albedo.rgb, lambertShape, lit1, strength, sssThickness);
    #endif
    #if defined(_ANIME)
        // アニメ塗り: ライトの色味は残し、明るさのムラで塗った色をくすませない(暗い場面でも色が沈みすぎない)
        half lightMax = max(max(lightColor.r, lightColor.g), max(lightColor.b, 1e-3h));
        lightColor = lerp(lightColor, lightColor / lightMax * clamp(lightMax * 1.15h, 0.55h, 1.0h), _AnimeFlatLight);
    #endif
    color *= lightColor;
    // 影の模様: 物体の座標の斜めの向きに縞(体についてくる)。影の濃さに合わせて線を太く
    if (_ShadowPattern > 0.001h)
    {
        half darkness = saturate(1.0h - lit1);
        float stripe = frac(dot(input.positionOS, float3(0.7071, 0.7071, 0.0)) * _ShadowPatternDensity);
        half widthNow = _ShadowPatternWidth * darkness;
        half aa = (half)max(fwidth(stripe), 1e-4);
        half ink = smoothstep(widthNow + aa, widthNow - aa, (half)stripe) * step(0.02h, darkness);
        color = lerp(color, color * _ShadowPatternColor.rgb, ink * _ShadowPattern);
    }
    // カメラからの補助光: 影の側の、カメラを向いた面だけ(明暗の形は残す)
    if (_FillLightColor.a > 0.001h)
    {
        half facing = saturate(dot(normal, viewDir));
        color += albedo.rgb * _FillLightColor.rgb * (_FillLightColor.a * (1.0h - lit1) * facing * facing) * max(max(lightColor.r, lightColor.g), lightColor.b);
    }
    // 影の側を空の色に寄せる: 光と反対の向きの環境光(SH)の色味を、影の所だけ掛ける。夕焼け・青空で影が空の色になる
    if (_ShadowAmbientTint > 0.001h)
    {
        half3 sky = max(SampleSH(-lightDir), 1e-3h);
        half3 skyTint = sky / max(max(sky.r, sky.g), max(sky.b, 1e-3h));
        color *= lerp(lerp(half3(1, 1, 1), skyTint, _ShadowAmbientTint), half3(1, 1, 1), lit1);
    }

    // ==== 6. 足す光 ====
    // 金属・つや(URP 標準の金属・つやと同じ値の意味)。金属ほど地の色が暗くなり、映り込みが地の色に染まる
    #if defined(_GLOSSY)
        half metallic = _Metallic, smoothness = _Smoothness;
        // 触れるとへこむ: 強く押した所は肌が張って、少しつやが出る
        if (_SoftTouch > 0.5h) smoothness = saturate(smoothness + input.softFxColor.y * 0.15h);
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
    color = min(color, albedo.rgb * _LightCeiling); // 追加ライトを足しても上限を超えない。透過光・光沢・リムはこの後に足す
    #if defined(_SSS)
        // 透過光は _LightCeiling クランプの後に加算し、明るい肌でも逆光・斜光の赤み透過が消されないようにする
        color += GalaxyToonSssTranslucency(albedo.rgb, normal, viewDir, lightDir, lightColor, receivedShadow, sssThickness);
    #endif

    half3 halfDir = normalize(lightDir + viewDir);
    half nh = saturate(dot(normal, halfDir));
    half3 normalVS = mul((float3x3)UNITY_MATRIX_V, normal); // ビュー空間の法線(深度リム・マットキャップ・屈折)

    #if defined(_SPECULAR)
    {
        // ハイライト(段のついた光沢。髪・瞳・金属・革など)。
        // マスク(_SpecularMask)で金具や革だけ光らせ、_SpecularShadowMask を下げると影の中でも金属や革が鈍く光る(トゥーンと PBR の中間の表現)
        half specMask = _SpecularColor.a * SAMPLE_TEXTURE2D(_SpecularMask, sampler_linear_repeat, input.uv).r * lerp(1.0h, lit1, _SpecularShadowMask) * ilm.r * ilm.b;
        if (_SpecularRampOn > 0.5h)
        {
            // ランプで光の形と色を決める: 光の外側(0)→中心(1)の位置でランプを引く(段・縞・色の移り変わりを絵で作れる)
            half u = saturate((nh - (1.0h - _SpecularSize)) / max(_SpecularSize, 1e-3h));
            half3 specRamp = SAMPLE_TEXTURE2D(_SpecularRampTex, sampler_linear_clamp, float2(u, rampRowV)).rgb;
            color += specRamp * _SpecularColor.rgb * lightColor * specMask;
        }
        else
        {
            // チラつきを抑える: 画素あたりの nh の変化だけ境界をぼかす(遠い・細かい所で点滅しない)
            half spec = GalaxyTooning(nh, 1.0h - _SpecularSize, _SpecularSoft + fwidth(nh) * _SpecularAA) * specMask;
            color += _SpecularColor.rgb * lightColor * spec;
        }
    }
    #endif
    #if defined(_SHEER)
    {
        // ストッキング特有の微細な繊維光沢(視線と光のハーフベクトルに対してなだらかに光るシアーシーン)
        if (_SheerSheen > 0.001h)
        {
            half sheerMask = SAMPLE_TEXTURE2D(_SheerMask, sampler_linear_repeat, input.uv).r * _SheerAmount;
            half sheen = pow(nh, 6.0h) * (1.0h - saturate(dot(normal, viewDir)) * 0.5h) * _SheerSheen * sheerMask * lerp(0.35h, 1.0h, lit1);
            color += lerp(albedo.rgb, _SheerSkinColor.rgb, 0.5h) * lightColor * sheen;
        }
    }
    #endif

    #if defined(_ANISOTROPY)
    {
        // 異方性ハイライト(髪の天使の輪): 髪の流れ(接線か従法線)に沿った細長い光。向きを法線側へずらして位置を動かす。
        // _AnisoJitterStrength > 0 のときは毛束(流れと直交する UV 方向)に沿って位置を上下に波打たせ、板ポリ感のない繊細な毛束ツヤにする
        float3 strand = _AnisoDirection < 0.5h ? tangent : bitangent;
        float jitter = 0.0;
        if (_AnisoJitterStrength > 0.001h)
        {
            float2 juv = _AnisoDirection < 0.5h ? input.uv.yx : input.uv.xy;
            float jn = GalaxyValueNoise2(float2(juv.x * _AnisoJitterScale, juv.y * _AnisoJitterScale * 0.15)) * 0.65
                     + GalaxyValueNoise2(float2(juv.x * _AnisoJitterScale * 2.4 + 3.7, juv.y * _AnisoJitterScale * 0.3)) * 0.35;
            jitter = (jn * 2.0 - 1.0) * _AnisoJitterStrength;
        }
        float3 shifted = normalize(strand + normal * (_AnisoShift + jitter));
        half th = dot(shifted, halfDir);
        float3 shifted2 = normalize(strand + normal * (_Aniso2Shift + jitter * 0.8));
        half th2 = dot(shifted2, halfDir);
        // ピクセルあたりの変化(分岐の外で取る)。細い輪がピクセルより細くなってちらつかないように、その分だけ太らせる
        half thWidth = fwidth(th), th2Width = fwidth(th2);
        // 濡れ髪: 輪は細く鋭く・強く、束の山にだけ乗る(溝では消える)
        // 形を正面ほど丸く・縁ほど細長く: 見る向きに近い所は鋭さを上げて短く、縁は下げて流れに沿って伸ばす
        half centerShape = lerp(1.0h, lerp(0.6h, 1.6h, saturate(dot(normal, viewDir))), _AnisoCenterShape);
        half aniso = GalaxyStrandLobe(th, _AnisoSharpness * lerp(1.0h, 2.5h, hairWet) * centerShape, thWidth);
        if (_RingMode > 2.5h)
        {
            // 髪の陰影マップの R: 異方性の輪を、描いた範囲(頭の上の帯)だけに出す。影の中では下の _AnisoShadowMask で消える
            aniso *= hairMapR;
        }
        else if (_RingMode > 0.5h)
        {
            // マットキャップ: 見る向きの空間の法線で、輪を描いた球の画像を引く(光の向きに左右されない。頭の上の決まった高さに輪)
            // UV2 の帯: 2 番目の UV の v(髪の上から毛先へ)に、見る高さでずらした位置の帯の濃さ(横は u)
            half3 ringTex;
            if (_RingMode < 1.5h)
            {
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normal);
                float2 muv = normalVS.xy * 0.5 + 0.5 + float2(0, _RingOffset * 0.5 + jitter * 0.2);
                ringTex = SAMPLE_TEXTURE2D(_RingTex, sampler_linear_clamp, muv).rgb;
            }
            else
            {
                float v = input.uv2.y + _RingOffset + viewDir.y * _RingViewShift + jitter * 0.2;
                ringTex = SAMPLE_TEXTURE2D(_RingTex, sampler_linear_clamp, float2(input.uv2.x, v)).rgb;
            }
            aniso = max(ringTex.r, max(ringTex.g, ringTex.b));
        }
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
        // 2本目のサブ天使の輪(2段のハイライト帯)
        if (_Aniso2Color.a > 0.001h)
        {
            half aniso2 = GalaxyStrandLobe(th2, _Aniso2Sharpness * lerp(1.0h, 2.0h, hairWet), th2Width);
            half aniso2Mask = _Aniso2Color.a * hairClump * lerp(1.0h, lit1, _AnisoShadowMask);
            color += _Aniso2Color.rgb * lightColor * GalaxyTooning(aniso2, _AnisoBorder, _AnisoBlur * 0.7h) * aniso2Mask;
        }
    }
    #endif

    #if defined(_BACKLIGHT)
    {
        // 逆光ライト: 光がカメラの反対側(キャラの後ろ)にあるとき、縁が光る
        half directivity = pow(saturate(-dot(viewDir, lightDir) * 0.5h + 0.5h), _BackLightFocus);
        half ln = dot(normalize(-viewDir * _BackLightViewMix + lightDir), normal) * 0.5h + 0.5h;
        half back = GalaxyTooning(ln, _BackLightEdge, _BackLightSoft) * directivity * _BackLightTint.a;
        color += _BackLightTint.rgb * lerp(half3(1, 1, 1), albedo.rgb, _BackLightAlbedoMix) * lightColor * back;
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
        // クリアコート(二層塗装・エナメルの透明ニス層): 下地の色や金属感・アニメ塗りのツヤ消し(_AnimeNoGloss)に関わらず、明示的に設定されたら表面の鋭いハイライトとフレネル反射を乗せる
        if (_ClearCoat > 0.001h)
        {
            half ccSmooth = _ClearCoatSmoothness;
            half ccSpec = GalaxyTooning(nh, 1.0h - lerp(0.15h, 0.003h, ccSmooth), 0.008h + 0.03h * (1.0h - ccSmooth)) * ccSmooth;
            half ccFresnel = 0.04h + 0.96h * pow(1.0h - saturate(dot(normal, viewDir)), 5.0h) * ccSmooth;
            half3 ccRefl = GlossyEnvironmentReflection(reflectDir, input.positionWS, 1.0h - ccSmooth, 1.0h, GetNormalizedScreenSpaceUV(input.positionCS));
            if (_ReflectionSteps > 0.5h) ccRefl = floor(ccRefl * _ReflectionSteps + 0.5h) / _ReflectionSteps;
            color += (lightColor * ccSpec * lerp(0.25h, 1.0h, lit1) + ccRefl * ccFresnel * _ReflectionStrength) * _ClearCoat;
        }
        // ベルベット・起毛布(逆フレネル毛羽立ち): 輪郭に向かって布の表面が柔らかく白み、高級なフェルト・ベルベット・ピーチスキン感を出す
        if (_VelvetColor.a > 0.001h)
        {
            half velvetRim = pow(1.0h - saturate(dot(normal, viewDir)), _VelvetPower);
            half velvetLit = lerp(0.35h, 1.0h, lit1);
            color += lerp(albedo.rgb, _VelvetColor.rgb, 0.7h) * lightColor * (velvetRim * _VelvetColor.a * velvetLit);
        }
    }
    #endif

    #if defined(_GLITTER)
        color += GalaxyGlitter(input.uv, normal, tangent, bitangent, viewDir, lightDir) * lightColor * lerp(1.0h, lit1, _GlitterShadeMask);
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
        // しずくを立体に見せる(アニメのしずくの描き方): 下の縁は暗く、下側の中は光が集まって明るく、左上に白い光の点
        color *= 1.0h - fx.dropRim * 0.45h;
        color += lightColor * fx.dropCaustic * 0.35h;
        color = lerp(color, max(color, lightColor * 1.2h + 0.25h), fx.dropHighlight);
    }
    #endif

    #if defined(_RIMLIGHT)
    {
        // リムライト(縁の光)。影の側では弱める
        half rim = pow(saturate(1.0h - abs(dot(normal, viewDir))), _RimFalloff);
        rim = GalaxyTooning(rim, _RimEdge, _RimSoft);
        #if defined(_DEPTH_RIM)
            // 深度リム: 法線の向きへ少しずらした画面の深度が、自分より十分奥なら「輪郭のすぐ内側」として光らせる(シルエットだけが光る)
            float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
            float selfDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
            // 幅は cm(ワールド)。透視投影で画面上の UV のずれに直す。
            // 絶対値: テクスチャ描画で投影が上下反転しても(画面 UV 側で補正済み)向きを変えない
            float2 offset = normalVS.xy * (_RimDepthWidth * 0.01 / max(selfDepth, 0.1)) * abs(float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11)) * 0.5;
            float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV + offset), _ZBufferParams);
            if (_RimDepthSoft > 0.001h)
            {
                // やわらかい深度リム: 3 か所(0.6・1・1.4 倍のずらし)で読み、しきい値のまわりをなめらかに
                float soft = max(_RimDepthSoft * _RimDepthThreshold, 1e-4);
                float d0 = sceneDepth - selfDepth;
                float d1 = LinearEyeDepth(SampleSceneDepth(screenUV + offset * 0.6), _ZBufferParams) - selfDepth;
                float d2 = LinearEyeDepth(SampleSceneDepth(screenUV + offset * 1.4), _ZBufferParams) - selfDepth;
                rim *= (smoothstep(_RimDepthThreshold - soft, _RimDepthThreshold + soft, d0) +
                        smoothstep(_RimDepthThreshold - soft, _RimDepthThreshold + soft, d1) +
                        smoothstep(_RimDepthThreshold - soft, _RimDepthThreshold + soft, d2)) / 3.0;
            }
            else
            {
                rim *= step(_RimDepthThreshold, sceneDepth - selfDepth);
            }
        #endif
        // ライト方向への絞り(_RimLightAlign)と、反対側(影側)の環境色リム(_RimOppositeColor)
        half ndl01 = saturate(dot(normal, lightDir) * 0.5h + 0.5h);
        half alignFactor = _RimLightAlign >= 0.0h ? lerp(1.0h, ndl01, _RimLightAlign) : lerp(1.0h, 1.0h - ndl01, -_RimLightAlign);
        half mainRim = rim * _RimColor.a * lerp(1.0h, lit1, _RimShadeFade) * alignFactor * (1.0h + GalaxyAudioLevel() * _AudioRim) * GalaxyVolumeRim();
        color += _RimColor.rgb * GalaxyIlmLut(input.uv, 1).rgb * lightColor * mainRim;
        if (_RimOppositeColor.a > 0.001h)
        {
            half oppRim = rim * _RimOppositeColor.a * (1.0h - ndl01);
            color += _RimOppositeColor.rgb * lightColor * oppRim;
        }
    }
    #endif

    // 後ろからの光: 光の来る側の輪郭だけを、くっきりした細い帯で明るくする(アニメの逆光の縁。リムの設定が無いマテリアルにも効く)
    if (_GalaxyBackLightColor.a > 0.001h)
    {
        half3 backDir = (half3)normalize(_GalaxyBackLightDir.xyz + float3(0, 1e-5, 0));
        half edge = pow(saturate(1.0h - abs(dot(normal, viewDir))), 1.5h) * saturate(dot(normal, backDir) + 0.2h);
        half band = smoothstep(0.3h, 0.36h, edge);
        color += lerp(half3(1, 1, 1), albedo.rgb, 0.5h) * _GalaxyBackLightColor.rgb * (_GalaxyBackLightColor.a * band);
    }

    // リムシェード: 輪郭の内側を掛け算で暗く・色味を変えて締める(加算のリムとは別)
    if (_RimDarkTint.a > 0.001h)
    {
        half f = pow(saturate(1.0h - abs(dot(normal, viewDir))), _RimShadePower);
        f = GalaxyTooning(f, _RimDarkEdge, _RimDarkSoft);
        color = lerp(color, color * _RimDarkTint.rgb, f * _RimDarkTint.a);
    }

    // 虹色の薄膜: 見る角度で色相が回る(シャボン・油膜・玉虫)。縁ほど強い
    if (_IridescenceStrength > 0.001h)
    {
        half nv = saturate(dot(normal, viewDir));
        half hue = frac(nv * _IridescenceScale + _IridescenceShift);
        color += GalaxyHsvToRgb(half3(hue, 0.65h, 1.0h)) * lightColor * (_IridescenceStrength * pow(1.0h - nv, 1.5h));
    }

    // マットキャップ: ビュー空間の法線で球テクスチャを引く
    half2 matCapUV = normalVS.xy * 0.5h + 0.5h;
    if (_MatCapViewFix > 0.5h)
    {
        // 歪みの補正: 視線の向きを基準に法線を組み直す(画面の端でも反射がゆがまない)
        float3 eyeVS = normalize(mul((float3x3)UNITY_MATRIX_V, -viewDir));
        float3 bx = normalize(float3(-eyeVS.z, 0, eyeVS.x));
        float3 by = cross(eyeVS, bx);
        matCapUV = float2(dot(bx, normalVS), dot(by, normalVS)) * 0.5 + 0.5;
    }
    if (_MatCapNoRoll > 0.5h)
    {
        // カメラの傾き(ロール)を打ち消す: ワールドの上がビュー空間で傾いた分だけ回し戻す
        float3 upVS = mul((float3x3)UNITY_MATRIX_V, float3(0, 1, 0));
        float roll = atan2(upVS.x, upVS.y);
        float s = sin(roll), c = cos(roll);
        float2 d = matCapUV - 0.5;
        matCapUV = float2(d.x * c - d.y * s, d.x * s + d.y * c) + 0.5;
    }
    #if defined(_MATCAP)
        half3 matCap = SAMPLE_TEXTURE2D(_MatCapTex, sampler_linear_clamp, matCapUV).rgb * _MatCapColor.rgb * _MatCapColor.a * SAMPLE_TEXTURE2D(_MatCapMask, sampler_linear_repeat, input.uv).r;
        color += matCap * lightColor * lerp(1.0h, lit1, _MatCapShadeMask);
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
    {
        half3 emission = GalaxyEmission(input.uv);
        half4 ilmGlow = GalaxyIlmLut(input.uv, 3);
        emission += albedo.rgb * ilmGlow.rgb * ilmGlow.a; // 素材 ID ごとの光る色(ブルームのかかる量)
        emission *= 1.0h + GalaxyAudioLevel() * _AudioEmission; // 音に合わせて強く
        // 発光の演出: 色相を時間で回す、見る角度(縁ほど)で別の色へ
        if (_EmissionHueSpeed > 0.001h)
        {
            half3 hsv = GalaxyRgbToHsv(emission);
            hsv.x = frac(hsv.x + _Time.y * _EmissionHueSpeed);
            emission = GalaxyHsvToRgb(hsv);
        }
        if (_EmissionViewColor.a > 0.001h)
        {
            half edge = 1.0h - saturate(dot(normal, viewDir));
            emission = lerp(emission, max(max(emission.r, emission.g), emission.b) * _EmissionViewColor.rgb, edge * _EmissionViewColor.a);
        }
        color += emission;
    }
    #endif
    color += layerGlow;
    // 星空
    if (_StarryStrength > 0.001h) color += GalaxyStarry(input.positionCS, viewDir, input.uv);
    // 鼻線: 横顔のときだけ、鼻筋の線を出す(正面では消える)
    if (_NoseLineColor.a > 0.001h)
    {
        half side = 1.0h - abs(dot(faceForward, viewDir));
        half noseMask = SAMPLE_TEXTURE2D(_NoseLineMask, sampler_linear_repeat, input.uv).r;
        color = lerp(color, _NoseLineColor.rgb * lightColor, noseMask * smoothstep(_NoseLineSide, _NoseLineSide + 0.15h, side) * _NoseLineColor.a);
    }
    #if defined(_EYE)
        color += GalaxyEyeGlow(eye) * lerp(half3(1, 1, 1), lightColor, 0.5h); // 瞳のハイライトは暗い場面で少し落とす
    #endif
    #if defined(_DISSOLVE)
        color = lerp(color, _DissolveEdgeColor.rgb, dissolveEdge * _DissolveEdgeColor.a); // ディゾルブの境目
    #endif

    color = GalaxyVolumeAdjust(color, lit1); // Volume でまとめて調整
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
    #if defined(_ALPHATEST_ON)
        if (_AlphaToMask > 0.5h) return half4(color, saturate((albedo.a - _Cutoff) / max(fwidth(albedo.a), 1e-4h) + 0.5h));
    #endif
    return half4(color, albedo.a);
}

#endif
