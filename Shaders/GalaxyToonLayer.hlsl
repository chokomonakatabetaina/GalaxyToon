// GalaxyShader/Toon のレイヤーテクスチャ(最大3枚)。メインの上に重ねる模様・柄・光の帯など。
// それぞれ: 合成(通常/加算/スクリーン/乗算)・スクロールと回転のアニメ・フェード(手動の量 × 明滅)・マスク・影を受けるか(受けない=光る)。
#ifndef GALAXY_TOON_LAYER_INCLUDED
#define GALAXY_TOON_LAYER_INCLUDED

// UV を中心(0.5,0.5)まわりに回してから、スクロールする。animate が 0 なら動かさない(向き・回転だけ使う)
float2 GalaxyLayerUV(float2 uv, float4 st, float4 scroll, half animate)
{
    float t = _Time.y * animate;
    uv = uv * st.xy + st.zw;
    float angle = radians(scroll.z + scroll.w * t);
    float s = sin(angle), c = cos(angle);
    uv = mul(float2x2(c, -s, s, c), uv - 0.5) + 0.5;
    return uv + scroll.xy * t;
}

// 明滅: 1秒あたり blink 回、フェードイン↔アウトを繰り返す(0 なら常に 1)
half GalaxyBlink(half blink, half phase)
{
    return blink > 0.0h ? 0.5h + 0.5h * cos((_Time.y * blink + phase) * TWO_PI) : 1.0h;
}

// 発光(メイン + 発光レイヤー2)。それぞれスクロール・回転・明滅できる
half3 GalaxyEmission(float2 uv)
{
    half3 e = SAMPLE_TEXTURE2D(_EmissionMap, sampler_linear_repeat, GalaxyLayerUV(uv, _EmissionMap_ST, _EmissionScroll, _EmissionAnimate)).rgb
            * _EmissionColor.rgb * GalaxyBlink(_EmissionBlink, 0);
    #if defined(_EMISSION2)
        e += SAMPLE_TEXTURE2D(_Emission2Map, sampler_linear_repeat, GalaxyLayerUV(uv, _Emission2Map_ST, _Emission2Scroll, _Emission2Animate)).rgb
           * _Emission2Color.rgb * GalaxyBlink(_Emission2Blink, 0);
    #endif
    return e;
}

half3 GalaxyBlend(half3 dst, half3 src, half blendMode)
{
    if (blendMode < 0.5h) return src;                    // 通常
    if (blendMode < 1.5h) return dst + src;              // 加算
    if (blendMode < 2.5h) return max(dst + src - dst * src, dst); // スクリーン
    return dst * src;                                    // 乗算
}

/// 1枚ぶん重ねる。unlit が 1 の部分は影・ライトを受けず、あとで発光として足す(glow へ)。
void GalaxyApplyLayer(inout half3 albedo, inout half3 glow, float2 uv0,
    TEXTURE2D_PARAM(tex, samp), TEXTURE2D(mask), float4 st, half4 color, half blendMode, float4 scroll, half animate,
    half fade, half blink, half blinkPhase, half unlit)
{
    half4 c = SAMPLE_TEXTURE2D(tex, samp, GalaxyLayerUV(uv0, st, scroll, animate)) * color;
    half alpha = c.a * fade * SAMPLE_TEXTURE2D(mask, samp, uv0).r * GalaxyBlink(blink, blinkPhase);
    half3 blended = GalaxyBlend(albedo, c.rgb, blendMode);
    albedo = lerp(albedo, blended, alpha * (1.0h - unlit));
    glow += (blendMode < 2.5h ? c.rgb : 0) * alpha * unlit; // 光る側は色をそのまま足す(乗算は光らない)
}

void GalaxyApplyLayers(inout half3 albedo, out half3 glow, float2 uv0)
{
    glow = 0;
    #if defined(_LAYER1)
        GalaxyApplyLayer(albedo, glow, uv0, TEXTURE2D_ARGS(_Layer1Tex, sampler_linear_repeat), _Layer1Mask, _Layer1Tex_ST, _Layer1Color, _Layer1Blend, _Layer1Scroll, _Layer1Animate, _Layer1Fade, _Layer1Blink, _Layer1BlinkPhase, _Layer1Unlit);
    #endif
    #if defined(_LAYER2)
        GalaxyApplyLayer(albedo, glow, uv0, TEXTURE2D_ARGS(_Layer2Tex, sampler_linear_repeat), _Layer2Mask, _Layer2Tex_ST, _Layer2Color, _Layer2Blend, _Layer2Scroll, _Layer2Animate, _Layer2Fade, _Layer2Blink, _Layer2BlinkPhase, _Layer2Unlit);
    #endif
    #if defined(_LAYER3)
        GalaxyApplyLayer(albedo, glow, uv0, TEXTURE2D_ARGS(_Layer3Tex, sampler_linear_repeat), _Layer3Mask, _Layer3Tex_ST, _Layer3Color, _Layer3Blend, _Layer3Scroll, _Layer3Animate, _Layer3Fade, _Layer3Blink, _Layer3BlinkPhase, _Layer3Unlit);
    #endif
}

#endif
