#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SurfaceWetness.WaterGun;
using Hit=SurfaceWetness.WaterGun.WetRayHit;

public static class WaterGunLifecycleChecks
{
    private static int Owned() => Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Count(o=>o!=null &&
        (o is Material || o is Mesh || o is RenderTexture) && o.name.StartsWith("WaterGun "));
    public static object Run()
    {
        string[] paths={WaterGunDemoProfiles.Path};
        var update=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat");
        var coverage=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat");
        int baseline=Owned();int rounds=12;var counts=new int[1];
        for(int round=0;round<rounds;round++)
        {
            int which=round%paths.Length;var root=PrefabUtility.LoadPrefabContents(paths[which]);WetSurfaceBindings bindings=null;
            Texture2D cpu=null;var previous=RenderTexture.active;
            try
            {
                var catalog=WetCharacterCatalog.Scan(root);counts[which]=catalog.CandidateCount;
                var originals=catalog.Entries.Select(e=>e.Material).ToArray();
                var queues=originals.Select(m=>m.renderQueue).ToArray();
                var keywords=originals.Select(m=>m.shaderKeywords).ToArray();
                bindings=new WetSurfaceBindings(catalog,WaterGunDemoProfiles.Profiles(root),update,coverage);
                bindings.Update(0,false);
                Need(bindings.Surfaces.Count==3,"Different surface counts");
                Need(bindings.GeometryCount==3,"Different geometry counts");
                foreach(var s in bindings.Surfaces)
                {
                    var original=s.Entry.Material;var clone=s.Binding.RuntimeMaterial;
                    foreach(string property in new[]{"_MainTex","_BumpMap","_MetallicMap","_SurfaceMap"})
                        Need(original.GetTexture(property)==clone.GetTexture(property),"Original textures preserved");
                    Need(original.shader==clone.shader && original.shaderKeywords.Where(Unrelated).SequenceEqual(clone.shaderKeywords.Where(Unrelated)),"Shader and unrelated keywords preserved");
                    Need(clone.renderQueue==2000 && clone.IsKeywordEnabled("_WET_TRANSPARENT")==s.Profile.Opacity && clone.GetTag("RenderType",false)=="Opaque" && clone.IsKeywordEnabled("_WET_OPAQUE_ONLY"),"Profile controls clone render mode and preserves shadow classification");
                }
                for(int i=0;i<originals.Length;i++)Need(originals[i].renderQueue==queues[i] && originals[i].shaderKeywords.SequenceEqual(keywords[i]),"Source mode untouched");
                Hit hit;WetSurfaceBindings.Surface selected;bool found=false;
                var cloth=bindings.Surfaces.First(s=>s.Profile.Order==0);var bounds=cloth.Entry.Renderer.bounds;
                for(int side=0;side<2 && !found;side++)for(int y=2;y<9 && !found;y++)for(int x=2;x<9 && !found;x++)
                {
                    var target=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x/10f),Mathf.Lerp(bounds.min.y,bounds.max.y,y/10f),bounds.center.z);
                    found=bindings.Trace(new Ray(target+Vector3.forward*(side==0 ? -3:3),side==0 ? Vector3.forward:Vector3.back),8,out hit,out selected);
                }
                Need(found,"Both targets have actual hits");
                // Source replacement by another system must survive disposal.
                if(round==4)
                {
                    var external=new Material(originals[0]);var renderer=catalog.Entries[0].Renderer;int slot=catalog.Entries[0].Slot;
                    try
                    {
                        var array=renderer.sharedMaterials;array[slot]=external;renderer.sharedMaterials=array;
                        Need(!bindings.IsCurrent(),"Material swap detected");bindings.Dispose();bindings=null;
                        Need(renderer.sharedMaterials[slot]==external,"Other system's material preserved");
                        array=renderer.sharedMaterials;array[slot]=originals[0];renderer.sharedMaterials=array;
                    }
                    finally{UnityEngine.Object.DestroyImmediate(external);}
                }
                else
                {
                    bindings.Dispose();bindings.Dispose();bindings=null;
                    for(int i=0;i<catalog.Entries.Count;i++)Need(catalog.Entries[i].Renderer.sharedMaterials[catalog.Entries[i].Slot]==originals[i],"Original restored");
                }
                bindings=new WetSurfaceBindings(WetCharacterCatalog.Scan(root),WaterGunDemoProfiles.Profiles(root),update,coverage);
                cpu=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);RenderTexture.active=bindings.Surfaces[0].Binding.State.Mask;
                cpu.ReadPixels(new Rect(128,128,1,1),0,0);cpu.Apply();Need(cpu.GetPixel(0,0).r==0,"Rebind begins dry");
                RenderTexture.active=previous;
                if(round==rounds-1){PrefabUtility.UnloadPrefabContents(root);root=null;Need(!bindings.IsCurrent(),"Destroyed target detected");}
            }
            finally
            {
                RenderTexture.active=previous;bindings?.Dispose();if(cpu!=null)UnityEngine.Object.DestroyImmediate(cpu);
                if(root!=null)PrefabUtility.UnloadPrefabContents(root);
            }
            Need(Owned()==baseline,"Owned materials/meshes/RT return to baseline round "+round);
        }
        var evidence=new{rounds,slotCounts=counts,geometryCounts=new[]{3},ownedBaseline=baseline,ownedAfter=Owned(),textureAndShaderPreserved=true,externalMaterialPreserved=true,destroyedTargetSafe=true,rebindDry=true};
        System.IO.File.WriteAllText(".codex-artifacts/WaterGun/phase5-lifecycle.json",Newtonsoft.Json.JsonConvert.SerializeObject(evidence,Newtonsoft.Json.Formatting.Indented));return evidence;
    }
    private static void Need(bool condition,string message){if(!condition)throw new Exception("WaterGun lifecycle: "+message);}
    private static bool Unrelated(string keyword)=>keyword!="_WET_TRANSPARENT" && keyword!="_WET_OPAQUE_ONLY" && keyword!="_WET_TRANSLUCENT_ONLY";
}
#endif
