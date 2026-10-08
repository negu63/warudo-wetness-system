Shader "WetnessDemoCharacter/WetCloth"
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
        _DrySmoothness ("Dry Smoothness (No Map)", Range(0,1)) = 0.15
        _MetallicMap ("Metallic Map (R)", 2D) = "white" {}
        [Toggle] _UseMetallicMap ("Use Metallic Map", Float) = 0
        _Metallic ("Metallic (No Map)", Range(0,1)) = 0
        _Wetness ("Wetness", Range(0,1)) = 0
        _WetMask ("Fixed Wet Mask (R)", 2D) = "white" {}
        [Toggle] _UseWetMask ("Use Fixed Wet Mask", Float) = 0
        _WetDarkening ("Wet Darkening", Range(0,1)) = 0.2
        _WetSmoothness ("Wet Smoothness Increase", Range(0,1)) = 0.65
        _WetOpacity ("Fully Wet Opacity", Range(0,1)) = 0.35
        [Toggle] _WetOpacityEnabled ("Wet Opacity Enabled", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "SurfaceWetnessContract"="1" "SurfaceWetnessFeatures"="MaskColorPbrOpacity" }
        Cull Back
        ZWrite Off
        Pass
        {
            Name "ClothDepth"
            ZWrite On
            ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }
            fixed4 frag() : SV_Target { return 0; }
            ENDCG
        }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:premul
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        #include "WetnessCommon.cginc"
        void surf(Input IN, inout SurfaceOutputStandard o) { EvaluateWetSurface(IN, o, true); }
        ENDCG
    }
    Fallback Off
}
