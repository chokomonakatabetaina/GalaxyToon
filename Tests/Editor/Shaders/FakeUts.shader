// テスト用: 移行ツールが読む項目だけを持つ代わりのシェーダー(本物のシェーダーが無いプロジェクトでも移行をテストできる)
Shader "Hidden/GalaxyToonTests/UniversalToon"
{
    Properties
    {
        _BaseColor("_BaseColor", Color) = (1,1,1,1)
        _MainTex("_MainTex", 2D) = "white" {}
        _NormalMap("_NormalMap", 2D) = "white" {}
        _BumpScale("_BumpScale", Float) = 1
        _1st_ShadeColor("_1st_ShadeColor", Color) = (0.8,0.7,0.7,1)
        _1st_ShadeMap("_1st_ShadeMap", 2D) = "white" {}
        _BaseColor_Step("_BaseColor_Step", Float) = 0.5
        _BaseShade_Feather("_BaseShade_Feather", Float) = 0.05
        _2nd_ShadeColor("_2nd_ShadeColor", Color) = (0.6,0.5,0.5,1)
        _ShadeColor_Step("_ShadeColor_Step", Float) = 0.3
        _1st2nd_Shades_Feather("_1st2nd_Shades_Feather", Float) = 0.05
        _RimLight("_RimLight", Float) = 0
        _RimLightColor("_RimLightColor", Color) = (1,1,1,1)
        _MatCap("_MatCap", Float) = 0
        _MatCap_Sampler("_MatCap_Sampler", 2D) = "white" {}
        _MatCapColor("_MatCapColor", Color) = (1,1,1,1)
        _Emissive_Tex("_Emissive_Tex", 2D) = "white" {}
        _Emissive_Color("_Emissive_Color", Color) = (0,0,0,1)
        _Outline_Width("_Outline_Width", Float) = 1
        _Outline_Color("_Outline_Color", Color) = (0,0,0,1)
        _ClippingMode("_ClippingMode", Float) = 0
        _Clipping_Level("_Clipping_Level", Float) = 0.5
        _CullMode("_CullMode", Float) = 2
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
