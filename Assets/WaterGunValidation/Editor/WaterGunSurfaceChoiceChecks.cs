using System;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using SurfaceWetness.WaterGun;
using UnityEditor;
using UnityEngine;
using Warudo.Core.Attributes;
using Warudo.Core.Data;

public static class WaterGunSurfaceChoiceChecks
{
    public static void Run()
    {
        var attribute = typeof(WetSurfaceProfile).GetField(nameof(WetSurfaceProfile.Key)).GetCustomAttribute<AutoCompleteAttribute>();
        if (!attribute.ForceSelection) throw new Exception("Surface selector uses free text instead of a dropdown");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WetnessDemoCharacter/Character.prefab");
        var root = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var snapshot = WetCharacterCatalog.Scan(root);
            if (snapshot.Entries.Count != 3) throw new Exception("Expected three demo slots");
            var asset = new WaterGunAsset();
            typeof(WaterGunAsset).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, snapshot);
            var profile = new WetSurfaceProfile { Parent = asset, Key = snapshot.Entries[0].Key };
            var provider = typeof(WetSurfaceProfile).GetMethod(attribute.MethodName, BindingFlags.Instance | BindingFlags.NonPublic);
            var task = (UniTask<AutoCompleteList>)provider.Invoke(profile, null);
            var entries = task.GetAwaiter().GetResult().categories.SelectMany(c => c.entries).ToArray();
            if (entries.Length != 3 || entries.Any(e => string.IsNullOrEmpty(e.label))) throw new Exception("Missing surface choices");
            if (!entries.Select(e => e.value).SequenceEqual(snapshot.Entries.Select(e => e.Key))) throw new Exception("Surface keys changed");
            if (!profile.GetHeader().Contains(snapshot.Entries[0].DisplayName)) throw new Exception("Selected surface label missing");
            System.IO.Directory.CreateDirectory("Artifacts");
            System.IO.File.WriteAllText("Artifacts/surface-choice-validation.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = true, forceSelection = true, choices = entries.Length, keysPreserved = true, selectedLabelResolved = true }, Newtonsoft.Json.Formatting.Indented));
            Debug.Log("[Portfolio] SURFACE CHOICE CHECK PASSED");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
