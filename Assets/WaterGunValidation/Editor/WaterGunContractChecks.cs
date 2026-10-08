#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SurfaceWetness.WaterGun;

public static class WaterGunContractChecks
{
    public static Shader Variant(string prefix,string name)
    {
        var core=File.ReadAllText("Assets/WetnessModule/WetnessCore.cginc");
        var common=File.ReadAllText("Assets/WetnessDemoCharacter/Shaders/WetnessCommon.cginc")
            .Replace("#include \"../../WetnessModule/WetnessCore.cginc\"",core);
        var source=File.ReadAllText("Assets/WetnessDemoCharacter/Shaders/WetMaterial.shader")
            .Replace("#include \"WetnessCommon.cginc\"",common)
            .Replace("WetnessDemoCharacter/WetMaterial",name)
            .Replace("SurfaceWetnessContract",prefix+"Contract")
            .Replace("SurfaceWetnessFeatures",prefix+"Features")
            .Replace("SurfaceWetnessRenderMode",prefix+"RenderMode");
        var shader=ShaderUtil.CreateShaderAsset(source);
        var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").ToArray();
        if(errors.Length>0){UnityEngine.Object.DestroyImmediate(shader);throw new Exception("Contract fixture shader compilation failed");}
        return shader;
    }
    public static object Names()
    {
        foreach(string prefix in new[]{"LifeIsWet","SurfaceWetness","Wetness"})
        {
            var shader=Variant(prefix,"ContractChecks/AnyName/"+prefix);
            var material=new Material(shader);
            try
            {
                Need(WetShaderAdapter.Check(material)==WetShaderAdapter.Supported,"Identical wetness shader with arbitrary name and "+prefix+" contract connects");
                WetShaderAdapter.SetOpacityMode(material,true,2);
                Need(material.IsKeywordEnabled("_WET_TRANSPARENT") && material.renderQueue==2998,"Renamed shader receives opacity mode");
                material.SetOverrideTag(prefix+"Contract","2");
                Need(WetShaderAdapter.Check(material)=="젖음 계약 버전 불일치","Wrong contract version rejected");
                material.SetOverrideTag(prefix+"Contract","1");
                material.SetOverrideTag("WetnessContract","2");
                Need(WetShaderAdapter.Check(material)=="젖음 계약 버전 불일치","Explicit neutral contract cannot fall back to a legacy version");
                material.SetOverrideTag("WetnessContract","1");
                material.SetOverrideTag("WetnessFeatures","MaskColorPbrOpacity");
                material.SetOverrideTag("WetnessRenderMode","MaterialToggle");
                material.SetFloat("_WetSplitSupported",0);
                Need(WetShaderAdapter.Check(material)!=WetShaderAdapter.Supported,"Disabled split rendering is rejected");
            }
            finally{UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(shader);}
        }
        var unsupported=new Material(Shader.Find("Standard"));
        try{Need(WetShaderAdapter.Check(unsupported)!=WetShaderAdapter.Supported,"Unintegrated Standard remains unsupported");}
        finally{UnityEngine.Object.DestroyImmediate(unsupported);}
        return new{arbitraryNames=true,legacyAndNeutralContracts=true,opacityMode=true,wrongVersionRejected=true,neutralAuthoritative=true,splitRequired=true,standardRejected=true};
    }
    public static object Recovery()
    {
        var root=PrefabUtility.LoadPrefabContents("Assets/WetnessDemoCharacter/Character.prefab");
        var unsupported=new Material(Shader.Find("Standard"));WetSurfaceBindings bindings=null;
        try
        {
            var catalog=WetCharacterCatalog.Scan(root);
            var profiles=catalog.Entries.Select(e=>new WetSurfaceProfile{Key=e.Key}).ToArray();
            var renderers=root.GetComponentsInChildren<Renderer>();var originals=renderers.Select(r=>r.sharedMaterials).ToArray();
            foreach(var r in renderers)r.sharedMaterials=r.sharedMaterials.Select(m=>unsupported).ToArray();
            bindings=new WetSurfaceBindings(WetCharacterCatalog.Scan(root),profiles,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat"));
            Need(bindings.Surfaces.Count==0,"Unsupported materials make an empty binding");
            for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=originals[i];
            var repaired=WetCharacterCatalog.Scan(root);
            Need(!bindings.IsCurrent(repaired),"An empty binding invalidates when previously rejected surfaces become supported");
            bindings.Dispose();bindings=null;
            bindings=new WetSurfaceBindings(repaired,profiles,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat"));
            Need(bindings.Surfaces.Count==3,"Recovered materials reconnect all registered surfaces");
            Need(bindings.IsCurrent(WetCharacterCatalog.Scan(root)),"Own runtime clones do not cause repeated rebind");
            return new{failedBindingInvalidates=true,recoveredSurfaces=3,stableRuntimeClones=true};
        }
        finally{bindings?.Dispose();UnityEngine.Object.DestroyImmediate(unsupported);PrefabUtility.UnloadPrefabContents(root);}
    }
    public static object SupplyAndRender(string prefix)
    {
        var shader=Variant(prefix,"ContractChecks/Rendered/"+prefix);
        var root=PrefabUtility.LoadPrefabContents("Assets/WetnessDemoCharacter/Character.prefab");
        var materials=new System.Collections.Generic.List<Material>();
        WetSurfaceBindings bindings=null;Texture2D sample=null;var previous=RenderTexture.active;
        try
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var clones=renderer.sharedMaterials.Select(m=>new Material(m){shader=shader}).ToArray();
                materials.AddRange(clones);renderer.sharedMaterials=clones;
            }
            var catalog=WetCharacterCatalog.Scan(root);
            Need(catalog.CandidateCount==3,"Renamed contract shader is discovered on selected character");
            var profiles=catalog.Entries.Select(e=>new WetSurfaceProfile{Key=e.Key,Opacity=true}).ToArray();
            bindings=new WetSurfaceBindings(catalog,profiles,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat"));
            var bounds=bindings.Surfaces.First(s=>s.Entry.Renderer.name=="OuterCloth").Entry.Renderer.bounds;
            var ray=new Ray(new Vector3(bounds.center.x,Mathf.Lerp(bounds.min.y,bounds.max.y,.65f),bounds.min.z-3),Vector3.forward);
            Need(bindings.TracePath(ray,8,.15f,true)>0,"Renamed shader has actual mesh hits");
            bindings.SupplyPath(ray.direction,.05f,.5f,.7f);bindings.Update(0,false);
            var first=bindings.Path[0];
            sample=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
            Func<float> read=()=>{var mask=first.Surface.Binding.State.Mask;RenderTexture.active=mask;sample.ReadPixels(new Rect(
                Mathf.Clamp((int)(first.Hit.textureCoord.x*mask.width),0,mask.width-1),
                Mathf.Clamp((int)(first.Hit.textureCoord.y*mask.height),0,mask.height-1),1,1),0,0);sample.Apply();return sample.GetPixel(0,0).r;};
            float wet=read();first.Surface.Binding.State.Dry(40,20);float dry=read();
            Need(wet>.5f && dry<.0001f,"Renamed shader binding deposits and dries actual wetness map (wet="+wet+", dry="+dry+")");
            return new{prefix,connected=bindings.Surfaces.Count,wet,dry,render=WaterGunOpacityChecks.Run(shader),splitRender=WaterGunSplitRenderChecks.Run(shader)};
        }
        finally
        {
            RenderTexture.active=previous;bindings?.Dispose();
            if(sample!=null)UnityEngine.Object.DestroyImmediate(sample);
            PrefabUtility.UnloadPrefabContents(root);
            foreach(var material in materials)UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(shader);
        }
    }
    private static void Need(bool condition,string message){if(!condition)throw new Exception("WaterGun contract: "+message);}
}
#endif
