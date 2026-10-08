#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using SurfaceWetness.WaterGun;
using SurfaceHit = SurfaceWetness.WaterGun.WetRayHit;

public static class WaterGunChecks
{
    public static object Phase2()
    {
        var root=PrefabUtility.LoadPrefabContents("Assets/WetnessDemoCharacter/Character.prefab");
        WetSingleSurface surface=null;Texture2D cpu=null;
        var previous=RenderTexture.active;
        try
        {
            var entry=WetCharacterCatalog.Scan(root).Entries.First(e=>e.Renderer.name=="OuterCloth");
            var original=entry.Material;
            int colliders=root.GetComponentsInChildren<Collider>(true).Length;
            surface=new WetSingleSurface(entry,AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat"),512);
            var bounds=entry.Renderer.bounds;SurfaceHit hit=default(SurfaceHit);Ray ray=default(Ray);bool found=false;
            for(int side=0;side<2 && !found;side++)for(int y=2;y<9 && !found;y++)for(int x=2;x<9 && !found;x++)
            {
                var target=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x/10f),Mathf.Lerp(bounds.min.y,bounds.max.y,y/10f),bounds.center.z);
                ray=new Ray(target+Vector3.forward*(side==0 ? -3:3),side==0 ? Vector3.forward:Vector3.back);
                found=surface.Trace(ray,8,out hit);
            }
            Require(found,"Real outer hit");
            surface.Deposit(hit,ray.direction,.05f,.5f,.7f);surface.Apply(.2f,.65f,true,.35f);
            cpu=new Texture2D(512,512,TextureFormat.RGBAFloat,false,true);
            Func<float> sample=()=>
            {
                RenderTexture.active=surface.State.Mask;cpu.ReadPixels(new Rect(0,0,512,512),0,0);cpu.Apply();
                return cpu.GetPixel(Mathf.Clamp((int)(hit.textureCoord.x*512),0,511),Mathf.Clamp((int)(hit.textureCoord.y*512),0,511)).r;
            };
            float wet=sample();surface.State.Dry(40,20);float dry=sample();
            surface.Deposit(hit,ray.direction,.05f,.5f,.7f);float again=sample();
            Require(wet>.5f && dry<.0001f && again>.5f,"Deposit/dry/rewet");
            surface.Trace(ray,8,out hit);int refresh=surface.GeometryRefreshCount;Require(refresh==1,"One refresh per frame");
            var host=PrefabUtility.LoadPrefabContents("Assets/WaterGun/Prefabs/WaterGunRuntime.prefab");
            try
            {
                var cameraObject=new GameObject("ValidationCamera");cameraObject.transform.SetParent(host.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
                camera.transform.SetPositionAndRotation(ray.origin,Quaternion.LookRotation(ray.direction));
                var driver=host.AddComponent<WaterGunDriver>();driver.Surface=surface;driver.DisplayCamera=camera;
                driver.ShowHud=false;driver.Drying=false;
                driver.Initialize(AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/WaterStream.mat"),
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/WaterSplash.mat"));
                var pointer=new Vector2(Screen.width*.5f,Screen.height*.5f);
                driver.Surface=null;driver.Drawn=true;
                driver.Tick(1f/60,pointer,true,true);
                var rig=host.transform.Find("WaterGunRig");
                Require(!driver.Held && !driver.Gun.Firing && driver.Status.Contains("표면 0개"),"Unregistered surface explains blocked firing");
                Require(Vector3.Distance(rig.position,camera.transform.TransformPoint(new Vector3(.2f,-.13f,.65f)))<.00001f,"Unregistered gun remains camera mounted");
                driver.Surface=surface;driver.ShowHud=true;
                driver.Tick(1f/60,new Vector2(30,Screen.height-30),true,true);
                Require(!driver.Held && !driver.Gun.Firing && driver.Status.Contains("HUD"),"HUD explains input block");
                camera.transform.position+=Vector3.right;
                driver.Tick(1f/60,new Vector2(30,Screen.height-30),false,true);
                Require(Vector3.Distance(rig.position,camera.transform.TransformPoint(new Vector3(.2f,-.13f,.65f)))<.00001f,"Blocked gun follows moving camera");
                camera.transform.position=ray.origin;driver.ShowHud=false;
                driver.Drawn=false;driver.Tick(1f/60,pointer,true,true);Require(!driver.Held && !driver.Gun.Firing,"Holstered input off");
                driver.Drawn=true;driver.Tick(1f/60,pointer,true,true);Require(!driver.Held,"Draw while held blocked");
                driver.Tick(1f/60,pointer,false,true);driver.Tick(1f/60,pointer,true,true);
                Require(driver.Held && driver.Gun.Firing && driver.Deposits==1,"Camera/muzzle firing");
                driver.Drawn=false;driver.Drying=true;driver.Tick(40,pointer,false,true);
                Require(!driver.Gun.Firing && sample()<.0001f,"Drying while holstered");
                driver.Surface=null;
            }
            finally{PrefabUtility.UnloadPrefabContents(host);}
            RenderTexture.active=previous;
            surface.Dispose();surface.Dispose();Require(entry.Renderer.sharedMaterials[entry.Slot]==original,"Original restored");
            Require(root.GetComponentsInChildren<Collider>(true).Length==colliders,"No collider changes");
            var evidence=new{wet,dry,again,geometryRefresh=refresh,u=hit.textureCoord.x,v=hit.textureCoord.y,originalRestored=true,collidersUnchanged=true,
                holsteredInputOff=true,drawWhileHeldBlocked=true,cameraAndMuzzleFiring=true,dryingWhileHolstered=true,
                unregisteredGunCameraMounted=true,blockedGunFollowsCamera=true,missingSurfaceAndHudDiagnostics=true};
            System.IO.Directory.CreateDirectory(".codex-artifacts/WaterGun");
            System.IO.File.WriteAllText(".codex-artifacts/WaterGun/phase2-regression.json",Newtonsoft.Json.JsonConvert.SerializeObject(evidence,Newtonsoft.Json.Formatting.Indented));
            return evidence;
        }
        finally
        {
            RenderTexture.active=previous;if(surface!=null)surface.Dispose();
            if(cpu!=null)UnityEngine.Object.DestroyImmediate(cpu);PrefabUtility.UnloadPrefabContents(root);
        }
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception("WaterGun check failed: "+message);}
    public static object Phase3()
    {
        var root=new GameObject("RenamedCharacter");var mesh=new Mesh{name="four slots with base vertices"};
        var material=new Material(Shader.Find("WetnessDemoCharacter/WetMaterial"));
        WetSurfaceBindings bindings=null;Texture2D cpu=null;var previous=RenderTexture.active;
        try
        {
            var node=new GameObject("ArbitraryMesh");node.transform.SetParent(root.transform,false);
            var renderer=node.AddComponent<SkinnedMeshRenderer>();
            var vertices=new Vector3[16];var uv=new Vector2[16];var weights=new BoneWeight[16];
            for(int s=0;s<4;s++)
            {
                int b=s*4;float x=s*2;
                vertices[b]=new Vector3(x-.5f,-.5f,0);vertices[b+1]=new Vector3(x-.5f,.5f,0);
                vertices[b+2]=new Vector3(x+.5f,.5f,0);vertices[b+3]=new Vector3(x+.5f,-.5f,0);
                uv[b]=Vector2.zero;uv[b+1]=Vector2.up;uv[b+2]=Vector2.one;uv[b+3]=Vector2.right;
                for(int i=0;i<4;i++)weights[b+i]=new BoneWeight{boneIndex0=0,weight0=1};
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.boneWeights=weights;mesh.bindposes=new[]{Matrix4x4.identity};mesh.subMeshCount=4;
            for(int s=0;s<4;s++)mesh.SetIndices(new[]{0,1,2,0,2,3},MeshTopology.Triangles,s,true,s*4);
            mesh.RecalculateNormals();mesh.RecalculateBounds();renderer.sharedMesh=mesh;renderer.bones=new[]{node.transform};
            renderer.rootBone=node.transform;renderer.sharedMaterials=new[]{material,material,material,material};
            var collider=node.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
            var update=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat");
            var coverage=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat");
            var entries=WetCharacterCatalog.Scan(root).Entries;
            var profiles=entries.Select(e=>new WetSurfaceProfile{Key=e.Key}).ToArray();
            foreach(int count in new[]{1,3,4})
            {
                bindings=new WetSurfaceBindings(WetCharacterCatalog.Scan(root),profiles.Take(count).ToArray(),update,coverage);
                Require(bindings.Surfaces.Count==count && bindings.GeometryCount==1,"Variable slot count/shared geometry");
                bindings.Dispose();bindings=null;
            }
            bindings=new WetSurfaceBindings(WetCharacterCatalog.Scan(root),profiles,update,coverage);
            bool refused=false;
            try{var duplicate=new WetSurfaceBindings(WetCharacterCatalog.Scan(root),profiles,update,coverage);duplicate.Dispose();}
            catch(InvalidOperationException){refused=true;}
            Require(refused && bindings.IsCurrent(),"Duplicate ownership refused without touching first owner");
            cpu=new Texture2D(512,512,TextureFormat.RGBAFloat,false,true);
            for(int s=0;s<4;s++)
            {
                var ray=new Ray(new Vector3(s*2+.12f,.03f,-2),Vector3.forward);
                SurfaceHit hit;WetSurfaceBindings.Surface selected;
                Require(bindings.Trace(ray,8,out hit,out selected) && selected.Binding.Entry.Slot==s,"Base vertex slot "+s);
                RaycastHit physics;Require(collider.Raycast(ray,out physics,8) && physics.triangleIndex==hit.triangleIndex,"PhysX triangle mapping");
                Require((physics.textureCoord-hit.textureCoord).sqrMagnitude<.000001f,"PhysX UV mapping");
                selected.Binding.Deposit(hit,ray.direction,.15f,.5f,.8f);
                for(int i=0;i<4;i++)Require(bindings.Surfaces[i].Binding.State.StampCount==(i<=s ? 1:0),"Only hit slot stamped");
                RenderTexture.active=selected.Binding.State.Mask;cpu.ReadPixels(new Rect(0,0,512,512),0,0);cpu.Apply();
                Require(cpu.GetPixel((int)(hit.textureCoord.x*512),(int)(hit.textureCoord.y*512)).r>.7f,"Slot mask sample");
            }
            Require(bindings.Surfaces.All(s=>s.Binding.Geometry==bindings.Surfaces[0].Binding.Geometry && s.Binding.GeometryRefreshCount==1),"One snapshot across slots");
            Require(bindings.Surfaces.Select(s=>s.Binding.RuntimeMaterial).Distinct().Count()==4,"Independent material clones");
            Require(bindings.Surfaces.Select(s=>s.Binding.State.Mask).Distinct().Count()==4,"Independent masks with same UV");
            profiles[0].Darkening=.1f;profiles[1].Darkening=.7f;bindings.Update(0,false);
            Require(bindings.Surfaces[0].Binding.RuntimeMaterial.GetFloat("_WetDarkening")==.1f && bindings.Surfaces[1].Binding.RuntimeMaterial.GetFloat("_WetDarkening")==.7f,"Live per-slot settings");
            profiles[0].Opacity=true;bindings.Update(0,false);
            Require(bindings.Surfaces[0].Binding.RuntimeMaterial.IsKeywordEnabled("_WET_TRANSPARENT") && bindings.Surfaces[0].Binding.RuntimeMaterial.renderQueue==2000,"Live opacity keeps opaque stage while enabling split transparency");
            Require(!bindings.Surfaces[1].Binding.RuntimeMaterial.IsKeywordEnabled("_WET_TRANSPARENT") && bindings.Surfaces[1].Binding.RuntimeMaterial.renderQueue==2000,"Same shader independent opaque slot");
            profiles[0].Opacity=false;bindings.Update(0,false);
            Require(!bindings.Surfaces[0].Binding.RuntimeMaterial.IsKeywordEnabled("_WET_TRANSPARENT") && bindings.Surfaces[0].Binding.State.StampCount==1,"Toggle keeps wet mask and returns opaque");
            RenderTexture.active=previous;bindings.Dispose();bindings=null;
            Require(renderer.sharedMaterials.All(m=>m==material),"All shared originals restored");
            var bad=new Material(Shader.Find("Standard"));renderer.sharedMaterials=new[]{bad,material,material,material};
            try{Require(WetCharacterCatalog.Scan(root).CandidateCount==3,"Unsupported shader excluded");}finally{UnityEngine.Object.DestroyImmediate(bad);}
            material.SetOverrideTag("SurfaceWetnessContract","2");
            Require(WetShaderAdapter.Check(material)=="젖음 계약 버전 불일치","Contract mismatch refused");
            material.SetOverrideTag("SurfaceWetnessContract","");
            var evidence=new{counts=new[]{1,3,4},geometryOwners=1,refreshPerFrame=1,baseVertexAndPhysXMatch=true,onlyHitSlot=true,independentSharedMaterials=true,independentSharedUvMasks=true,liveSettings=true,restored=true,unsupportedExcluded=true,duplicateRefused=true,contractMismatchRefused=true};
            System.IO.File.WriteAllText(".codex-artifacts/WaterGun/phase3-regression.json",Newtonsoft.Json.JsonConvert.SerializeObject(evidence,Newtonsoft.Json.Formatting.Indented));
            return evidence;
        }
        finally
        {
            RenderTexture.active=previous;bindings?.Dispose();if(cpu!=null)UnityEngine.Object.DestroyImmediate(cpu);
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);
        }
    }
    public static object Phase4()
    {
        var root=new GameObject("TransferFixture");var mesh=new Mesh();
        var material=new Material(Shader.Find("WetnessDemoCharacter/WetMaterial"));
        var unsupported=new Material(Shader.Find("Standard"));WetSurfaceBindings bindings=null;
        try
        {
            mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(1,-1,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();
            var renderers=new Renderer[4];var profiles=new WetSurfaceProfile[4];
            for(int i=0;i<4;i++)
            {
                var node=new GameObject("Layer"+i);node.transform.SetParent(root.transform,false);node.transform.localPosition=Vector3.forward*(i*.02f);
                node.AddComponent<MeshFilter>().sharedMesh=mesh;renderers[i]=node.AddComponent<MeshRenderer>();renderers[i].sharedMaterial=material;
            }
            var entries=WetCharacterCatalog.Scan(root).Entries;
            for(int i=0;i<4;i++)profiles[i]=new WetSurfaceProfile{Key=entries[i].Key,Order=i,StopAfterWet=i==2,Opacity=false};
            var update=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat");
            var coverage=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat");
            var ray=new Ray(new Vector3(.1f,.05f,-1),Vector3.forward);
            bindings=new WetSurfaceBindings(WetCharacterCatalog.Scan(root),profiles,update,coverage);
            Require(bindings.TracePath(ray,8,.15f,true)==3,"Outer-inner-body stop");
            bindings.SupplyPath(ray.direction,.15f,.5f,.8f);bindings.Update(0,false);
            Require(bindings.Surfaces.Take(3).All(s=>s.Binding.State.StampCount==1) && bindings.Surfaces[3].Binding.State.StampCount==0,"Back garment dry");
            Require(bindings.Surfaces[0].Binding.RuntimeMaterial.GetFloat("_WetOpacityEnabled")==0,"Opacity off still transmits");
            Require(bindings.TracePath(new Ray(new Vector3(.1f,.05f,.03f),Vector3.forward),8,.15f,true)==1,"Direct body stops");
            renderers[1].enabled=false;Require(bindings.TracePath(ray,8,.15f,true)==2,"No inner: outer-body");renderers[1].enabled=true;
            Require(bindings.TracePath(ray,8,.01f,true)==1,"Depth limit");
            Require(bindings.TracePath(ray,8,.15f,false)==1,"Transfer toggle off");
            profiles[0].Transmit=false;Require(bindings.TracePath(ray,8,.15f,true)==1,"Per-surface transmit off");profiles[0].Transmit=true;
            profiles[0].Order=2;profiles[1].Order=1;profiles[2].Order=0;
            Require(bindings.TracePath(ray,8,.15f,true)==2 && bindings.Path[1].Surface.Profile==profiles[2],"Inverted order skips cloth but body barrier remains");
            profiles[0].Order=0;profiles[1].Order=0;profiles[2].Order=2;
            Require(bindings.TracePath(ray,8,.15f,true)==3,"Same-order garments: physical distance");
            bindings.Dispose();bindings=null;
            renderers[2].sharedMaterial=unsupported;profiles[2].Enabled=false;profiles[2].BlockOnly=true;mesh.uv=null;
            // Blocker can lack UV, but wet surfaces must keep UV. Give blocker its own mesh.
            var blockerMesh=UnityEngine.Object.Instantiate(mesh);mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
            renderers[2].GetComponent<MeshFilter>().sharedMesh=blockerMesh;
            try
            {
                bindings=new WetSurfaceBindings(WetCharacterCatalog.Scan(root),profiles,update,coverage);
                Require(bindings.TracePath(ray,8,.15f,true)==3 && bindings.Path[2].Surface.Binding==null,"Unsupported UV-less blocker without RT");
                bindings.SupplyPath(ray.direction,.15f,.5f,.8f);
                Require(bindings.Surfaces[3].Binding.State.StampCount==0 && renderers[2].sharedMaterial==unsupported,"Blocker keeps material and stops behind");
                Require(bindings.TracePath(new Ray(new Vector3(.1f,.05f,.03f),Vector3.forward),8,.15f,true)==1,"Direct blocker stops");
                bindings.Dispose();bindings=null;
            }
            finally{UnityEngine.Object.DestroyImmediate(blockerMesh);}
            var evidence=new{outerInnerBody=true,backGarmentDry=true,directBody=true,noInner=true,depthLimit=true,transferOff=true,opacityIndependent=true,invertedOrderBarrier=true,sameOrderDistance=true,unsupportedUvlessBlockerNoRT=true,blockerMaterialPreserved=true};
            System.IO.File.WriteAllText(".codex-artifacts/WaterGun/phase4-transfer.json",Newtonsoft.Json.JsonConvert.SerializeObject(evidence,Newtonsoft.Json.Formatting.Indented));return evidence;
        }
        finally{bindings?.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(unsupported);}
    }
}
#endif
