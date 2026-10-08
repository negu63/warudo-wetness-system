using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DemoAutomation
{
    public static void CreateVerifyAndBuild()
    {
        DemoSceneBuilder.Create();
        VerifyScene();
        DemoSceneBuilder.Build();
    }

    [MenuItem("Portfolio/Verify Interactive Demo Scene")]
    public static void VerifyScene()
    {
        Directory.CreateDirectory("Artifacts");
        const string path = "Assets/Demo/WetnessDemo.unity";
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
        {
            File.WriteAllText("Artifacts/demo-scene-check.txt", "FAILED: missing interactive demo scene");
            throw new Exception("Missing interactive demo scene");
        }

        // Interactive verification reads the loaded scene without replacing unsaved user work.
        var scene = Application.isBatchMode
            ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
            : SceneManager.GetSceneByPath(path);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("먼저 Portfolio > Open Interactive Demo Scene으로 데모 씬을 여세요.");

        bool connected = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    throw new Exception("Missing script: " + child.name);
            foreach (var controller in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(controller is WetnessDemo demo)) continue;
                connected = true;
                if (!demo.Character || !demo.Gun || !demo.View || !demo.MaskUpdate || !demo.Coverage || !demo.Stream || !demo.Splash) throw new Exception("Unconnected demo scene reference");
            }
        }
        if (!connected) throw new Exception("Demo controller missing");
        File.WriteAllText("Artifacts/demo-scene-check.txt", "PASSED");
        Debug.Log("Interactive demo scene references: PASSED");
    }
}
