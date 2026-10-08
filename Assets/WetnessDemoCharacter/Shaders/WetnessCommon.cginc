#ifndef SURFACE_WETNESS_PREVIEW_COMMON
#define SURFACE_WETNESS_PREVIEW_COMMON
#include "../../WetnessModule/WetnessCore.cginc"
sampler2D _MainTex, _BumpMap, _SurfaceMap, _MetallicMap, _WetMask;
fixed4 _Color;
half _NormalStrength, _DrySmoothness, _Metallic, _UseSurfaceMap, _SurfaceMapMode, _UseMetallicMap;
half _Wetness, _UseWetMask, _WetDarkening, _WetSmoothness, _WetOpacity, _WetOpacityEnabled;
struct Input { float2 uv_MainTex; float2 uv_WetMask; };

void EvaluateWetSurface(Input IN, inout SurfaceOutputStandard o, bool cloth)
{
    fixed4 base = tex2D(_MainTex, IN.uv_MainTex) * _Color;
    half wet = WetnessAmount(lerp(1.0h, tex2D(_WetMask, IN.uv_WetMask).r, saturate(_UseWetMask)),_Wetness);
    fixed4 surface = tex2D(_SurfaceMap, IN.uv_MainTex);
    half mappedSmoothness = _SurfaceMapMode < 0.5h ? 1.0h - surface.r : _SurfaceMapMode < 1.5h ? surface.r : surface.a;
    half drySmoothness = saturate(lerp(_DrySmoothness, mappedSmoothness, saturate(_UseSurfaceMap)));
    o.Albedo = WetnessDarken(base.rgb,wet,_WetDarkening);
    o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _NormalStrength);
    o.Metallic = saturate(lerp(_Metallic, tex2D(_MetallicMap, IN.uv_MainTex).r, saturate(_UseMetallicMap)));
    o.Smoothness = WetnessPbrSmoothness(drySmoothness,wet,_WetSmoothness);
    o.Occlusion = 1;
    // Base texture alpha is not wetness. The dry outer garment remains opaque.
    o.Alpha = cloth ? WetnessOpacity(1.0h,wet,_WetOpacity,_WetOpacityEnabled) : 1.0h;
}
#endif
