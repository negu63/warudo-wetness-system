#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using SurfaceWetness.WaterGun;

[DefaultExecutionOrder(500)]
public sealed class WaterGunProfileRunner : MonoBehaviour
{
    public string PrefabPath="Assets/WetnessDemoCharacter/Character.prefab",RunName="water-gun-demo";
    public int SampleFrames=180,WarmupFrames=30;
    public bool Complete { get; private set; }
    public string Failure { get; private set; }
    private GameObject target,host;
    private WetBoneLoop loop;
    private WaterGunDriver driver;
    private WetSurfaceBindings bindings;
    private Camera camera;
    private readonly string[] modes={"holstered-base","aim-base","fire-base","holstered-stress","aim-stress","fire-stress"};
    private readonly string[] names={"Main Thread","Render Thread","GC Allocated In Frame","WaterGun.Tick","WaterGun.BakeMesh","WaterGun.BVHRefit","WaterGun.Trace","WaterGun.Brush","WaterGun.Deposit","WaterGun.Dry"};
    private readonly Dictionary<string,ProfilerRecorder> recorders=new Dictionary<string,ProfilerRecorder>();
    private sealed class Row
    {
        public string mode;public int frame,pathCount,deposits,geometryRefresh,gpuFrame=-1;
        public bool firing;public float elapsedMs;public long gpuNs;public long[] metrics;
    }
    private Row[] rows;private int written,frame,mode;
    private bool oldProfiler,oldBinary,oldGpu,restored;private string oldLog,output;
    private int oldVsync,oldTarget,previousBakes,previousDeposits;
    private readonly List<int> previousCounts=new List<int>();
    private WetGeometry[] geometries;
    public void Begin()
    {
        if(!Application.isPlaying || driver!=null)throw new InvalidOperationException("New runner in Play mode required");
        oldVsync=QualitySettings.vSyncCount;oldTarget=Application.targetFrameRate;
        oldProfiler=Profiler.enabled;oldBinary=Profiler.enableBinaryLog;oldLog=Profiler.logFile;oldGpu=Profiler.GetAreaEnabled(ProfilerArea.GPU);
        target=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        loop=target.AddComponent<WetBoneLoop>();loop.Animate=false;loop.ApplyPose(0);
        var catalog=WetCharacterCatalog.Scan(target);
        var profiles=catalog.Entries.Select(e=>new WetSurfaceProfile
        {
            Key=e.Key,Order=e.Renderer.name=="OuterCloth" || e.Renderer.name=="Jacket" ? 0 : e.Renderer.name=="InnerCloth" || e.Renderer.name=="Shirt" ? 1 : 2,
            StopAfterWet=e.Renderer.name=="Skin" || e.Renderer.name=="RigSkin",Opacity=e.Renderer.name=="OuterCloth" || e.Renderer.name=="Jacket",DrySeconds=20
        }).ToArray();
        bindings=new WetSurfaceBindings(catalog,profiles,AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat"));
        geometries=bindings.Surfaces.Select(s=>s.Geometry).Distinct().ToArray();
        var outer=bindings.Surfaces.First(s=>s.Profile.Order==0);var bounds=outer.Entry.Renderer.bounds;
        Ray ray=default(Ray);bool found=false;
        for(int side=0;side<2 && !found;side++)for(int y=3;y<8 && !found;y++)for(int x=3;x<8 && !found;x++)
        {
            var aim=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x/10f),Mathf.Lerp(bounds.min.y,bounds.max.y,y/10f),bounds.center.z);
            ray=new Ray(aim+Vector3.forward*(side==0 ? -3:3),side==0 ? Vector3.forward:Vector3.back);
            found=bindings.TracePath(ray,8,.15f,true)==3;
        }
        if(!found)throw new InvalidOperationException("Three-layer camera ray required");
        var cameraObject=new GameObject("ProfileCamera");cameraObject.transform.SetParent(transform,false);
        camera=cameraObject.AddComponent<Camera>();camera.transform.SetPositionAndRotation(ray.origin,Quaternion.LookRotation(ray.direction));
        var lightObject=new GameObject("ProfileLight");lightObject.transform.SetParent(transform,false);
        var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.shadows=LightShadows.None;
        lightObject.transform.rotation=Quaternion.Euler(40,30,0);
        host=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WaterGun/Prefabs/WaterGunRuntime.prefab"));
        driver=host.AddComponent<WaterGunDriver>();driver.Bindings=bindings;driver.DisplayCamera=camera;driver.ShowHud=false;
        driver.Initialize(AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/WaterStream.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/WaterSplash.mat"));
        // Register custom markers before discovering counters; outside measured frames.
        driver.Drawn=true;driver.Tick(1f/60,new Vector2(Screen.width*.5f,Screen.height*.5f),false,true);
        driver.Tick(1f/60,new Vector2(Screen.width*.5f,Screen.height*.5f),true,true);
        foreach(var s in bindings.Surfaces)s.Binding.State.Clear();
        rows=new Row[SampleFrames*modes.Length];for(int i=0;i<rows.Length;i++)rows[i]=new Row{metrics=new long[names.Length]};
        QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
        output=System.IO.Path.Combine(".codex-artifacts/WaterGun/profiles",RunName);Directory.CreateDirectory(output);
        Profiler.enabled=false;Profiler.logFile=System.IO.Path.Combine(output,"frames.raw");Profiler.enableBinaryLog=true;
        Profiler.SetAreaEnabled(ProfilerArea.GPU,true);Profiler.enabled=true;
        var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
        foreach(string name in names)foreach(var handle in handles)
        {
            var description=ProfilerRecorderHandle.GetDescription(handle);if(description.Name!=name)continue;
            recorders[name]=ProfilerRecorder.StartNew(description.Category,name,1,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame);break;
        }
        driver.Drawn=false;driver.Tick(1f/60,new Vector2(Screen.width*.5f,Screen.height*.5f),false,true);
    }
    private void LateUpdate()
    {
        if(driver==null || Complete || Failure!=null)return;
        try
        {
            if(frame>WarmupFrames)
            {
                var row=rows[written++];row.mode=modes[mode];row.frame=Time.frameCount;
                row.pathCount=bindings.PathCount;row.deposits=previousDeposits;row.geometryRefresh=previousBakes;
                row.firing=driver.Gun.Firing;row.elapsedMs=Time.unscaledDeltaTime*1000;
                for(int i=0;i<names.Length;i++)if(recorders.TryGetValue(names[i],out var recorder))row.metrics[i]=recorder.LastValue;
                int last=UnityEditorInternal.ProfilerDriver.lastFrameIndex;
                for(int f=last;f>=Mathf.Max(UnityEditorInternal.ProfilerDriver.firstFrameIndex,last-12);f--)
                using(var data=UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f,0))
                {if(data.valid && data.frameGpuTimeNs>0){row.gpuNs=(long)data.frameGpuTimeNs;row.gpuFrame=f;break;}}
            }
            if(++frame>WarmupFrames+SampleFrames)
            {
                frame=0;if(++mode>=modes.Length){Finish();return;}
                foreach(var s in bindings.Surfaces)s.Binding.State.Clear();
            }
            loop.StressTest=mode>=3;loop.StressIntensity=1;
            loop.ApplyPose(mode>=3 ? Mathf.Repeat(frame*4/72f,1) : .5f-.5f*Mathf.Cos(frame*2*Mathf.PI/240));
            driver.Drawn=mode%3!=0;int stamps=driver.Deposits;
            previousCounts.Clear();foreach(var g in geometries)previousCounts.Add(g.RefreshCount);
            driver.Tick(1f/60,new Vector2(Screen.width*.5f,Screen.height*.5f),mode%3==2 && frame>1,true);
            previousDeposits=driver.Deposits-stamps;previousBakes=0;
            for(int i=0;i<geometries.Length;i++)previousBakes+=geometries[i].RefreshCount-previousCounts[i];
        }
        catch(Exception e){Failure=e.ToString();Debug.LogException(e);Restore();}
    }
    private void Finish()
    {
        var availability=recorders.ToDictionary(p=>p.Key,p=>p.Value.Valid);
        var report=new{RunName,PrefabPath,unity=Application.unityVersion,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,
            graphicsJobs=PlayerSettings.graphicsJobs,screen=new{Screen.width,Screen.height},WarmupFrames,SampleFrames,
            fixedSimulationDelta=1f/60,stressClock="frame*4/72; not wall clock",driverOnly=true,warudoAssetScanIncluded=false,
            uniqueGpuFrames=rows.Where(r=>r.gpuNs>0).Select(r=>r.gpuFrame).Distinct().Count(),
            availability,metricNames=names,units="ns; GC counter bytes; elapsedMs milliseconds",rows};
        File.WriteAllText(System.IO.Path.Combine(output,"samples.json"),Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        Complete=true;Restore();
    }
    private void Restore()
    {
        if(restored)return;restored=true;
        foreach(var r in recorders.Values)r.Dispose();recorders.Clear();
        Profiler.enabled=false;Profiler.enableBinaryLog=oldBinary;Profiler.logFile=oldLog;Profiler.SetAreaEnabled(ProfilerArea.GPU,oldGpu);Profiler.enabled=oldProfiler;
        QualitySettings.vSyncCount=oldVsync;Application.targetFrameRate=oldTarget;
        if(driver!=null){driver.Bindings=null;driver.Gun?.Stop();}
        bindings?.Dispose();bindings=null;
        if(host!=null)Destroy(host);if(target!=null)Destroy(target);
    }
    private void OnDestroy(){Restore();}
}
#endif
