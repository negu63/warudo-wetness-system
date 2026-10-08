#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SurfaceWetness.WaterGun;

// Diagnostic captures only: no edits to scene assets, source materials or installed packages.
public static class WaterGunVisualDiagnostics
{
    public static object Run(float opacity=.05f,float radius=.18f,int stamps=24,RenderingPath renderingPath=RenderingPath.Forward)
    {
        string output=".codex-artifacts/WaterGun/visual-diagnostics-opacity"+opacity.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"-radius"+radius.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
        if(renderingPath!=RenderingPath.Forward)output+="-"+renderingPath;
        System.IO.Directory.CreateDirectory(output);
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WetnessDemoCharacter/Character.prefab"));
        var cameraObject=new GameObject("DiagnosticCamera");var lightObject=new GameObject("DiagnosticLight");
        var target=new RenderTexture(512,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var cpu=new Texture2D(512,768,TextureFormat.RGBA32,false,true);
        var previous=RenderTexture.active;var previousShadows=QualitySettings.shadows;var previousDistance=QualitySettings.shadowDistance;
        WetSurfaceBindings bindings=null;
        try
        {
            var animator=root.GetComponentInChildren<Animator>();if(animator!=null)animator.enabled=false;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            var catalog=WetCharacterCatalog.Scan(root);var profiles=catalog.Entries.Select(e=>new WetSurfaceProfile
            {Key=e.Key,Order=e.Renderer.name=="OuterCloth"?0:e.Renderer.name=="InnerCloth"?1:2,
             Opacity=e.Renderer.name!="Skin",StopAfterWet=e.Renderer.name=="Skin",Drying=false,WetOpacity=opacity}).ToArray();
            bindings=new WetSurfaceBindings(catalog,profiles,AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat"));
            var outer=bindings.Surfaces.First(s=>s.Entry.Renderer.name=="OuterCloth");
            var inner=bindings.Surfaces.First(s=>s.Entry.Renderer.name=="InnerCloth");
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.2f,.2f,.2f);
            camera.renderingPath=renderingPath;camera.fieldOfView=35;camera.depthTextureMode=DepthTextureMode.Depth;
            camera.targetTexture=target;camera.nearClipPlane=.05f;camera.farClipPlane=10;
            var center=outer.Entry.Renderer.bounds.center;
            camera.transform.position=center+new Vector3(0,.12f,-1.5f);camera.transform.LookAt(center);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<31;
            light.transform.rotation=Quaternion.Euler(25,-30,0);light.intensity=1;
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=10;
            Func<string,Color32[]> capture=name=>
            {
                bindings.Update(0,false);camera.Render();RenderTexture.active=target;
                cpu.ReadPixels(new Rect(0,0,512,768),0,0);cpu.Apply();
                System.IO.File.WriteAllBytes(output+"/"+name+".png",cpu.EncodeToPNG());return cpu.GetPixels32();
            };
            var shadowDifferences=new int[2];var dryMaximum=new int[2];
            for(int shadows=0;shadows<2;shadows++)
            {
                light.shadows=shadows==0?LightShadows.None:LightShadows.Soft;
                foreach(var p in profiles)p.Opacity=false;
                var off=capture("dry-shadow"+shadows+"-off");
                outer.Profile.Opacity=true;inner.Profile.Opacity=true;
                var on=capture("dry-shadow"+shadows+"-on");
                for(int i=0;i<on.Length;i++)
                {
                    int error=Error(on[i],off[i]);dryMaximum[shadows]=Math.Max(dryMaximum[shadows],error);
                    if(error>2)shadowDifferences[shadows]++;
                }
            }
            for(int shadows=0;shadows<2;shadows++)
                if(shadowDifferences[shadows]!=0 || dryMaximum[shadows]>2)
                    throw new Exception("Dry opacity toggle changed lighting/shadows: "+shadowDifferences[shadows]);
            // Fix one world-space hit and all three masks, then change only inner opacity.
            var bounds=outer.Entry.Renderer.bounds;SurfaceWetness.WaterGun.WetRayHit hit;WetSurfaceBindings.Surface selected;
            var shot=new Ray(new Vector3(bounds.center.x,bounds.center.y+.1f,bounds.min.z-1),Vector3.forward);
            if(!bindings.Trace(shot,8,out hit,out selected))throw new Exception("No fixed character hit");
            int path=bindings.TracePath(shot,8,.5f,true);
            for(int i=0;i<stamps;i++)bindings.SupplyPath(shot.direction,radius,.3f,.1f);
            // Wet PBR remains active, but alpha=1 everywhere: toggle must not alter shadows.
            foreach(var p in profiles){p.Opacity=false;p.WetOpacity=1;}
            var wetSolidOff=capture("wet-target1-off-shadow1");
            outer.Profile.Opacity=true;inner.Profile.Opacity=true;
            var wetSolidOn=capture("wet-target1-on-shadow1");int wetSolidChanged=0,wetSolidMaximum=0;
            for(int i=0;i<wetSolidOn.Length;i++)
            {
                int error=Error(wetSolidOn[i],wetSolidOff[i]);wetSolidMaximum=Math.Max(wetSolidMaximum,error);
                if(error>2)wetSolidChanged++;
            }
            if(wetSolidChanged!=0 || wetSolidMaximum>2)throw new Exception("Wet opaque pixels changed lighting/shadows on toggle");
            foreach(var p in profiles)p.WetOpacity=opacity;
            outer.Profile.Opacity=true;inner.Profile.Opacity=false;
            var wetOff=capture("wet-inner-off-shadow1");inner.Profile.Opacity=true;
            var wetOn=capture("wet-inner-on-shadow1");
            int changedWet=0;for(int i=0;i<wetOn.Length;i++)if(Error(wetOn[i],wetOff[i])>2)changedWet++;
            light.shadows=LightShadows.None;
            inner.Profile.Opacity=false;capture("wet-inner-off-shadow0");inner.Profile.Opacity=true;capture("wet-inner-on-shadow0");
            camera.fieldOfView=22;camera.transform.LookAt(center+Vector3.up*.12f);
            inner.Profile.Opacity=false;capture("zoom-inner-off");inner.Profile.Opacity=true;capture("zoom-inner-on");
            camera.transform.position=center+new Vector3(0,-.6f,-1.5f);camera.transform.LookAt(center+Vector3.up*.12f);capture("zoom-inner-on-below");
            camera.transform.position=center+new Vector3(0,.6f,-1.5f);camera.transform.LookAt(center+Vector3.up*.12f);capture("zoom-inner-on-above");
            camera.transform.position=center+new Vector3(0,.12f,-1.5f);camera.transform.LookAt(center+Vector3.up*.12f);
            inner.Profile.WetOpacity=1;capture("zoom-inner-on-target1");inner.Profile.WetOpacity=opacity;
            var result=new {resolution="512x768",renderingPath=renderingPath.ToString(),actualRenderingPath=camera.actualRenderingPath.ToString(),maskResolution=512,
                dryChangedPixels=shadowDifferences,dryMaxByteError=dryMaximum,wetChangedPixels=changedWet,
                wetOpaqueToggleChangedPixels=wetSolidChanged,wetOpaqueToggleMaxByteError=wetSolidMaximum,
                shotPathCount=path,outerStamps=outer.Binding.State.StampCount,innerStamps=inner.Binding.State.StampCount,
                opacity,radius,stamps,output,diagnosticOnly=true};
            System.IO.File.WriteAllText(output+"/results.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            RenderTexture.active=previous;bindings?.Dispose();QualitySettings.shadows=previousShadows;QualitySettings.shadowDistance=previousDistance;
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(lightObject);
            target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(cpu);
        }
    }
    private static int Error(Color32 a,Color32 b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)));
}
#endif
