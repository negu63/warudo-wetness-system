#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using SurfaceWetness.WaterGun;

public static class WaterGunCharacterDryChecks
{
    public static object Run()
    {
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WetnessDemoCharacter/Character.prefab"));
        var cameraObject=new GameObject("Dry character validation camera");var lightObject=new GameObject("Dry character validation light");
        var target=new RenderTexture(256,384,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var cpu=new Texture2D(256,384,TextureFormat.RGBA32,false,true);var previous=RenderTexture.active;
        var materials=new System.Collections.Generic.List<Material>();
        try
        {
            var animator=root.GetComponentInChildren<Animator>();if(animator!=null)animator.enabled=false;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            var renderers=root.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers)
            {
                bounds.Encapsulate(r.bounds);var array=r.sharedMaterials;
                for(int i=0;i<array.Length;i++)
                {
                    array[i]=new Material(array[i]);array[i].SetFloat("_Wetness",0);array[i].SetFloat("_UseWetMask",0);
                    materials.Add(array[i]);
                }
                r.sharedMaterials=array;
            }
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;
            camera.orthographicSize=bounds.extents.y*1.1f;camera.targetTexture=target;
            var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<31;light.shadows=LightShadows.None;
            Func<Color32[]> pixels=()=>{camera.Render();RenderTexture.active=target;cpu.ReadPixels(new Rect(0,0,256,384),0,0);cpu.Apply();return cpu.GetPixels32();};
            var differences=new int[3];var maximum=new int[3];
            for(int view=0;view<3;view++)
            {
                var rotation=Quaternion.Euler(0,(view-1)*35,0);camera.transform.position=bounds.center+rotation*new Vector3(0,0,-3);
                camera.transform.LookAt(bounds.center);light.transform.rotation=camera.transform.rotation;
                foreach(var m in materials){m.SetFloat("_WetOpacityEnabled",0);WetShaderAdapter.SetOpacityMode(m,false);}
                var reference=pixels();
                foreach(var r in renderers)foreach(var m in r.sharedMaterials)
                {
                    bool cloth=r.name!="Skin";m.SetFloat("_WetOpacityEnabled",cloth?1:0);
                    WetShaderAdapter.SetOpacityMode(m,cloth,r.name=="OuterCloth"?0:1);
                }
                var actual=pixels();
                for(int i=0;i<actual.Length;i++)
                {
                    int error=Math.Max(Math.Abs(actual[i].r-reference[i].r),Math.Max(Math.Abs(actual[i].g-reference[i].g),Math.Abs(actual[i].b-reference[i].b)));
                    maximum[view]=Math.Max(maximum[view],error);if(error>2)differences[view]++;
                }
                if(differences[view]>0)throw new Exception("Dry character ON/OFF changed "+differences[view]+" pixels at view "+view+" max="+maximum[view]);
            }
            var result=new{views=3,resolution="256x384",pixelsPerView=256*384,changedPixels=differences,maxByteError=maximum,passed=true};
            System.IO.File.WriteAllText(".codex-artifacts/WaterGun/character-dry-render.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(lightObject);
            foreach(var m in materials)UnityEngine.Object.DestroyImmediate(m);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(cpu);
        }
    }
}
#endif
