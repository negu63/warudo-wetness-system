using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class PortfolioExportSetupChecks
{
    private static int frames;
    public static void Run() { frames=0;EditorApplication.update+=Tick; }
    private static void Tick()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || ++frames<3)return;
        EditorApplication.update-=Tick;
        try
        {
            var settings=AssetDatabase.LoadAssetAtPath<UMod.ModTools.Export.ExportSettings>("Assets/ExportSettings.asset");
            var so=new SerializedObject(settings);var profiles=so.FindProperty("exportProfiles");
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            for(int i=0;i<profiles.arraySize;i++)
            {
                var p=profiles.GetArrayElementAtIndex(i);string name=p.FindPropertyRelative("modName").stringValue;
                AssertPath(p.FindPropertyRelative("modAssetPath").stringValue,Path.Combine(root,"Assets",name));
                AssertPath(p.FindPropertyRelative("modExportPath").stringValue,Path.Combine(root,"Exports",name));
            }
            int active=so.FindProperty("activeProfile").intValue;
            string before=EditorJsonUtility.ToJson(settings);
            var type=Type.GetType("PortfolioExportSetup, Assembly-CSharp-Editor");
            if(type==null)throw new Exception("Automatic export setup missing");
            type.GetMethod("ConfigurePaths",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
            type.GetMethod("ConfigurePaths",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
            if(before!=EditorJsonUtility.ToJson(settings))throw new Exception("Repeated setup changes metadata or active profile");
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/automatic-export-check.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {passed=true,profiles=profiles.arraySize,currentProjectPaths=true,outputDirectoriesExist=true,idempotent=true,activeProfilePreserved=active,warudoInstallationRequired=false},Newtonsoft.Json.Formatting.Indented));
            Debug.Log("[Portfolio] AUTOMATIC EXPORT CHECK PASSED");EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/automatic-export-failure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static void AssertPath(string actual,string expected)
    {
        if(!string.Equals(actual,expected,StringComparison.OrdinalIgnoreCase) || !Directory.Exists(actual))
            throw new Exception("Export directory not initialized for current project: "+actual+" expected "+expected);
    }
}
