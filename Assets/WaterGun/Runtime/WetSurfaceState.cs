using System;
using UnityEngine;

namespace SurfaceWetness.WaterGun
{
    // One independent UV state, not a fixed body/inner/outer array.
    public sealed class WetSurfaceState : IDisposable
    {
        private readonly RenderTexture[] textures=new RenderTexture[2];
        private Material update;
        private int read;
        private static readonly Unity.Profiling.ProfilerMarker DryMarker=new Unity.Profiling.ProfilerMarker("WaterGun.Dry");
        private static readonly Unity.Profiling.ProfilerMarker DepositMarker=new Unity.Profiling.ProfilerMarker("WaterGun.Deposit");
        public RenderTexture Mask => textures[read];
        public int StampCount { get; private set; }
        public WetSurfaceState(Material template,int resolution)
        {
            if(template==null || !template.shader.isSupported)throw new InvalidOperationException("Mask shader unavailable");
            try
            {
                update=new Material(template){name="WaterGun mask update"};
                var format=RenderTextureFormat.Default;
                foreach(var candidate in new[]{RenderTextureFormat.RFloat,RenderTextureFormat.ARGBFloat,RenderTextureFormat.RHalf,RenderTextureFormat.ARGBHalf})
                    if(SystemInfo.SupportsRenderTextureFormat(candidate)){format=candidate;break;}
                if(format==RenderTextureFormat.Default)throw new NotSupportedException("Floating point RT required");
                for(int i=0;i<2;i++)
                {
                    textures[i]=new RenderTexture(resolution,resolution,0,format,RenderTextureReadWrite.Linear)
                    {name="WaterGun wet state",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,useMipMap=false};
                    if(!textures[i].Create())throw new InvalidOperationException("Wet RT creation failed");
                }
                Clear();
            }
            catch { Dispose();throw; }
        }
        public void Clear()
        {
            var previous=RenderTexture.active;
            try {foreach(var rt in textures){RenderTexture.active=rt;GL.Clear(false,true,Color.clear);}}
            finally{RenderTexture.active=previous;}
        }
        public void Dry(float delta,float seconds)
        {
            if(delta<=0 || float.IsNaN(delta) || float.IsInfinity(delta))return;
            float amount=delta/Mathf.Max(.1f,seconds);if(amount<=0)return;
            using(DryMarker.Auto())Update(null,0,amount);
        }
        public void Deposit(Texture brush,float amount)
        {
            if(brush==null || amount<=0)return;
            using(DepositMarker.Auto())Update(brush,Mathf.Clamp01(amount),0);StampCount++;
        }
        private void Update(Texture brush,float amount,float dry)
        {
            update.SetTexture("_SurfaceBrush",brush);
            update.SetFloat("_UseSurfaceBrush",brush!=null ? 1 : 0);
            update.SetFloat("_Strength",amount);update.SetFloat("_DryAmount",dry);
            update.SetVector("_BrushUV",Vector4.zero);update.SetFloat("_Radius",1);update.SetFloat("_Hardness",0);
            var previous=RenderTexture.active;
            try{Graphics.Blit(textures[read],textures[1-read],update,0);}
            finally{RenderTexture.active=previous;}
            read=1-read;
        }
        public void Dispose()
        {
            for(int i=0;i<textures.Length;i++)if(textures[i]!=null)
            {
                if(RenderTexture.active==textures[i])RenderTexture.active=null;
                textures[i].Release();Release(textures[i]);textures[i]=null;
            }
            if(update!=null)Release(update);update=null;
        }
        internal static void Release(UnityEngine.Object value)
        {if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
