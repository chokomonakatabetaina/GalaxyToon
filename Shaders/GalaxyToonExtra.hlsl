// GalaxyShader/Toon の「瞳・ラメ・髪の影」。GalaxyToonFx.hlsl(ハッシュ関数)の後に読み込む。
#ifndef GALAXY_TOON_EXTRA_INCLUDED
#define GALAXY_TOON_EXTRA_INCLUDED

// ---------------------------------------------------------------- 髪の影
// 画面上で、光の来る向きへ少しずらした所に「自分より手前の髪」があれば影。
// 髪かどうかは URP の法線テクスチャの A(髪のマテリアルが DepthNormals パスで 1 を書く)、手前かどうかは URP の深度テクスチャで見る。
// 自前のテクスチャは使わない(別テクスチャをグローバルに渡す方式は D3D12 でクラッシュした)。ずらす量は cm(ワールド)を画面の量に直す。
#if defined(_HAIR_SHADOW)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
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
        float2 uv = saturate(screenUV + offset);
        half isHair = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_point_clamp, uv).a;
        float hairDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
        result = (isHair > 0.5h && hairDepth < selfDepth - _HairShadowBias * 0.01) ? 1.0h : 0.0h;
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
    float2 uv;      // 奥行きでずらした UV(ここで模様を引く)
    half inside;    // 瞳の中=1(縁はなめらかに)
    float2 q;       // 瞳の中の位置(画面の右=+X 上=+Y、-1〜1)
    float2 view;    // 視線の傾き(同じ向き)
};

GalaxyEyeData GalaxyEyeSetup(float2 uv, half3 viewDir, float3 tangent, float3 bitangent, half3 normal)
{
    GalaxyEyeData e;
    e.uv = uv; e.inside = 0; e.q = 0; e.view = 0;
    bool in1 = all(uv >= _EyeRect1.xy) && all(uv <= _EyeRect1.zw);
    float4 rect = in1 ? _EyeRect1 : _EyeRect2;
    float2 size = rect.zw - rect.xy;
    if (size.x <= 1e-5 || size.y <= 1e-5) return e;
    float2 local = (uv - rect.xy) / size * 2.0 - 1.0;           // UV の向きのまま -1〜1
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
    return _GlitterColor.rgb * _GlitterColor.a * saturate(sum * 3.0h) * mask;
}

#endif
