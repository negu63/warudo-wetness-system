Shader "Hidden/SurfaceWetnessWaterGun/WetSurfaceCoverage"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend One One
        BlendOp Max
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float3 _LocalIncoming;
            float4x4 _SurfaceLocalToWorld;
            float3 _BrushWorldCenter;
            float _BrushWorldRadius, _BrushHardness;
            struct Output { float4 position:SV_POSITION; float3 normal:TEXCOORD0; float3 worldPosition:TEXCOORD1; };
            Output vert(appdata_base v)
            {
                Output o;
                // UV rasterization must not inherit a host camera's view/projection state.
                o.position=float4(v.texcoord.xy*2.0-1.0,0,1);
                #if UNITY_UV_STARTS_AT_TOP
                o.position.y=-o.position.y;
                #endif
                o.normal=v.normal;
                o.worldPosition=mul(_SurfaceLocalToWorld,v.vertex).xyz;
                return o;
            }
            float4 frag(Output i):SV_Target
            {
                clip(-dot(i.normal,_LocalIncoming)-0.00001);
                float distance=length(i.worldPosition-_BrushWorldCenter)/max(_BrushWorldRadius,0.000001);
                clip(1.0-distance);
                float brush=1.0-smoothstep(min(_BrushHardness,0.999),1.0,distance);
                return brush;
            }
            ENDCG
        }
    }
    Fallback Off
}
