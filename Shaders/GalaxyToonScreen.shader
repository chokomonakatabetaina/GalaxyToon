// GalaxyToon の画面の効果(レンダラー機能から使う)。
// 0 = 画面の線: 深度と法線の差で、外形の線(手前の物の外側)と内側の線(指・重なり・部位の境目)を描く。線は塗りの色を暗くして作る
// 1 = グローの抜き出し: 明るい所だけを取り出し、ぼかして小さいテクスチャへ
// 2 = グローの合成: 小さいテクスチャをさらにぼかして、元の絵にスクリーンで重ねる(撮影処理のような拡散光)
// 3 = 塗りを均す: 桑原フィルタ(4 つの区画で、ばらつきのいちばん小さい区画の平均)と、明るさの段(ポスタリゼーション)
Shader "Hidden/GalaxyToon/Screen"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _GalaxyLine;    // x=太さ(px) y=深度のしきい値(割合) z=法線のしきい値 w=線の濃さ
        float4 _GalaxyLine2;   // x=暗くする倍率 y=線を描く最大の距離(m) z=物ごとの線(1=使う)
        TEXTURE2D_X(_GalaxyObjectIdTex);
        float4 _GalaxyGlow;    // x=しきい値 y=やわらかさ z=強さ w=広がり(px)
        float4 _GalaxyGlow2;   // x=光らせる最大の距離(m。0=どこでも。キャラだけに絞る: 背景は奥なので光らない)
        float4 _GalaxyPaint;   // x=半径(px) y=色の段の数(0=しない) z=効き
        TEXTURE2D_X(_GalaxyGlowTex);
        ENDHLSL

        Pass
        {
            Name "GalaxyScreenLines"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 texel = _GalaxyLine.x / _ScreenParams.xy;
                float d0 = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float3 n0 = SampleSceneNormals(uv);
                const float2 dirs[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };
                half edge = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 o = uv + dirs[i] * texel;
                    float d = LinearEyeDepth(SampleSceneDepth(o), _ZBufferParams);
                    // 外形: となりが十分手前なら、この画素(奥)に線を置く(線はシルエットの外側。キャラが痩せない)
                    half outer = smoothstep(_GalaxyLine.y, _GalaxyLine.y * 2.0, (d0 - d) / max(d0, 1e-3));
                    // 内側: 深度は近いのに法線が大きく違う所(指・重なり・部位の境目)
                    float3 n = SampleSceneNormals(o);
                    half inner = smoothstep(_GalaxyLine.z, _GalaxyLine.z * 1.6, 1.0 - dot(n0, n)) * (1.0 - smoothstep(0.02, 0.06, abs(d - d0) / max(d0, 1e-3)));
                    // 法線の無い所(空・背景)どうしの差は線にしない
                    inner *= step(0.5, dot(n0, n0)) * step(0.5, dot(n, n));
                    // 物ごとの線: 物の番号が違う所(重なった服と体・服どうし)。手前の物の側に置く(この画素が手前のときだけ)
                    half objectEdge = 0;
                    if (_GalaxyLine2.z > 0.5)
                    {
                        half4 id0 = SAMPLE_TEXTURE2D_X(_GalaxyObjectIdTex, sampler_PointClamp, uv);
                        half4 id1 = SAMPLE_TEXTURE2D_X(_GalaxyObjectIdTex, sampler_PointClamp, o);
                        half differ = step(0.004h, dot(abs(id0.rgb - id1.rgb), half3(1, 1, 1)));
                        objectEdge = differ * id0.a * id1.a * step(d0, d + 1e-4);
                    }

                    // 遠い所は描かない(手前の側の深度で決める。外形の線は奥の画素に置くので、奥の深度では判定しない)
                    edge = max(edge, max(max(outer, inner), objectEdge) * step(min(d, d0), _GalaxyLine2.y));
                }
                return half4(lerp(color.rgb, color.rgb * _GalaxyLine2.x, edge * _GalaxyLine.w), color.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "GalaxyGlowPrefilter"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            // 光らせる距離の中か(0 なら、どこでも)
            half Near(float2 uv)
            {
                if (_GalaxyGlow2.x <= 0.0) return 1.0h;
                float d = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                return d <= _GalaxyGlow2.x ? 1.0h : 0.0h;
            }

            half3 Bright(half3 c)
            {
                half b = max(c.r, max(c.g, c.b));
                half soft = saturate((b - _GalaxyGlow.x + _GalaxyGlow.y) / max(2.0 * _GalaxyGlow.y, 1e-4));
                soft = soft * soft * _GalaxyGlow.y;
                half contribution = max(soft, b - _GalaxyGlow.x) / max(b, 1e-4);
                return c * contribution;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _GalaxyGlow.w / _ScreenParams.xy;
                half3 sum = 0;
                half weight = 0;
                [unroll]
                for (int y = -2; y <= 2; y++)
                {
                    [unroll]
                    for (int x = -2; x <= 2; x++)
                    {
                        half w = exp(-(x * x + y * y) / 4.0);
                        float2 o = uv + float2(x, y) * texel;
                        sum += Bright(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, o).rgb) * Near(o) * w;
                        weight += w;
                    }
                }
                return half4(sum / weight, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "GalaxyGlowComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 texel = _GalaxyGlow.w * 2.0 / _ScreenParams.xy;
                half3 glow = 0;
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 0.785398;
                    glow += SAMPLE_TEXTURE2D_X(_GalaxyGlowTex, sampler_LinearClamp, uv + float2(cos(a), sin(a)) * texel).rgb;
                }
                glow = (glow / 8.0 + SAMPLE_TEXTURE2D_X(_GalaxyGlowTex, sampler_LinearClamp, uv).rgb) * 0.5 * _GalaxyGlow.z;
                // スクリーン合成(白を超えない、やわらかい光)
                return half4(1.0 - (1.0 - saturate(color.rgb)) * (1.0 - saturate(glow)) + max(color.rgb - 1.0, 0), color.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "GalaxyPaint"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 texel = 1.0 / _ScreenParams.xy;
                int r = (int)clamp(_GalaxyPaint.x, 1, 4);
                half3 mean[4] = { (half3)0, (half3)0, (half3)0, (half3)0 };
                half3 sq[4] = { (half3)0, (half3)0, (half3)0, (half3)0 };
                const int2 quad[4] = { int2(-1, -1), int2(1, -1), int2(-1, 1), int2(1, 1) };
                half count = (r + 1) * (r + 1);
                [unroll]
                for (int q = 0; q < 4; q++)
                {
                    [loop]
                    for (int y = 0; y <= r; y++)
                    {
                        [loop]
                        for (int x = 0; x <= r; x++)
                        {
                            half3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(x * quad[q].x, y * quad[q].y) * texel).rgb;
                            mean[q] += c;
                            sq[q] += c * c;
                        }
                    }
                }
                half best = 1e9;
                half3 result = original.rgb;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    half3 m = mean[k] / count;
                    half3 v = abs(sq[k] / count - m * m);
                    half s = v.r + v.g + v.b;
                    if (s < best) { best = s; result = m; }
                }
                // 色の段: 明るさだけを段にする(色相は変えない)。明るさは見た目に近い平方根の空間で区切る
                if (_GalaxyPaint.y > 1.5)
                {
                    half luma = max(dot(result, half3(0.2126h, 0.7152h, 0.0722h)), 1e-4h);
                    half stepped = floor(sqrt(luma) * _GalaxyPaint.y + 0.5h) / _GalaxyPaint.y;
                    result *= max(stepped * stepped, 0.02h) / luma;
                }
                return half4(lerp(original.rgb, result, _GalaxyPaint.z), original.a);
            }
            ENDHLSL
        }
    }
}
