using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

public static class PortfolioProject
{
    [MenuItem("Portfolio/Configure Mod Exports")]
    public static void ConfigureExports()
    {
        PortfolioExportSetup.ConfigurePaths();
    }

    [MenuItem("Portfolio/Run Validation")]
    public static void Validate()
    {
        try
        {
            Directory.CreateDirectory("Artifacts/WaterGun");
            WaterGunSurfaceChoiceChecks.Run();
            var results = new {
                phase2 = WaterGunChecks.Phase2(), phase3 = WaterGunChecks.Phase3(),
                phase4 = WaterGunChecks.Phase4(), opacity = WaterGunOpacityChecks.Run(),
                splitRender = WaterGunSplitRenderChecks.Run(), lifecycle = WaterGunLifecycleChecks.Run(),
                contractNames = WaterGunContractChecks.Names(), recovery = WaterGunContractChecks.Recovery(),
                aim = WaterGunAimChecks.Run(), rotatedAim = WaterGunAimChecks.Run(true),
                legacyContract = WaterGunContractChecks.SupplyAndRender("LifeIsWet"),
                publicContract = WaterGunContractChecks.SupplyAndRender("SurfaceWetness"),
                neutralContract = WaterGunContractChecks.SupplyAndRender("Wetness"),
                characterDry = WaterGunCharacterDryChecks.Run()
            };
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/validation.json", JsonConvert.SerializeObject(results, Formatting.Indented));
            Debug.Log("[Portfolio] VALIDATION PASSED");
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/validation.json", JsonConvert.SerializeObject(new { failed = true, error = e.ToString() }, Formatting.Indented));
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    [MenuItem("Portfolio/Build Benchmark Player")]
    public static void BuildPlayer()
    {
        Directory.CreateDirectory("Artifacts/BenchmarkPlayer");
        Debug.Log("[Portfolio] PLAYER " + WaterGunPlayerProfileExport.Build(false, Path.GetFullPath("Artifacts/BenchmarkPlayer/WetnessBenchmark.exe")));
    }

    [MenuItem("Portfolio/Build All Mods")]
    public static void BuildAll()
    {
        ConfigureExports();
        var initial = AssetDatabase.LoadAssetAtPath<UMod.ModTools.Export.ExportSettings>("Assets/ExportSettings.asset");
        var initialState = new SerializedObject(initial);
        int selected = initialState.FindProperty("activeProfile").intValue;
        int count = initialState.FindProperty("exportProfiles").arraySize;
        try
        {
            for (int i = 0; i < count; i++)
            {
                // The SDK releases temporary assemblies and settings after each build.
                var settings = AssetDatabase.LoadAssetAtPath<UMod.ModTools.Export.ExportSettings>("Assets/ExportSettings.asset");
                if (settings == null) throw new InvalidOperationException("Export settings unavailable after build");
                var so = new SerializedObject(settings);
                so.FindProperty("activeProfile").intValue = i;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(settings);
                var result = UMod.BuildEngine.ModToolsUtil.StartBuild(settings);
                if (result == null || !result.Successful) throw new InvalidOperationException("Mod build failed for profile " + i);
                Debug.Log("[Portfolio] MOD BUILD PASSED " + i);
            }
        }
        finally
        {
            var settings = AssetDatabase.LoadAssetAtPath<UMod.ModTools.Export.ExportSettings>("Assets/ExportSettings.asset");
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("activeProfile").intValue = selected;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(settings);
            }
        }
    }
}
