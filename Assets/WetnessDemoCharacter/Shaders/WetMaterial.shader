Shader "WetnessDemoCharacter/WetMaterial"
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
        [Toggle(_WET_TRANSPARENT)] _WetOpacityEnabled ("Wet Opacity Enabled", Float) = 0
        [HideInInspector] _WetSplitSupported ("Split Renderer Contract", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "SurfaceWetnessContract"="1" "SurfaceWetnessFeatures"="MaskColorPbrOpacity" "SurfaceWetnessRenderMode"="MaterialToggle" }
        Cull Back
        // Dry pixels keep the opaque lighting/depth path even when transparency is allowed.
        CGPROGRAM
        #pragma surface surfOpaque Standard fullforwardshadows addshadow
        #pragma shader_feature _WET_TRANSPARENT
        #pragma multi_compile __ _WET_TRANSLUCENT_ONLY
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        #include "WetnessCommon.cginc"
        void surfOpaque(Input IN, inout SurfaceOutputStandard o)
        {
            #ifdef _WET_TRANSLUCENT_ONLY
            clip(-1);
            #endif
            EvaluateWetSurface(IN,o,true);
            #ifdef _WET_TRANSPARENT
            clip(o.Alpha-0.999h);
            #endif
            o.Alpha=1;
        }
        ENDCG
        CGPROGRAM
        #pragma surface surfTransparent Standard fullforwardshadows alpha:fade
        #pragma shader_feature _WET_TRANSPARENT
        #pragma multi_compile __ _WET_OPAQUE_ONLY
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        #include "WetnessCommon.cginc"
        void surfTransparent(Input IN, inout SurfaceOutputStandard o)
        {
            #ifdef _WET_OPAQUE_ONLY
            clip(-1);
            #endif
            #ifndef _WET_TRANSPARENT
            clip(-1);
            #endif
            EvaluateWetSurface(IN,o,true);
            // Only genuinely translucent pixels use blending; opaque pixels already wrote depth.
            clip(0.999h-o.Alpha);
        }
        ENDCG
    }
    CustomEditor "WetMaterialInspector"
    Fallback Off
}
