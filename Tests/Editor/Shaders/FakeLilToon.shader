// テスト用: 移行ツールが読む項目だけを持つ代わりのシェーダー(本物のシェーダーが無いプロジェクトでも移行をテストできる)
Shader "Hidden/GalaxyToonTests/lilToonOutline"
{
    Properties
    {
        _Color("_Color", Color) = (1,1,1,1)
        _MainTex("_MainTex", 2D) = "white" {}
        _UseBumpMap("_UseBumpMap", Float) = 1
        _BumpMap("_BumpMap", 2D) = "white" {}
        _BumpScale("_BumpScale", Float) = 1
        _UseShadow("_UseShadow", Float) = 0
        _ShadowStrength("_ShadowStrength", Float) = 1
        _ShadowColor("_ShadowColor", Color) = (0.7,0.6,0.8,1)
        _ShadowBorder("_ShadowBorder", Float) = 0.5
        _ShadowBlur("_ShadowBlur", Float) = 0.1
        _Shadow2ndColor("_Shadow2ndColor", Color) = (0.6,0.5,0.7,0)
        _Shadow2ndBorder("_Shadow2ndBorder", Float) = 0.3
        _Shadow2ndBlur("_Shadow2ndBlur", Float) = 0.1
        _Shadow3rdColor("_Shadow3rdColor", Color) = (0.5,0.4,0.6,0)
        _Shadow3rdBorder("_Shadow3rdBorder", Float) = 0.2
        _Shadow3rdBlur("_Shadow3rdBlur", Float) = 0.1
        _ShadowStrengthMask("_ShadowStrengthMask", 2D) = "white" {}
        _UseRim("_UseRim", Float) = 0
        _RimColor("_RimColor", Color) = (1,1,1,1)
        _RimBorder("_RimBorder", Float) = 0.5
        _RimBlur("_RimBlur", Float) = 0.5
        _RimFresnelPower("_RimFresnelPower", Float) = 3
        _UseMatCap("_UseMatCap", Float) = 0
        _MatCapTex("_MatCapTex", 2D) = "white" {}
        _MatCapColor("_MatCapColor", Color) = (1,1,1,1)
        _UseEmission("_UseEmission", Float) = 0
        _EmissionMap("_EmissionMap", 2D) = "white" {}
        _EmissionColor("_EmissionColor", Color) = (0,0,0,1)
        _OutlineWidth("_OutlineWidth", Float) = 0.08
        _OutlineColor("_OutlineColor", Color) = (0,0,0,1)
        _OutlineWidthMask("_OutlineWidthMask", 2D) = "white" {}
        _TransparentMode("_TransparentMode", Float) = 0
        _Cutoff("_Cutoff", Float) = 0.5
        _Cull("_Cull", Float) = 2
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 p : POSITION) : SV_POSITION { return p; }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
