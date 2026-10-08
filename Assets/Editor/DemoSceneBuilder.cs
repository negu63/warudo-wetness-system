using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DemoSceneBuilder
{
    public const string ScenePath = "Assets/Demo/WetnessDemo.unity";
    [MenuItem("Portfolio/Create Interactive Demo Scene")]
    public static void Create()
    {
        Directory.CreateDirectory("Assets/Demo");
        Directory.CreateDirectory("Artifacts");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        try {
            var cameraObject=new GameObject("Demo Camera");var camera=cameraObject.AddComponent<Camera>();cameraObject.tag="MainCamera";
            cameraObject.AddComponent<AudioListener>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.22f);camera.nearClipPlane=.05f;camera.farClipPlane=30;
            var sun=new GameObject("Key Light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.25f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(35,-35,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.55f,.65f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Demo Floor";floor.transform.localScale=Vector3.one*.6f;
            var floorMat=DemoMaterial("Assets/Demo/Floor.mat",new Color(.22f,.27f,.32f));floor.GetComponent<Renderer>().sharedMaterial=floorMat;
            var character=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WetnessDemoCharacter/Character.prefab"));
            character.name="Demo Character";
            var gun=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WaterGun/Prefabs/WaterGunRuntime.prefab"));gun.name="Demo WaterGun";
            var controller=new GameObject("Wetness Demo").AddComponent<WetnessDemo>();controller.Character=character;controller.Gun=gun;controller.View=camera;
            controller.MaskUpdate=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/MaskUpdate.mat");controller.Coverage=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat");
            controller.Stream=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/WaterStream.mat");controller.Splash=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterGun/Materials/WaterSplash.mat");
            var bounds=character.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var r in character.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            camera.transform.position=bounds.center+new Vector3(0,.05f,-3.6f);camera.transform.LookAt(bounds.center-Vector3.right*.5f);camera.fieldOfView=38;
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.defaultScreenWidth=1200;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
            File.WriteAllText("Artifacts/demo-created.txt", "PASSED");
        }
        finally { if(!Application.isBatchMode && scene.IsValid() && scene.isLoaded)SceneManager.SetActiveScene(scene); }
        AssetDatabase.Refresh();
    }
    private static Material DemoMaterial(string path, Color color)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
        material.color=color;EditorUtility.SetDirty(material);return material;
    }
    [MenuItem("Portfolio/Open Interactive Demo Scene")]
    public static void Open() { if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath); }
    [MenuItem("Portfolio/Build Interactive Demo")]
    public static void Build()
    {
        if(!File.Exists(ScenePath))Create();
        Directory.CreateDirectory("Artifacts/InteractiveDemo");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{ScenePath},locationPathName=Path.GetFullPath("Artifacts/InteractiveDemo/WetnessDemo.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Demo build failed: "+report.summary.result);
        File.WriteAllText("Artifacts/demo-build.txt","PASSED");
    }
}
