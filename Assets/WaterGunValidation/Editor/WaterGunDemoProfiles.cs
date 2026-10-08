#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using SurfaceWetness.WaterGun;

public static class WaterGunDemoProfiles
{
    public const string Path="Assets/WetnessDemoCharacter/Character.prefab";
    public static WetSurfaceProfile[] Profiles(GameObject root)
    {
        // Demo setup only; production plugin never depends on these mesh names.
        return WetCharacterCatalog.Scan(root).Entries.Select(e=>new WetSurfaceProfile
        {
            Key=e.Key,Order=e.Renderer.name=="OuterCloth" ? 0 : e.Renderer.name=="InnerCloth" ? 1 : 2,
            StopAfterWet=e.Renderer.name=="Skin",
            Opacity=e.Renderer.name=="OuterCloth",DrySeconds=20
        }).ToArray();
    }
}
#endif
