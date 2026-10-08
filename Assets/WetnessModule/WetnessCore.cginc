#ifndef SURFACE_WETNESS_CORE_V1
#define SURFACE_WETNESS_CORE_V1
// Shader contract v1: UV0 linear mask R, 0=dry, 1=wet. No texture/PBR assumptions.
float WetnessAmount(float maskR, float amount) { return saturate(maskR) * saturate(amount); }
float3 WetnessDarken(float3 originalColor, float wet, float darkening)
{ return originalColor * (1.0 - saturate(wet) * saturate(darkening)); }
float WetnessPbrSmoothness(float originalSmoothness, float wet, float increase)
{ float dry=saturate(originalSmoothness); return dry+(1.0-dry)*saturate(increase)*saturate(wet); }
// Multiplication preserves original cutout holes; adapter chooses its dry-alpha policy.
float WetnessOpacity(float originalAlpha, float wet, float targetOpacity, float enabled)
{ return originalAlpha * lerp(1.0,saturate(targetOpacity),saturate(wet)*saturate(enabled)); }
#endif
