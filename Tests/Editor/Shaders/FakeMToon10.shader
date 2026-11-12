// テスト用: 移行ツールが読む項目だけを持つ代わりのシェーダー(本物のシェーダーが無いプロジェクトでも移行をテストできる)
Shader "Hidden/GalaxyToonTests/VRM10/MToon10"
{
    Properties
    {
        _Color("_Color", Color) = (1,1,1,1)
        _MainTex("_MainTex", 2D) = "white" {}
        _BumpMap("_BumpMap", 2D) = "white" {}
        _BumpScale("_BumpScale", Float) = 1
        _ShadeColor("_ShadeColor", Color) = (0.8,0.6,0.6,1)
        _ShadeTex("_ShadeTex", 2D) = "white" {}
        _ShadingShiftFactor("_ShadingShiftFactor", Float) = -0.05
        _ShadingToonyFactor("_ShadingToonyFactor", Float) = 0.95
        _EmissionMap("_EmissionMap", 2D) = "white" {}
        _EmissionColor("_EmissionColor", Color) = (0,0,0,1)
        _MatcapTex("_MatcapTex", 2D) = "white" {}
        _MatcapColor("_MatcapColor", Color) = (1,1,1,1)
        _RimColor("_RimColor", Color) = (0,0,0,1)
        _RimFresnelPower("_RimFresnelPower", Float) = 5
        _OutlineWidthMode("_OutlineWidthMode", Float) = 0
        _OutlineWidth("_OutlineWidth", Float) = 0
        _OutlineColor("_OutlineColor", Color) = (0,0,0,1)
        _OutlineLightingMix("_OutlineLightingMix", Float) = 1
        _OutlineWidthTex("_OutlineWidthTex", 2D) = "white" {}
        _AlphaMode("_AlphaMode", Float) = 0
        _Cutoff("_Cutoff", Float) = 0.5
        _DoubleSided("_DoubleSided", Float) = 0
        _RenderQueueOffset("_RenderQueueOffset", Float) = 0
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
