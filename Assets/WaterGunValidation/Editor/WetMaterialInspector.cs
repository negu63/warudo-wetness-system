#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SurfaceWetness.WaterGun;

// Editor-only UI. Warudo uses the same material mode helper from its per-surface profile.
public sealed class WetMaterialInspector : ShaderGUI
{
    public override void OnGUI(MaterialEditor editor,MaterialProperty[] properties)
    {
        base.OnGUI(editor,properties);
        foreach(var target in editor.targets)
        {
            var material=target as Material;
            if(material!=null)WetShaderAdapter.SetOpacityMode(material,material.GetFloat("_WetOpacityEnabled")>.5f);
        }
    }
}
#endif
