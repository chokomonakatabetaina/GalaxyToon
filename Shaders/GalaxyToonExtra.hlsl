// GalaxyShader/Toon の「瞳・ラメ・髪の影」。GalaxyToonFx.hlsl(ハッシュ関数)の後に読み込む。
#ifndef GALAXY_TOON_EXTRA_INCLUDED
#define GALAXY_TOON_EXTRA_INCLUDED

// ---------------------------------------------------------------- 網タイツの食い込み
// 地のテクスチャに描かれた網(暗い線、またはアルファ)を見つけ、網の線で肌が押されて沈み、網目の間がふくらんで見えるようにする。
// - 法線: 網の線の近さ(線のまわりをぼかした値)を「へこみ」とみなし、その傾きで法線を傾ける(光の当たり方で立体に見える)
// - 色: 網のすぐ際は押されてできる陰で暗く、網目の真ん中は少し明るく(アニメ塗りで法線の凹凸を弱めても見える)
// 網の線そのもの(網の糸)には掛けない。_NetDentMask の白い所だけに出す。
#if defined(_NET_DENT)
half GalaxyNetMask(float2 uv)
{
    half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    half lum = dot(c.rgb, half3(0.299h, 0.587h, 0.114h));
    half byLines = saturate((_NetThreshold - lum) / max(_NetThreshold * 0.5h, 0.02h)) * c.a;
    return _NetSource > 0.5h ? c.a : byLines;
}

void GalaxyNetDent(float2 uv, inout half3 normal, float3 tangent, float3 bitangent, inout half3 albedo)
{
    half area = SAMPLE_TEXTURE2D(_NetDentMask, sampler_linear_repeat, uv).r;
    if (area <= 0.001h) return;
    float2 d = _BaseMap_TexelSize.xy * _NetDentWidth;
    half c = GalaxyNetMask(uv);
    half l = GalaxyNetMask(uv - float2(d.x, 0)), r = GalaxyNetMask(uv + float2(d.x, 0));
    half b = GalaxyNetMask(uv - float2(0, d.y)), t = GalaxyNetMask(uv + float2(0, d.y));
    half lb = GalaxyNetMask(uv - d), rt = GalaxyNetMask(uv + d);
    half lt = GalaxyNetMask(uv + float2(-d.x, d.y)), rb = GalaxyNetMask(uv + float2(d.x, -d.y));
    // 網の近さ(0=網目の真ん中 1=網の線の上)と、その傾き(ソーベル)
    half nearNet = saturate((c * 4.0h + (l + r + b + t) * 2.0h + (lb + rt + lt + rb)) / 16.0h * 1.6h);
    half gx = ((r - l) * 2.0h + (rt - lt) + (rb - lb)) * 0.25h;
    half gy = ((t - b) * 2.0h + (rt - rb) + (lt - lb)) * 0.25h;
    half skin = (1.0h - c) * area; // 網の糸そのものには掛けない
    half depth = _NetDentStrength;
    #if defined(_ANIME)
        depth *= 1.0h - _AnimeFlatNormal * 0.5h; // アニメ塗り: 細かい凹凸でザラつかないよう弱める(色の陰影は残す)
    #endif
    // 高さ = -網の近さ(網の線で沈む)。高さの傾きの逆向きへ法線を倒すと、網に向かって下がる面になる
    normal = normalize(normal + (half3)(tangent * gx + bitangent * gy) * (depth * skin));
    half edge = saturate(nearNet * 2.2h) * skin;                  // 網のすぐ際(押されて沈む所)
    half bulge = saturate(1.0h - nearNet * 3.0h) * skin;          // 網目の真ん中(ふくらむ所)
    albedo = lerp(albedo, albedo * _NetDentColor.rgb, edge * _NetDentShadow);
    albedo += albedo * bulge * _NetDentHighlight * 0.5h;
}
#endif

// ---------------------------------------------------------------- 髪の影
// 画面上で、光の来る向きへ少しずらした所に「自分より手前の髪」があれば影。
// 髪かどうかは URP の法線テクスチャの A(髪のマテリアルが DepthNormals パスで 1 を書く)、手前かどうかは URP の深度テクスチャで見る。
// 自前のテクスチャは使わない(別テクスチャをグローバルに渡す方式は D3D12 でクラッシュした)。ずらす量は cm(ワールド)を画面の量に直す。
// 境界のジャギーを防ぐため、_HairShadowSoftness > 0 のときは回転グリッドの複数点サンプル(PCF)でなめらかにぼかす。
#if defined(_HAIR_SHADOW)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

half GalaxyHairShadowSample(float2 uv, float selfDepth, float bias)
{
    uv = saturate(uv);
    half isHair = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_point_clamp, uv).a;
    float hairDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
    // 手前に髪があり、かつ離れすぎていない(背景や別キャラの誤判定を防ぐため 25cm 以内に制限)
    return (isHair > 0.5h && hairDepth < selfDepth - bias && selfDepth - hairDepth < 0.25) ? 1.0h : 0.0h;
}
#endif

half GalaxyHairShadow(float4 positionCS, half3 lightDir)
{
    half result = 0;
    #if defined(_HAIR_SHADOW)
    UNITY_BRANCH
    if (_GalaxyHairShadowOn > 0.5) // 法線プリパスの無いカメラでは読まない
    {
        float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
        float selfDepth = LinearEyeDepth(positionCS.z, _ZBufferParams);
        float3 lightVS = mul((float3x3)UNITY_MATRIX_V, lightDir);
        float2 offset = lightVS.xy * (_HairShadowDistance * 0.01 / max(selfDepth, 0.1)) * abs(float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11)) * 0.5;
        float2 baseUV = screenUV + offset;
        float bias = _HairShadowBias * 0.01;
        if (_HairShadowSoftness <= 0.01h)
        {
            result = GalaxyHairShadowSample(baseUV, selfDepth, bias);
        }
        else
        {
            // 画面ピクセルサイズ(_ScreenParams.zw - 1.0) × ぼかし半径で回転グリッド4点サンプル + 中心
            float2 texel = (_ScreenParams.zw - 1.0) * _HairShadowSoftness;
            half sum = GalaxyHairShadowSample(baseUV, selfDepth, bias) * 2.0h;
            sum += GalaxyHairShadowSample(baseUV + float2( 0.85,  0.45) * texel, selfDepth, bias);
            sum += GalaxyHairShadowSample(baseUV + float2(-0.45,  0.85) * texel, selfDepth, bias);
            sum += GalaxyHairShadowSample(baseUV + float2(-0.85, -0.45) * texel, selfDepth, bias);
            sum += GalaxyHairShadowSample(baseUV + float2( 0.45, -0.85) * texel, selfDepth, bias);
            result = sum / 6.0h;
        }
    }
    #endif
    return result;
}

// ---------------------------------------------------------------- 頬染め(照れ)
// 顔テクスチャの UV 上の2点(_BlushUV)を中心に、横長の楕円をぼかして色を重ねる。照れ線は斜めの細い線を楕円の中に重ねる
half3 GalaxyBlush(half3 albedo, float2 uv)
{
    half m = 0;
    [unroll] for (int i = 0; i < 2; i++)
    {
        float2 c = i == 0 ? _BlushUV.xy : _BlushUV.zw;
        if (all(c == 0)) continue;
        float2 d = (uv - c) / max(_BlushSize, 1e-4);
        d.y *= 1.5;                                                   // 横長
        float r = length(d);
        m = max(m, 1.0h - smoothstep(saturate(1.0h - _BlushSoft), 1.0h, r));
    }
    m *= _BlushAmount;
    albedo = lerp(albedo, _BlushColor.rgb, m * _BlushColor.a);
    if (_BlushLines > 0.001h)
    {
        float wave = sin((uv.x + uv.y) * 6.2832 / max(_BlushSize * 0.28, 1e-4));   // 斜線
        half stripe = smoothstep(0.55, 0.85, wave) * step(0.25h, m / max(_BlushAmount, 1e-4));  // 楕円の中だけ、はっきりした線
        albedo = lerp(albedo, _BlushColor.rgb * 0.75h, stripe * _BlushLines * _BlushAmount);
    }
    return albedo;
}

// ---------------------------------------------------------------- 瞳
// 瞳は顔テクスチャの中の円(UV の長方形 _EyeRect1/2 に内接)。その中だけ、
//   奥行き: 見る角度で瞳の模様を奥へずらす(角膜の下にあるように見える)
//   ハイライト: 画面に対して決まった位置(右上など)に大小2つ。角膜の上なので模様とは逆向きに少し動く
//   うるうる: ハイライトが小刻みに揺れ、瞳の下に揺れる水面の光
//   きらめき: 十字の星がランダムにまたたく
struct GalaxyEyeData
{
    float2 local;   // 瞳の中の位置(テクスチャの向き。-1〜1。+Y がテクスチャの上 = まぶたの側)
    float2 uv;      // 奥行きでずらした UV(ここで模様を引く)
    half inside;    // 瞳の中=1(縁はなめらかに)
    float2 q;       // 瞳の中の位置(画面の右=+X 上=+Y、-1〜1)
    float2 view;    // 視線の傾き(同じ向き)
};

GalaxyEyeData GalaxyEyeSetup(float2 uv, half3 viewDir, float3 tangent, float3 bitangent, half3 normal)
{
    GalaxyEyeData e;
    e.uv = uv; e.inside = 0; e.q = 0; e.view = 0; e.local = 0;
    bool in1 = all(uv >= _EyeRect1.xy) && all(uv <= _EyeRect1.zw);
    float4 rect = in1 ? _EyeRect1 : _EyeRect2;
    float2 size = rect.zw - rect.xy;
    if (size.x <= 1e-5 || size.y <= 1e-5) return e;
    float2 local = (uv - rect.xy) / size * 2.0 - 1.0;           // UV の向きのまま -1〜1
    e.local = local;
    float r = length(local);
    if (r >= 1.0) return e;
    e.inside = 1.0h - smoothstep(0.88h, 1.0h, r);

    // 画面の右・上を UV の向きに写す(テクスチャ上で瞳が回転・反転していても画面基準で置ける)
    float2 rightUV = float2(dot(UNITY_MATRIX_V[0].xyz, tangent), dot(UNITY_MATRIX_V[0].xyz, bitangent));
    float2 upUV = float2(dot(UNITY_MATRIX_V[1].xyz, tangent), dot(UNITY_MATRIX_V[1].xyz, bitangent));
    rightUV *= rsqrt(max(dot(rightUV, rightUV), 1e-6));
    upUV *= rsqrt(max(dot(upUV, upUV), 1e-6));
    e.q = float2(dot(local, rightUV), dot(local, upUV));

    // 奥行き: 接空間の視線で模様をずらす(中心ほど深い)
    float3 viewTS = float3(dot(viewDir, tangent), dot(viewDir, bitangent), dot(viewDir, normal));
    float2 shift = viewTS.xy / max(viewTS.z, 0.35) * (_EyeParallax * 0.3 * (1.0 - r * r));
    e.uv = clamp(uv - shift * size * 0.5, rect.xy, rect.zw);
    e.view = float2(dot(viewTS.xy, rightUV), dot(viewTS.xy, upUV));
    return e;
}

/// まぶたの影と、瞳の光の溜まり(地の色に掛ける)。まぶたの影は瞳の上の縁(テクスチャの上)から、光の溜まりは光と反対の側の下に
half3 GalaxyEyeShade(GalaxyEyeData e, half3 albedo, half3 lightDir)
{
    if (e.inside <= 0.0h) return albedo;
    // まぶたの影: 上の縁から幅の分、なめらかに暗く(縁に近いほど濃い)
    // 瞳の絵は UV の長方形の真ん中の一部なので、上の縁は「瞳の大きさ」(_EyeIrisRadius。長方形の半分を 1)で決める
    half radius = max(_EyeIrisRadius, 0.05h);
    half y = (half)e.local.y / radius;
    half width = max(_EyeLidShadowWidth, 0.01h);
    half lid = smoothstep(1.0h - width * (1.0h + _EyeLidShadowSoft), 1.0h - width * (1.0h - _EyeLidShadowSoft * 0.5h), y);
    albedo = lerp(albedo, albedo * _EyeLidShadowColor.rgb, lid * _EyeLidShadowColor.a * e.inside);
    // 光の溜まり: 光の向きを画面に写し、その反対の側・下ほど明るい三日月
    float2 lightScreen = float2(dot(UNITY_MATRIX_V[0].xyz, lightDir), dot(UNITY_MATRIX_V[1].xyz, lightDir));
    float len = length(lightScreen);
    float2 away = len > 1e-3 ? -lightScreen / len : float2(0, -1);
    away = normalize(away + float2(0, -0.8)); // 下へ寄せる(光の溜まりは瞳の下にできる)
    half size = max(_EyeCausticSize, 0.05h);
    float2 qn = e.q / radius;
    half caustic = smoothstep(1.0h - size, 1.0h, (half)dot(qn, away) * 0.5h + 0.5h) * (1.0h - smoothstep(0.75h, 0.98h, (half)length(qn)));
    caustic *= 1.0h - lid; // まぶたの影の中には出さない
    return albedo + albedo * _EyeCausticColor.rgb * (_EyeCausticColor.a * caustic * e.inside) * 1.5h;
}

half3 GalaxyEyeGlow(GalaxyEyeData e)
{
    if (e.inside <= 0.0h) return 0;
    float t = _Time.y;
    // うるうる: 小刻みな揺れ(2つの sin を重ねて不規則に)
    float2 wobble = _EyeWet * 0.035 * float2(sin(t * 9.1) + sin(t * 5.7 + 1.3), sin(t * 7.3 + 0.7) + sin(t * 4.1));
    float2 p = e.q + e.view * 0.12 + wobble;
    half size = _EyeHighlightSize * (1.0h + 0.12h * _EyeWet * sin(t * 6.0));
    half big = smoothstep(size, size * 0.65h, length((p - _EyeHighlightPos.xy) * float2(1.0, 1.15)));
    half small = smoothstep(size * 0.5h, size * 0.3h, length(p - _EyeHighlightPos.zw));
    // 水面の光: 瞳の下の三日月。横に波打ちながら揺れる
    float wave = sin(e.q.x * 9.0 + t * 3.0) * 0.05;
    half lower = smoothstep(0.5h, 0.72h, -e.q.y + wave) * (1.0h - smoothstep(0.82h, 0.98h, length(e.q)));
    half3 glow = _EyeHighlightColor.rgb * _EyeHighlightColor.a * saturate(big + small)
               + _EyeHighlightColor.rgb * lower * _EyeWet * 0.7h;
    // うるうるの細かい光の粒(揺れる)
    if (_EyeWet > 0.01h)
    {
        float2 dp = e.q * 5.0 + float2(0, t * 0.3);
        float2 c = floor(dp);
        float2 rnd = GalaxyHash22(c + 3.3);
        float2 d = frac(dp) - (0.3 + 0.4 * rnd) - 0.03 * float2(sin(t * 8.0 + rnd.x * 6.0), cos(t * 7.0 + rnd.y * 6.0));
        glow += _EyeHighlightColor.rgb * smoothstep(0.09h, 0.04h, length(d)) * step(0.55, rnd.y) * _EyeWet * 0.6h;
    }
    // きらめき: 十字の星。セルごとに位相をずらしてまたたく
    if (_EyeSparkleColor.a > 0.0h)
    {
        float2 sp = e.q * _EyeSparkleScale;
        float2 cell = floor(sp);
        half star = 0;
        [loop] for (int y = -1; y <= 1; y++)
        [loop] for (int x = -1; x <= 1; x++)
        {
            float2 c = cell + float2(x, y);
            float2 rnd = GalaxyHash22(c + 11.7);
            float2 d = sp - (c + 0.2 + 0.6 * rnd);
            float tw = saturate(sin(t * _EyeSparkleSpeed * (0.7 + rnd.x * 1.6) + rnd.y * 6.2832));
            tw = tw * tw * tw;
            float ax = abs(d.x), ay = abs(d.y);
            half rays = (0.03 / (ax * 30.0 + 0.03)) * saturate(1.0 - ay * 3.0) + (0.03 / (ay * 30.0 + 0.03)) * saturate(1.0 - ax * 3.0);
            star += rays * 0.5h * tw * saturate(1.0 - length(d) * 2.2);
        }
        glow += _EyeSparkleColor.rgb * _EyeSparkleColor.a * saturate(star);
    }
    return glow * e.inside;
}

// ---------------------------------------------------------------- ラメ
// UV のマスごとに小さな鏡の粒(向きがランダムに傾く)を置き、光を視線へ反射した粒だけが光る。動くとまたたく
half3 GalaxyGlitter(float2 uv, half3 normal, float3 tangent, float3 bitangent, half3 viewDir, half3 lightDir)
{
    half mask = SAMPLE_TEXTURE2D(_GlitterMask, sampler_linear_repeat, uv).r;
    if (mask <= 0.001h) return 0;
    float2 p = uv * _GlitterScale;
    float2 cell = floor(p);
    float2 f = frac(p);
    half sum = 0;
    [loop] for (int y = -1; y <= 1; y++)
    [loop] for (int x = -1; x <= 1; x++)
    {
        float2 o = float2(x, y);
        float2 c = cell + o;
        float2 r1 = GalaxyHash22(c), r2 = GalaxyHash22(c + 19.19);
        if (r2.x > _GlitterDensity) continue;
        half dotShape = saturate(1.0 - length(f - (o + 0.2 + 0.6 * r1)) / _GlitterSize);
        float2 tilt = (GalaxyHash22(c + 7.7) * 2.0 - 1.0) * 0.6;
        float3 flake = normalize(normal + tangent * tilt.x + bitangent * tilt.y);
        half spark = pow(saturate(dot(reflect(-lightDir, flake), viewDir)), _GlitterSharpness * 6.0);
        sum += dotShape * spark;
    }
    return _GlitterTint.rgb * _GlitterTint.a * saturate(sum * 3.0h) * mask;
}

#endif
