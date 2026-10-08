namespace SurfaceWetness.WaterGun {
using UnityEngine;

// Presentation only; WaterGunDriver owns screen-space aim and surface tracing.
public sealed class WetWaterGun : System.IDisposable
{
    private readonly Transform rig, pivot, muzzle;
    private readonly LineRenderer stream;
    private readonly ParticleSystem splash;
    private readonly Transform[] transforms;
    public Vector3 MuzzlePosition => muzzle.position;
    public Vector3 EndPoint { get; private set; }
    public bool Firing => stream.enabled;

    public WetWaterGun(Transform rig, Material streamMaterial, Material splashMaterial)
    {
        this.rig=rig;
        pivot=rig.Find("Pivot"); muzzle=pivot.Find("Muzzle");
        stream=rig.gameObject.AddComponent<LineRenderer>();
        stream.sharedMaterial=streamMaterial; stream.useWorldSpace=true;
        stream.positionCount=2; stream.numCapVertices=4;
        stream.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        stream.receiveShadows=false; stream.textureMode=LineTextureMode.Tile;
        stream.enabled=false;
        var go=new GameObject("WaterSplash"); go.transform.SetParent(rig,false);
        splash=go.AddComponent<ParticleSystem>(); splash.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=splash.main; main.playOnAwake=false; main.loop=true;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(.08f,.18f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(.2f,.6f);
        main.startSize=new ParticleSystem.MinMaxCurve(.008f,.02f);
        main.startColor=new Color(.7f,.92f,1,.7f); main.gravityModifier=.5f; main.maxParticles=100;
        var emission=splash.emission; emission.rateOverTime=100;
        var shape=splash.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.angle=65; shape.radius=.012f;
        var size=splash.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
        var renderer=go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=splashMaterial;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        transforms=rig.GetComponentsInChildren<Transform>(true);
    }

    public void SetVisible(bool visible) { rig.gameObject.SetActive(visible); if(!visible)Stop(); }
    public void Aim(Camera camera, Vector3 target, Vector3 cameraOffset, Vector3 muzzleOffset)
    {
        // Move and scale together about the camera to preserve the on-screen gun size,
        // while keeping the muzzle on the camera side of a nearby aim point.
        float depth=camera.transform.InverseTransformPoint(target).z;
        float scale=Mathf.Clamp((depth-.015f)/(cameraOffset.z+muzzleOffset.magnitude),.001f,1f);
        rig.position=camera.transform.TransformPoint(cameraOffset*scale);
        rig.rotation=camera.transform.rotation;
        var parentScale=rig.parent.lossyScale;
        rig.localScale=new Vector3(scale/parentScale.x,scale/parentScale.y,scale/parentScale.z);
        // The model converges on a distant point along screen aim. Actual near hits
        // remain independent of this visual target, so the gun cannot turn backwards.
        var cameraDirection=target-camera.transform.position;
        var poseTarget=camera.transform.position+cameraDirection.normalized*Mathf.Max(2f,cameraDirection.magnitude);
        var direction=poseTarget-pivot.position;
        if(direction.sqrMagnitude>.000001f)pivot.rotation=Quaternion.LookRotation(direction,camera.transform.up);
        muzzle.localPosition=muzzleOffset;
        // One refinement accounts for the barrel offset from the pivot.
        direction=poseTarget-muzzle.position;
        if(direction.sqrMagnitude>.000001f)pivot.rotation=Quaternion.LookRotation(direction,camera.transform.up);
        SetLayer(camera);
    }
    private void SetLayer(Camera camera)
    {
        int layer=0; for(int i=0;i<32;i++)if((camera.cullingMask&(1<<i))!=0){layer=i;break;}
        foreach(var t in transforms)t.gameObject.layer=layer;
    }
    public void Draw(Vector3 end, bool firing, bool hit, Vector3 normal, float width)
    {
        EndPoint=end; stream.enabled=firing;
        stream.startWidth=Mathf.Max(.001f,width); stream.endWidth=stream.startWidth*.65f;
        stream.SetPosition(0,muzzle.position); stream.SetPosition(1,end);
        if(firing&&hit)
        {
            splash.transform.SetPositionAndRotation(end+normal*.002f,Quaternion.LookRotation(normal));
            if(!splash.isPlaying)splash.Play();
        }
        else if(splash.isPlaying)splash.Stop(true,ParticleSystemStopBehavior.StopEmitting);
    }
    public void Stop() { stream.enabled=false; splash.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); }
    public void Dispose() { Stop(); WetSurfaceState.Release(stream); WetSurfaceState.Release(splash.gameObject); }
}

}
