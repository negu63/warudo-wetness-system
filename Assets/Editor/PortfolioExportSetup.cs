using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PortfolioExportSetup
{
    static PortfolioExportSetup() { EditorApplication.update += Initialize; }

    private static void Initialize()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= Initialize;
        try { ConfigurePaths(); }
        catch (Exception e) { Debug.LogError("[Portfolio] Export setup failed: " + e.Message); }
    }

    public static void ConfigurePaths()
    {
        var settings = AssetDatabase.LoadAssetAtPath<UMod.ModTools.Export.ExportSettings>("Assets/ExportSettings.asset");
        if (settings == null) throw new InvalidOperationException("Export settings unavailable. Wait for package import, then use Portfolio/Configure Mod Exports.");
        var so = new SerializedObject(settings);
        var profiles = so.FindProperty("exportProfiles");
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        bool changed = false;
        for (int i = 0; i < profiles.arraySize; i++)
        {
            var profile = profiles.GetArrayElementAtIndex(i);
            string name = profile.FindPropertyRelative("modName").stringValue;
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidOperationException("Mod name must be a valid folder name: " + name);
            string assets = Path.Combine(root, "Assets", name);
            if (!Directory.Exists(assets)) throw new DirectoryNotFoundException("Mod assets missing: " + assets);
            string output = Path.Combine(root, "Exports", name);
            Directory.CreateDirectory(output);
            changed |= SetPath(profile.FindPropertyRelative("modAssetPath"), assets);
            changed |= SetPath(profile.FindPropertyRelative("modExportPath"), output);
        }
        if (!changed) return;
        so.ApplyModifiedPropertiesWithoutUndo();
        // Save only the export configuration; do not save unrelated user assets.
        AssetDatabase.SaveAssetIfDirty(settings);
        Debug.Log("[Portfolio] Export paths configured for the current project");
    }

    private static bool SetPath(SerializedProperty property, string path)
    {
        if (property.stringValue == path) return false;
        property.stringValue = path;
        return true;
    }
}
