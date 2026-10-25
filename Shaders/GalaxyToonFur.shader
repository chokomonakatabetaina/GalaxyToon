// GalaxyToon  Author: @matechagame
// GalaxyShader/ToonFur: ファー(毛並み)。GalaxyToonFur が同じメッシュを何層も重ねて描く(シェル法)。
// 各層は法線方向へ少し浮かせ、UV のマスごとに1本の毛(上の層ほど細く・少なく)を切り抜く。根元は暗く、毛先は明るく。
// 重力・動きで毛先がなびく(_FurForce)。ライトは GalaxyShader/Toon と同じ「キャラ専用ライト」か、シーンのメインライト。
Shader "GalaxyShader/ToonFur"
{
    Properties
    {
        [MainTexture] _BaseMap("毛の色 (テクスチャ)", 2D) = "white" {}
        [MainColor] _BaseColor("毛の色", Color) = (1, 1, 1, 1)
        _Cutoff("テクスチャの透明部分を切る (A のしきい値)", Range(0, 1)) = 0.5
        [NoScaleOffset] _FurMask("毛の生える所 (R。白=生える)", 2D) = "white" {}
        _FurLength("毛の長さ (cm)", Range(0, 10)) = 1.5
        _FurDensity("毛の密度 (UV 1 あたりの本数)", Range(10, 2000)) = 400
        _FurThickness("毛の太さ", Range(0.1, 1)) = 0.5
        _FurGravity("重力で垂れる", Range(0, 1)) = 0.3
        _FurRootDark("根元の暗さ", Range(0, 1)) = 0.5
        _FurTipColor("毛先の色 (A=混ぜる強さ)", Color) = (1, 1, 1, 0.3)
        _ShadowColor("影の色", Color) = (0.82, 0.76, 0.85, 1)
        _ShadowBorder("影の境界", Range(0, 1)) = 0.5
        _ShadowBlur("影のぼかし", Range(0, 1)) = 0.15
        [HDR] _RimColor("毛先の縁の光 (A=強さ)", Color) = (1, 1, 1, 0.3)
        _LightMinLimit("明るさの下限", Range(0, 1)) = 0.05
        _LightMaxLimit("明るさの上限", Range(0, 10)) = 1
        [HideInInspector] _FurShell("層 (0=根元 1=毛先。GalaxyToonFur が入れる)", Float) = 0
        [HideInInspector] _FurForce("なびく向き (ワールド。GalaxyToonFur が入れる)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest+10" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_FurMask); SAMPLER(sampler_linear_repeat);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _FurLength;
                half _FurDensity;
                half _FurThickness;
                half _FurGravity;
                half _FurRootDark;
                half4 _FurTipColor;
                half4 _ShadowColor;
                half _ShadowBorder;
                half _ShadowBlur;
                half4 _RimColor;
                half _LightMinLimit;
                half _LightMaxLimit;
                half _FurShell;
                float4 _FurForce;
            CBUFFER_END

            float4 _AdvToonLightDir;
            half4 _AdvToonLightColor;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; half fogFactor : TEXCOORD3; };

            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float t = _FurShell;
                float len = _FurLength * 0.01;
                // 法線方向へ浮かせ、毛先ほど重力・動きの向きへ曲がる(t^2)
                positionWS += normalWS * (len * t);
                positionWS += (float3(0, -1, 0) * _FurGravity + _FurForce.xyz) * (len * t * t);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.normalWS = normalWS;
                o.positionWS = positionWS;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);
                float t = _FurShell;
                half mask = SAMPLE_TEXTURE2D(_FurMask, sampler_linear_repeat, input.uv).r;
                // 毛の切り抜き: マスごとに1本。高さがランダムで、層が上がるほど細く、届かない毛は消える
                float2 p = input.uv * _FurDensity;
                float2 cell = floor(p);
                float2 rnd = Hash22(cell);
                float2 center = 0.2 + 0.6 * Hash22(cell + 7.3);
                float height = (0.4 + 0.6 * rnd.x) * mask;
                float radius = _FurThickness * 0.5 * (1.0 - t * 0.85) * (0.6 + 0.4 * rnd.y);
                float d = length(frac(p) - center);
                clip(min(height - t, radius - d));

                // トゥーンの明るさ(キャラ専用ライトがあればそれ)
                Light main = GetMainLight();
                half3 lightDir = main.direction; half3 lightColor = main.color;
                if (dot(_AdvToonLightDir.xyz, _AdvToonLightDir.xyz) > 1e-4) { lightDir = normalize(_AdvToonLightDir.xyz); lightColor = _AdvToonLightColor.rgb; }
                lightColor = clamp(lightColor, _LightMinLimit, _LightMaxLimit);
                half3 normal = normalize(input.normalWS);
                half lambert = saturate(dot(normal, lightDir) * 0.5h + 0.5h);
                half lit = smoothstep(_ShadowBorder - _ShadowBlur * 0.5h, _ShadowBorder + _ShadowBlur * 0.5h, lambert);
                half3 color = lerp(albedo.rgb * _ShadowColor.rgb, albedo.rgb, lit);
                color = lerp(color, _FurTipColor.rgb, t * t * _FurTipColor.a);       // 毛先の色
                color *= lerp(1.0h - _FurRootDark, 1.0h, t);                          // 根元は暗い(毛の重なりの影)
                half3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(saturate(1.0h - dot(normal, viewDir)), 3.0h) * t * _RimColor.a;
                color = color * lightColor + _RimColor.rgb * rim * lightColor;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
