using Warudo.Core.Attributes;
using Warudo.Core.Plugins;

namespace SurfaceWetness.WaterGun
{
    [PluginType(Id="surfacewetness.water-gun", Name="WaterGun", Version=WaterGunPlugin.CurrentVersion,
        Author="wonwoo", Description="Reusable water gun: independent character per-slot wetness.",
        AssetTypes=new[]{typeof(WaterGunAsset)})]
    public sealed class WaterGunPlugin : Plugin { public const string CurrentVersion="0.6.7"; }
}
