#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using SurfaceWetness.WaterGun;

public static class WaterGunOpacityChecks
{
    public static object Run(Shader suppliedShader=null)
    {
        var root=new GameObject("Opacity render validation");
        var materials=new Material[3];var layers=new GameObject[3];
        var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var cpu=new Texture2D(64,64,TextureFormat.RGBA32,false,true);
        Texture2D mask=null;
        var previous=RenderTexture.active;
        try
        {
            var cameraObject=new GameObject("ValidationCamera");cameraObject.transform.SetParent(root.transform);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
            camera.transform.position=new Vector3(0,0,-3);camera.orthographic=true;camera.orthographicSize=.5f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.targetTexture=target;
            var lightObject=new GameObject("ValidationLight");lightObject.transform.SetParent(root.transform);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<31;light.shadows=LightShadows.None;
            var shader=suppliedShader ?? AssetDatabase.LoadAssetAtPath<Shader>("Assets/WetnessDemoCharacter/Shaders/WetMaterial.shader");
            var colors=new[]{Color.red,Color.blue,Color.green};
            for(int i=0;i<3;i++)
            {
                layers[i]=GameObject.CreatePrimitive(PrimitiveType.Quad);layers[i].transform.SetParent(root.transform);layers[i].layer=31;
                layers[i].transform.localPosition=new Vector3(0,0,-.1f*i);
                materials[i]=new Material(shader);materials[i].SetColor("_Color",colors[i]);
                materials[i].SetFloat("_Wetness",1);materials[i].SetFloat("_WetOpacity",0);
                materials[i].SetFloat("_WetDarkening",0);materials[i].SetFloat("_WetSmoothness",0);materials[i].SetFloat("_DrySmoothness",0);
                materials[i].SetFloat("_WetOpacityEnabled",1);WetShaderAdapter.SetOpacityMode(materials[i],i>0,2-i);
                layers[i].GetComponent<Renderer>().sharedMaterial=materials[i];
            }
            Func<int,Color> sampleAt=x=>{camera.Render();RenderTexture.active=target;cpu.ReadPixels(new Rect(0,0,64,64),0,0);cpu.Apply();return cpu.GetPixel(x,32);};
            Func<Color> sample=()=>sampleAt(32);
            layers[1].SetActive(false);layers[2].SetActive(false);Color skin=sample();
            layers[1].SetActive(true);layers[2].SetActive(true);Color throughBoth=sample();
            Require(Vector3.Distance(Rgb(skin),Rgb(throughBoth))<.03f && skin.r>skin.b+.1f,"Both transparent layers reveal skin");
            materials[1].SetFloat("_WetOpacity",.5f);materials[2].SetFloat("_WetOpacity",.5f);Color partial=sample();
            Require(partial.r>.1f && partial.b>.1f && partial.g>.1f,"Both half-opacity layers contribute with skin");
            materials[1].SetFloat("_WetOpacity",0);materials[2].SetFloat("_WetOpacity",0);
            WetShaderAdapter.SetOpacityMode(materials[1],false);materials[1].SetFloat("_WetOpacityEnabled",0);Color innerOpaque=sample();
            Require(innerOpaque.b>innerOpaque.r+.1f,"Inner OFF blocks skin");
            WetShaderAdapter.SetOpacityMode(materials[1],true);materials[1].SetFloat("_WetOpacityEnabled",1);
            materials[1].SetFloat("_Wetness",0);materials[2].SetFloat("_Wetness",0);Color dry=sample();
            Require(dry.g>dry.r+.1f,"Dry transparent-mode cloth remains opaque");
            // Force the inner renderer to draw after the outer, as ambiguous bounds can do.
            materials[1].renderQueue=3100;Color dryWrongOrder=sample();
            Require(Vector3.Distance(Rgb(dry),Rgb(dryWrongOrder))<.03f,"Dry outer occludes inner even with reversed renderer order");
            materials[1].renderQueue=3000;
            materials[1].SetFloat("_Wetness",1);materials[2].SetFloat("_Wetness",1);
            WetShaderAdapter.SetOpacityMode(materials[2],false);materials[2].SetFloat("_WetOpacityEnabled",0);Color outerOpaque=sample();
            Require(outerOpaque.g>outerOpaque.r+.1f,"Outer OFF blocks inner and skin");
            WetShaderAdapter.SetOpacityMode(materials[2],true);materials[2].SetFloat("_WetOpacityEnabled",1);Color reenabled=sample();
            Require(Vector3.Distance(Rgb(skin),Rgb(reenabled))<.03f,"Live toggle re-enables transparency");
            materials[1].SetFloat("_WetOpacity",.5f);materials[2].SetFloat("_WetOpacity",.5f);
            WetShaderAdapter.SetOpacityMode(materials[1],true,1);WetShaderAdapter.SetOpacityMode(materials[2],true,0);Color orderedWet=sample();
            Require(materials[1].renderQueue<materials[2].renderQueue && Vector3.Distance(Rgb(partial),Rgb(orderedWet))<.03f,"Layer order keeps correct wet compositing");
            materials[1].SetFloat("_WetOpacity",0);materials[2].SetFloat("_WetOpacity",0);
            mask=new Texture2D(2,1,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            mask.SetPixels(new[]{Color.black,Color.white});mask.Apply();
            materials[2].SetTexture("_WetMask",mask);materials[2].SetFloat("_UseWetMask",1);
            materials[1].renderQueue=3100;
            Color mixedDry=sampleAt(16),mixedWet=sampleAt(48);
            Require(mixedDry.g>mixedDry.r+.1f && mixedWet.r>mixedWet.g+.1f,"Partial mask writes dry depth without blocking wet transparency");
            var result=new {shader=shader.name,skin=Channels(skin),throughBoth=Channels(throughBoth),partial=Channels(partial),innerOpaque=Channels(innerOpaque),dry=Channels(dry),dryWrongOrder=Channels(dryWrongOrder),outerOpaque=Channels(outerOpaque),reenabled=Channels(reenabled),orderedWet=Channels(orderedWet),mixedDry=Channels(mixedDry),mixedWet=Channels(mixedWet),passed=true};
            System.IO.File.WriteAllText("Artifacts/WaterGun/opacity-render.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(root);
            foreach(var m in materials)if(m!=null)UnityEngine.Object.DestroyImmediate(m);
            target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(cpu);
            if(mask!=null)UnityEngine.Object.DestroyImmediate(mask);
        }
    }
    private static Vector3 Rgb(Color c)=>new Vector3(c.r,c.g,c.b);
    private static float[] Channels(Color c)=>new[]{c.r,c.g,c.b,c.a};
    private static void Require(bool condition,string name){if(!condition)throw new Exception("Opacity validation: "+name);}
}
#endif
