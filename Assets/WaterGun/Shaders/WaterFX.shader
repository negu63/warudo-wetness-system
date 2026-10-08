Shader "SurfaceWetnessWaterGun/WaterFX"
{
    Properties
    {
        _Color ("Water Color", Color) = (.4,.8,1,.65)
        _Splash ("Splash Billboard", Float) = 0
        _Speed ("Flow Speed", Float) = 4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color; float _Splash, _Speed;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            v2f vert(appdata v) { v2f o; o.position=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float edge=pow(saturate(1-abs(i.uv.y*2-1)),.6);
                float flow=.65+.35*sin(i.uv.x*40-_Time.y*_Speed*20);
                float alpha=_Splash>.5 ? saturate(1-length(i.uv*2-1)) : edge*flow;
                return fixed4(_Color.rgb*i.color.rgb,_Color.a*i.color.a*alpha);
            }
            ENDCG
        }
    }
}
