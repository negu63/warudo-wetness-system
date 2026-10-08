using System;
using UnityEngine;

namespace SurfaceWetness.WaterGun
{
    // Phase 2 binding: one chosen slot. Geometry owner and state owner are separate.
    public sealed class WetSingleSurface : IDisposable
    {
        private static readonly System.Collections.Generic.Dictionary<System.Tuple<Renderer,int>,WetSingleSurface> owners=
            new System.Collections.Generic.Dictionary<System.Tuple<Renderer,int>,WetSingleSurface>();
        private readonly System.Tuple<Renderer,int> ownerKey;
        private bool registered;
        public readonly WetCharacterCatalog.Entry Entry;
        public readonly WetSurfaceState State;
        public Material RuntimeMaterial { get; private set; }
        private readonly Material original;
        public readonly WetGeometry Geometry;
        private readonly bool ownsGeometry;
        private readonly bool[] allowed;
        private Material coverage;
        private RenderTexture brush;
        private WetRenderPair renderPair;
        public int GeometryRefreshCount => Geometry.RefreshCount;

        public WetSingleSurface(WetCharacterCatalog.Entry entry,Material update,Material coverageTemplate,int resolution,WetGeometry geometry=null)
        {
            Entry=entry;original=entry.Material;ownsGeometry=geometry==null;
            ownerKey=System.Tuple.Create(entry.Renderer,entry.Slot);
            try
            {
                if(owners.ContainsKey(ownerKey))throw new InvalidOperationException("이미 다른 WaterGun이 연결한 표면: "+entry.Key);
                owners.Add(ownerKey,this);registered=true;
                Geometry=geometry ?? new WetGeometry(entry.Renderer);
                allowed=new bool[Geometry.Mesh.subMeshCount];allowed[entry.Slot]=true;
                State=new WetSurfaceState(update,resolution);
                coverage=new Material(coverageTemplate){name="WaterGun surface coverage"};
                brush=new RenderTexture(resolution,resolution,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
                {name="WaterGun surface brush",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                if(!brush.Create())throw new InvalidOperationException("Brush RT creation failed");
                RuntimeMaterial=new Material(original){name="WaterGun runtime / "+entry.Key};
                RuntimeMaterial.SetOverrideTag("SurfaceWetnessSourceMaterialName",original.GetTag("SurfaceWetnessSourceMaterialName",false,original.name));
                var materials=entry.Renderer.sharedMaterials;materials[entry.Slot]=RuntimeMaterial;entry.Renderer.sharedMaterials=materials;
                renderPair=WetRenderPair.Acquire(entry.Renderer,entry.Slot,RuntimeMaterial);
                Apply(.2f,.65f,false,.35f);
            }
            catch{Dispose();throw;}
        }
        public bool IsCurrent()
        {
            if(Entry.Renderer==null)return false;
            var materials=Entry.Renderer.sharedMaterials;
            if(Entry.Slot>=materials.Length || materials[Entry.Slot]!=RuntimeMaterial)return false;
            return Geometry.IsCurrent();
        }
        public bool Trace(Ray ray,float range,out WetRayHit hit)
        {
            hit=default(WetRayHit);
            if(Entry.Renderer==null || !Entry.Renderer.enabled || !Entry.Renderer.gameObject.activeInHierarchy)return false;
            return Geometry.Trace(ray,range,allowed,out hit);
        }
        public void Deposit(WetRayHit hit,Vector3 incoming,float radius,float hardness,float amount)
        {
            var matrix=Entry.Renderer.transform.localToWorldMatrix;
            var localIncoming=matrix.transpose.MultiplyVector(incoming).normalized;
            WetSurfaceCoverage.Render(Geometry.Mesh,coverage,brush,localIncoming,matrix,hit.point,radius,hardness,Entry.Slot);
            State.Deposit(brush,amount);
        }
        public void Apply(float darkening,float smoothness,bool opacity,float wetOpacity,int layerOrder=0)
        {
            WetShaderAdapter.SetOpacityMode(RuntimeMaterial,opacity,layerOrder);
            RuntimeMaterial.SetFloat("_UseWetMask",1);RuntimeMaterial.SetFloat("_Wetness",1);
            RuntimeMaterial.SetTexture("_WetMask",State.Mask);
            RuntimeMaterial.SetTextureScale("_WetMask",Vector2.one);RuntimeMaterial.SetTextureOffset("_WetMask",Vector2.zero);
            RuntimeMaterial.SetFloat("_WetDarkening",Mathf.Clamp01(darkening));RuntimeMaterial.SetFloat("_WetSmoothness",Mathf.Clamp01(smoothness));
            if(RuntimeMaterial.HasProperty("_WetOpacity"))RuntimeMaterial.SetFloat("_WetOpacity",Mathf.Clamp01(wetOpacity));
            if(RuntimeMaterial.HasProperty("_WetOpacityEnabled"))RuntimeMaterial.SetFloat("_WetOpacityEnabled",opacity ? 1 : 0);
            else if(RuntimeMaterial.HasProperty("_WetOpacity"))RuntimeMaterial.SetFloat("_WetOpacity",opacity ? Mathf.Clamp01(wetOpacity) : 1);
            renderPair.Apply(opacity,layerOrder);
        }
        public void Dispose()
        {
            renderPair?.Dispose();renderPair=null;
            if(Entry.Renderer!=null && RuntimeMaterial!=null)
            {
                var materials=Entry.Renderer.sharedMaterials;
                if(Entry.Slot<materials.Length && materials[Entry.Slot]==RuntimeMaterial){materials[Entry.Slot]=original;Entry.Renderer.sharedMaterials=materials;}
            }
            if(State!=null)State.Dispose();
            if(RuntimeMaterial!=null)WetSurfaceState.Release(RuntimeMaterial);RuntimeMaterial=null;
            if(coverage!=null)WetSurfaceState.Release(coverage);coverage=null;
            if(brush!=null){if(RenderTexture.active==brush)RenderTexture.active=null;brush.Release();WetSurfaceState.Release(brush);}brush=null;
            if(ownsGeometry && Geometry!=null)Geometry.Dispose();
            if(registered){owners.Remove(ownerKey);registered=false;}
        }
    }
}
