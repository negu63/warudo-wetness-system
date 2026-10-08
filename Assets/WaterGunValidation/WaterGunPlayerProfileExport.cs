#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditorInternal;

public static class WaterGunPlayerProfileExport
{
    public static string Build(bool graphicsJobs, string executablePath)
    {
        const string path = "Assets/WaterGunValidation/WaterGunPlayerProfile.unity";
        var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (original.isDirty) throw new InvalidOperationException("Save the original scene before building");
        bool oldJobs = UnityEditor.PlayerSettings.graphicsJobs;
        string connectPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../ProjectSettings/UnityConnectSettings.asset"));
        string oldConnect = File.ReadAllText(connectPath);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
        var runner = scene.GetRootGameObjects()[0].GetComponent<WaterGunPlayerProfileRunner>();
        bool oldLabel = runner.GraphicsJobsBuild;
        try
        {
            runner.GraphicsJobsBuild = graphicsJobs;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            UnityEditor.PlayerSettings.graphicsJobs = graphicsJobs;
            var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions {
                scenes = new[] { path }, locationPathName = executablePath,
                target = UnityEditor.BuildTarget.StandaloneWindows64, options = UnityEditor.BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Benchmark build failed: " + report.summary.result);
            return report.summary.result.ToString();
        }
        finally
        {
            runner.GraphicsJobsBuild = oldLabel;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
            UnityEditor.PlayerSettings.graphicsJobs = oldJobs;
            UnityEditor.AssetDatabase.SaveAssets();
            // Building enables Core Stats in this SDK project. Preserve the caller's file.
            File.WriteAllText(connectPath, oldConnect);
        }
    }

    public static int Export(string directory)
    {
        var frames = new List<object>();
        for (int scenario = 0; scenario < 12; scenario++)
        {
            ProfilerDriver.ClearAllFrames();
            if (!ProfilerDriver.LoadProfile(Path.Combine(directory, "scenario-" + scenario + ".raw"), false))
                throw new InvalidOperationException("Cannot load scenario " + scenario);
            for (int i = ProfilerDriver.firstFrameIndex; i <= ProfilerDriver.lastFrameIndex; i++)
            using (var data = ProfilerDriver.GetRawFrameDataView(i, 0))
            {
                if (!data.valid || data.GetFrameMetaDataCount(WaterGunPlayerProfileRunner.MetadataId, 1) == 0) continue;
                var metadata = data.GetFrameMetaData<int>(WaterGunPlayerProfileRunner.MetadataId, 1);
                if (metadata[0] != scenario) throw new InvalidOperationException("Scenario metadata mismatch");
                frames.Add(new { scenario, frame = metadata[1], sample = metadata[2] == 1,
                    gpuNs = data.frameGpuTimeNs, cpuMs = data.frameTimeMs });
            }
        }
        File.WriteAllText(Path.Combine(directory, "gpu.json"), Newtonsoft.Json.JsonConvert.SerializeObject(frames));
        return frames.Count;
    }
}
#endif
