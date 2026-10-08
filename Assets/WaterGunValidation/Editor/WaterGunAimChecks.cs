#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SurfaceWetness.WaterGun;

public static class WaterGunAimChecks
{
    public static object Run(bool rotated=false)
    {
        var root=new GameObject("AimValidationSurfaces");
        var mesh=new Mesh();
        var material=new Material(Shader.Find("WetnessDemoCharacter/WetMaterial"));
        var host=PrefabUtility.LoadPrefabContents("Assets/WaterGun/Prefabs/WaterGunRuntime.prefab");
        WetSurfaceBindings bindings=null;WaterGunDriver driver=null;
        var evidence=new List<object>();var previous=RenderTexture.active;
        var sample=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        try
        {
            if(rotated){root.transform.SetPositionAndRotation(new Vector3(3,1,2),Quaternion.Euler(17,60,9));}
            mesh.vertices=new[]{new Vector3(-2,-2,0),new Vector3(-2,2,0),new Vector3(2,2,0),new Vector3(2,-2,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();
            for(int i=0;i<3;i++)
            {
                var node=new GameObject("Layer"+i);node.transform.SetParent(root.transform,false);
                node.transform.localPosition=Vector3.forward*(i*.02f);
                node.AddComponent<MeshFilter>().sharedMesh=mesh;
                node.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            var catalog=WetCharacterCatalog.Scan(root);var profiles=new WetSurfaceProfile[3];
            for(int i=0;i<3;i++)profiles[i]=new WetSurfaceProfile{Key=catalog.Entries[i].Key,Order=i,StopAfterWet=i==2};
            bindings=new WetSurfaceBindings(catalog,profiles,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat"));
            var cameraObject=new GameObject("AimValidationCamera");cameraObject.transform.SetParent(host.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.01f;
            driver=host.AddComponent<WaterGunDriver>();driver.Bindings=bindings;driver.DisplayCamera=camera;
            driver.ShowHud=false;driver.Drying=false;driver.Drawn=true;
            driver.Initialize(AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/WaterStream.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/WaterSplash.mat"));
            foreach(float distance in new[]{.5f,.2f,.08f,.65f,.8f,1f,2f})
            foreach(var viewport in new[]{new Vector2(.5f,.5f),new Vector2(.62f,.43f),new Vector2(.15f,.85f),new Vector2(.85f,.15f)})
            {
                camera.transform.SetPositionAndRotation(root.transform.TransformPoint(new Vector3(0,0,-distance)),root.transform.rotation);
                var pointer=new Vector2(Screen.width*viewport.x,Screen.height*viewport.y);
                var ray=camera.ViewportPointToRay(new Vector3(viewport.x,viewport.y,0));
                SurfaceWetness.WaterGun.WetRayHit expected;WetSurfaceBindings.Surface selected;
                Need(bindings.Trace(ray,8,out expected,out selected),"Fixture camera hits outer layer");
                driver.Tick(1f/60,pointer,false,true);
                driver.Tick(1f/60,pointer,true,true);
                var pivot=host.transform.Find("WaterGunRig/Pivot");
                float angle=Vector3.Angle(pivot.forward,ray.direction);
                Need(angle<25,"Gun follows screen aim without sideways/reverse bend at distance "+distance+" (angle="+angle+")");
                float muzzleZ=root.transform.InverseTransformPoint(driver.Gun.MuzzlePosition).z;
                Need(muzzleZ<-.005f,"Muzzle stays in front of outer surface at distance "+distance);
                Need(driver.Held && driver.Gun.Firing && bindings.PathCount==3,"Near shot fires and transfers through all layers");
                Need(Vector3.Distance(driver.Gun.EndPoint,expected.point)<.0001f,"Stream ends at screen aim");
                Need(Vector3.Distance(bindings.Path[0].Hit.point,expected.point)<.0001f,"Wetness uses screen aim");
                var mask=bindings.Path[0].Surface.Binding.State.Mask;
                RenderTexture.active=mask;
                sample.ReadPixels(new Rect(Mathf.Clamp((int)(expected.textureCoord.x*mask.width),0,mask.width-1),
                    Mathf.Clamp((int)(expected.textureCoord.y*mask.height),0,mask.height-1),1,1),0,0);sample.Apply();
                Need(sample.GetPixel(0,0).r>.001f,"Wetness mask receives supply at screen hit UV");
                RenderTexture.active=previous;
                evidence.Add(new{distance,viewport=new{x=viewport.x,y=viewport.y},angle,muzzleZ,path=bindings.PathCount});
            }
            foreach(var surface in bindings.Surfaces)Need(surface.Binding.State.StampCount==28,"All aimed shots deposit to each layer");
            driver.Transfer=false;driver.Tick(1f/60,null,false,true);
            driver.Tick(1f/60,new Vector2(Screen.width*.5f,Screen.height*.5f),true,true);
            Need(bindings.PathCount==1,"Transfer OFF remains direct-only");
            // Aim outside the registered planes: the muzzle must not invent a different target.
            camera.transform.position=root.transform.TransformPoint(new Vector3(10,0,-.5f));
            driver.Tick(1f/60,new Vector2(Screen.width*.5f,Screen.height*.5f),true,true);
            Need(bindings.PathCount==0 && driver.Status=="표면 없음","Screen miss clears path and supplies no surface");
            return new{rotated,cases=evidence,screenAim=true,nearMuzzleOutside=true,maskSupply=true,transferOff=true,screenMiss=true};
        }
        finally
        {
            RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(sample);
            driver?.Gun?.Dispose();bindings?.Dispose();
            PrefabUtility.UnloadPrefabContents(host);
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(material);
        }
    }
    private static void Need(bool condition,string message){if(!condition)throw new Exception("WaterGun aim: "+message);}
}
#endif
