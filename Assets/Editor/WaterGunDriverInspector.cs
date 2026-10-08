using UnityEditor;
using SurfaceWetness.WaterGun;

[CustomEditor(typeof(WaterGunDriver))]
public sealed class WaterGunDriverInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var driver=(WaterGunDriver)target;
        if(driver.Bindings != null)
        {
            DrawPropertiesExcluding(serializedObject,"Surface","Darkening","Smoothness","Opacity","WetOpacity");
            EditorGUILayout.HelpBox("색·광택·불투명도는 각 Renderer의 런타임 머티리얼에서 조절합니다. Rate는 초당 젖음 공급량이며 물줄기 굵기·입자 수와는 별개입니다.",MessageType.Info);
        }
        else DrawPropertiesExcluding(serializedObject);
        serializedObject.ApplyModifiedProperties();
    }
}
