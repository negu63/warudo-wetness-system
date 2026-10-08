using UnityEngine;
using System.Collections.Generic;

// Deterministic bone tests; stress motion deliberately permits non-human poses and overlap.
[DefaultExecutionOrder(-200)]
public class WetBoneLoop : MonoBehaviour
{
    public bool Animate = true;
    public float Period = 4;
    public float ArmAngle = 60, ElbowAngle = 35;
    public bool StressTest;
    public float StressSpeed = 1, StressIntensity = 1;
    [Range(0,1)] public float Pose;
    private Transform arm, forearm;
    private Quaternion armRest, forearmRest;
    private float elapsed;
    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<string, Quaternion> rest = new Dictionary<string, Quaternion>();
    private static readonly string[] TestBones = {
        "arm_stretch.l", "forearm_stretch.l", "arm_stretch.r", "forearm_stretch.r",
        "spine_01.x", "spine_02.x", "spine_03.x", "neck.x",
        "thigh_stretch.l", "leg_stretch.l", "thigh_stretch.r", "leg_stretch.r"
    };
    private void Awake()
    {
        var animator=GetComponentInChildren<Animator>();
        if(animator!=null)animator.enabled=false;
        foreach(var bone in GetComponentsInChildren<Transform>())
        {
            if(bone.name=="arm_stretch.l")arm=bone;
            if(bone.name=="forearm_stretch.l")forearm=bone;
            foreach(var name in TestBones)
                if(bone.name==name) { bones[name]=bone; rest[name]=bone.localRotation; break; }
        }
        if(arm==null || forearm==null) { Debug.LogError("[WetBoneLoop] Test bones missing"); enabled=false; return; }
        armRest=arm.localRotation; forearmRest=forearm.localRotation;
    }
    private void LateUpdate()
    {
        if(Animate)
        {
            elapsed+=Time.deltaTime*(StressTest ? Mathf.Clamp(StressSpeed,.25f,4) : 1);
            Pose=StressTest ? Mathf.Repeat(elapsed/1.2f,1) : .5f-.5f*Mathf.Cos(elapsed*2*Mathf.PI/Mathf.Max(.5f,Period));
        }
        ApplyPose(Pose);
    }
    public void ApplyPose(float pose)
    {
        Pose=Mathf.Clamp01(pose);
        if(arm==null || forearm==null)return;
        RestoreBones();
        if(StressTest) { ApplyStress(Pose); return; }
        arm.localRotation=armRest*Quaternion.Euler(0,0,ArmAngle*Pose);
        forearm.localRotation=forearmRest*Quaternion.Euler(0,0,ElbowAngle*Pose);
    }
    private void Rotate(string name, Vector3 degrees)
    {
        Transform bone;
        if(bones.TryGetValue(name,out bone) && bone!=null)
            bone.localRotation=rest[name]*Quaternion.Euler(degrees*Mathf.Clamp01(StressIntensity));
    }
    private void ApplyStress(float phase)
    {
        float t=phase*2*Mathf.PI;
        float a=Mathf.Sin(t), b=Mathf.Sin(2*t), c=Mathf.Sin(3*t);
        float fold=.5f-.5f*Mathf.Cos(2*t);
        Rotate("arm_stretch.l",new Vector3(85*b,105*c,125*a));
        Rotate("arm_stretch.r",new Vector3(-85*c,-105*a,-125*b));
        Rotate("forearm_stretch.l",new Vector3(55*c,45*b,145*fold));
        Rotate("forearm_stretch.r",new Vector3(-55*b,-45*c,-145*fold));
        Rotate("spine_01.x",new Vector3(25*b,35*a,20*c));
        Rotate("spine_02.x",new Vector3(25*c,40*b,25*a));
        Rotate("spine_03.x",new Vector3(-20*a,30*c,-20*b));
        Rotate("neck.x",new Vector3(25*b,35*c,20*a));
        Rotate("thigh_stretch.l",new Vector3(80*a,35*c,45*b));
        Rotate("thigh_stretch.r",new Vector3(-80*b,-35*a,-45*c));
        Rotate("leg_stretch.l",new Vector3(125*fold,0,20*c));
        Rotate("leg_stretch.r",new Vector3(125*fold,0,-20*b));
    }
    private void RestoreBones()
    {
        foreach(var entry in bones)
            if(entry.Value!=null)entry.Value.localRotation=rest[entry.Key];
    }
    private void OnDestroy()
    {
        RestoreBones();
    }
}
