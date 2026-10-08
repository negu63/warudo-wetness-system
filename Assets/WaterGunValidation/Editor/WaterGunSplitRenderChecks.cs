#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SurfaceWetness.WaterGun;

public static class WaterGunSplitRenderChecks
{
    public static object Run(Shader suppliedShader=null)
    {
        var root=new GameObject("Split render validation");
        var co=new GameObject("Camera");co.transform.SetParent(root.transform);
        var camera=co.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
        camera.transform.position=new Vector3(0,0,-3);camera.orthographic=true;camera.orthographicSize=1;
        camera.clearFlags=CameraClearFlags.Skybox;
        var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        camera.targetTexture=target;var cpu=new Texture2D(64,64,TextureFormat.RGBA32,false,true);
        var previous=RenderTexture.active;
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform);quad.layer=31;
        var renderer=quad.GetComponent<Renderer>();
        var material=new Material(suppliedShader ?? Shader.Find("WetnessDemoCharacter/WetMaterial"));
        material.SetColor("_Color",Color.red);material.SetFloat("_Wetness",1);
        material.SetFloat("_WetOpacityEnabled",1);material.SetFloat("_WetOpacity",.5f);
        material.SetFloat("_WetDarkening",0);renderer.sharedMaterial=material;
        WetRenderPair pair=null;
        GameObject character=null;WetSurfaceBindings bindings=null;
        Func<Color> sample=()=>{camera.Render();RenderTexture.active=target;cpu.ReadPixels(new Rect(0,0,64,64),0,0);cpu.Apply();return cpu.GetPixel(32,32);};
        try
        {
            quad.SetActive(false);var sky=sample();quad.SetActive(true);
            WetShaderAdapter.SetOpacityMode(material,true);var reference=sample();
            pair=WetRenderPair.Acquire(renderer,0,material);pair.Apply(true,0);var split=sample();
            Need(Vector3.Distance(Rgb(split),Rgb(reference))<.02f,"Wet material over sky matches transparent-after-sky reference");
            Need(Vector3.Distance(Rgb(split),Rgb(sky))>.05f,"Sky does not overwrite wet clothing");
            var proxy=quad.GetComponentInChildren<WetTransparencyRenderer>();
            Need(proxy!=null && proxy.Proxy.sharedMaterial.renderQueue==3000 && material.renderQueue==2000,"Separate queues");
            Need(WetCharacterCatalog.Scan(root).RendererCount==1,"Internal renderer excluded from surface catalog");
            renderer.enabled=false;proxy.Synchronize();Need(!proxy.Proxy.enabled,"Source visibility propagated");
            renderer.enabled=true;pair.Apply(false,0);Need(!proxy.Proxy.enabled,"Opacity OFF disables transparent renderer");
            pair.Dispose();pair=null;Need(quad.GetComponentInChildren<WetTransparencyRenderer>()==null,"Proxy released");

            character=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WaterGunDemoProfiles.Path));
            var before=WetCharacterCatalog.Scan(character);
            bindings=new WetSurfaceBindings(before,WaterGunDemoProfiles.Profiles(character),AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat"));
            bindings.Update(0,false);
            var internalRenderers=character.GetComponentsInChildren<WetTransparencyRenderer>(true);
            Need(internalRenderers.Length==3,"One proxy per renderer, not one per material slot");
            Need(WetCharacterCatalog.Scan(character).Entries.Count==before.Entries.Count,"Multislot discovery unchanged");
            int skins=0;
            foreach(var sync in internalRenderers)
            {
                var source=sync.Source as SkinnedMeshRenderer;var dest=sync.Proxy as SkinnedMeshRenderer;
                if(source==null)continue;
                Need(dest.sharedMesh==source.sharedMesh && dest.bones.SequenceEqual(source.bones) && dest.rootBone==source.rootBone,"Skinned geometry and bones shared");skins++;
            }
            Need(skins==3,"All three skinned surfaces mirrored");
            bindings.Dispose();bindings=null;Need(character.GetComponentsInChildren<WetTransparencyRenderer>(true).Length==0,"All proxies released after unbind");
            var result=new{passed=true,sky=Channels(sky),reference=Channels(reference),split=Channels(split),sharedSkinnedRenderers=skins,oneProxyPerRenderer=true,catalogStable=true,visibilityAndDisposal=true};
            System.IO.File.WriteAllText("Artifacts/WaterGun/split-render.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            bindings?.Dispose();pair?.Dispose();RenderTexture.active=previous;
            if(character!=null)UnityEngine.Object.DestroyImmediate(character);
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(material);
            target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(cpu);
        }
    }
    private static Vector3 Rgb(Color c)=>new Vector3(c.r,c.g,c.b);
    private static float[] Channels(Color c)=>new[]{c.r,c.g,c.b};
    private static void Need(bool ok,string name){if(!ok)throw new Exception("Split render: "+name);}
}
#endif
