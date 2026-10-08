#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SurfaceWetness.WaterGun;
using Hit=SurfaceWetness.WaterGun.WetRayHit;

// Correctness control only: PhysX cooking is deliberately outside performance captures.
[DefaultExecutionOrder(500)]
public sealed class WaterGunDeformationChecks : MonoBehaviour
{
    public bool Complete { get; private set; }
    public string Failure { get; private set; }
    private GameObject target;
    private WetSurfaceBindings bindings;
    private bool oldBackfaces;
    private IEnumerator Start()
    {
        oldBackfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=false;
        target=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WetnessDemoCharacter/Character.prefab"));
        var loop=target.AddComponent<WetBoneLoop>();loop.Animate=false;
        var profiles=WetCharacterCatalog.Scan(target).Entries.Select(e=>new WetSurfaceProfile{Key=e.Key}).ToArray();
        bindings=new WetSurfaceBindings(WetCharacterCatalog.Scan(target),profiles,AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat"));
        var colliders=bindings.Surfaces.Select(s=>
        {
            var go=new GameObject("Physics control");go.transform.SetParent(s.Entry.Renderer.transform,false);return go.AddComponent<MeshCollider>();
        }).ToArray();
        int rays=0,hitPairs=0,poses=24;float maxDistanceError=0,maxUvError=0;
        for(int pose=0;pose<poses;pose++)
        {
            yield return null;
            try
            {
                loop.StressTest=pose>=12;loop.StressIntensity=1;loop.ApplyPose((pose%12)/12f);
                for(int s=0;s<bindings.Surfaces.Count;s++)
                {
                    var binding=bindings.Surfaces[s].Binding;Hit warm;
                    binding.Trace(new Ray(Vector3.zero,Vector3.forward),8,out warm);
                    colliders[s].sharedMesh=null;colliders[s].sharedMesh=binding.Geometry.Mesh;
                    var bounds=colliders[s].bounds;
                    for(int side=0;side<2;side++)for(int y=1;y<5;y++)for(int x=1;x<5;x++)
                    {
                        var aim=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,(x+.123f)/6),Mathf.Lerp(bounds.min.y,bounds.max.y,(y+.237f)/6),bounds.center.z);
                        var ray=new Ray(aim+Vector3.forward*(side==0 ? -3:3),side==0 ? Vector3.forward:Vector3.back);
                        Hit hit;RaycastHit physics;bool a=binding.Trace(ray,8,out hit),b=colliders[s].Raycast(ray,out physics,8);rays++;
                        if(a!=b)throw new Exception("Pose hit mismatch "+pose+" surface "+s);
                        if(!a)continue;hitPairs++;
                        float distance=Mathf.Abs(hit.distance-physics.distance),uv=(hit.textureCoord-physics.textureCoord).magnitude;
                        maxDistanceError=Mathf.Max(maxDistanceError,distance);maxUvError=Mathf.Max(maxUvError,uv);
                        if(distance>.0002f || uv>.002f)throw new Exception("Pose point/UV mismatch "+pose+" distance "+distance+" UV "+uv);
                    }
                }
            }
            catch(Exception e){Failure=e.ToString();Debug.LogException(e);Cleanup();yield break;}
        }
        var report=new{poses,basePoses=12,stressPoses=12,rays,hitPairs,maxDistanceError,maxUvError,reference="MeshCollider of current baked pose, no production colliders"};
        File.WriteAllText(".codex-artifacts/WaterGun/phase5-deformation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        Complete=true;Cleanup();
    }
    private void Cleanup(){Physics.queriesHitBackfaces=oldBackfaces;bindings?.Dispose();bindings=null;if(target!=null)Destroy(target);target=null;}
    private void OnDestroy(){Cleanup();}
}
#endif
