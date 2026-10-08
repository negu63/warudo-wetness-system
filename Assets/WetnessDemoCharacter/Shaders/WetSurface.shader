Shader "WetnessDemoCharacter/WetSurface"
{
    Properties
    {
        _MainTex ("Base Color", 2D) = "white" {}
        _Color ("Base Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1
        _SurfaceMap ("Roughness / Smoothness Map", 2D) = "white" {}
        [Toggle] _UseSurfaceMap ("Use Surface Map", Float) = 0
        [Enum(Roughness_R,0,Smoothness_R,1,Standard_Smoothness_A,2)] _SurfaceMapMode ("Surface Map Format", Float) = 0
        _DrySmoothness ("Dry Smoothness (No Map)", Range(0,1)) = 0.25
        _MetallicMap ("Metallic Map (R)", 2D) = "white" {}
        [Toggle] _UseMetallicMap ("Use Metallic Map", Float) = 0
        _Metallic ("Metallic (No Map)", Range(0,1)) = 0
        _Wetness ("Wetness", Range(0,1)) = 0
        _WetMask ("Fixed Wet Mask (R)", 2D) = "white" {}
        [Toggle] _UseWetMask ("Use Fixed Wet Mask", Float) = 0
        _WetDarkening ("Wet Darkening", Range(0,1)) = 0.08
        _WetSmoothness ("Wet Smoothness Increase", Range(0,1)) = 0.75
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "SurfaceWetnessContract"="1" "SurfaceWetnessFeatures"="MaskColorPbr" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        #include "WetnessCommon.cginc"
        void surf(Input IN, inout SurfaceOutputStandard o) { EvaluateWetSurface(IN, o, false); }
        ENDCG
    }
    Fallback "Diffuse"
}
