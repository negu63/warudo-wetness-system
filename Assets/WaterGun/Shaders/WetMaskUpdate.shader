Shader "Hidden/SurfaceWetnessWaterGun/WetMaskUpdate"
{
    Properties { _MainTex ("Previous Wetness", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex, _SurfaceBrush;
            float4 _SurfaceBrush_TexelSize;
            float _UseSurfaceBrush;
            float2 _BrushUV;
            float _Radius, _Hardness, _Strength, _DryAmount;
            float4 frag(v2f_img i) : SV_Target
            {
                float previous = tex2D(_MainTex, i.uv).r;
                float distance = length(i.uv - _BrushUV) / max(_Radius, 0.000001);
                float brush = 1.0 - smoothstep(min(_Hardness, 0.999), 1.0, distance);
                if (_UseSurfaceBrush > 0.5)
                {
                    brush = saturate(tex2D(_SurfaceBrush, i.uv).r);
                    // One texel of padding fills UV-island edges sampled by bilinear filtering.
                    // Only deposit is padded; stored wetness and drying are never dilated.
                    for (int y=-1; y<=1; y++) for (int x=-1; x<=1; x++)
                        brush=max(brush,saturate(tex2D(_SurfaceBrush,i.uv+float2(x,y)*_SurfaceBrush_TexelSize.xy).r));
                }
                float wet = saturate(max(0.0, previous - _DryAmount) + brush * _Strength);
                return float4(wet, wet, wet, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
