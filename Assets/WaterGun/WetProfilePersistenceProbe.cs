using System;
using Newtonsoft.Json;
using Warudo.Core.Data;
using Warudo.Core.Serializations;

namespace SurfaceWetness.WaterGun
{
    // Runs in Warudo: the Unity editor has no initialized Warudo Context.
    // Temporary structured entities only; never changes the user's Profiles or scene assets.
    internal static class WetProfilePersistenceProbe
    {
        public static string Run(WaterGunAsset parent)
        {
            WetSurfaceProfile source=null,copy=null;
            try
            {
                source=StructuredData.Create<WetSurfaceProfile,WaterGunAsset>(parent,p=>
                {
                    p.Key="Renamed%2FMesh[1]#slot=4";p.Enabled=false;p.BlockOnly=true;p.Order=7;
                    p.Transmit=false;p.StopAfterWet=true;p.TransferMultiplier=.37f;
                    p.Darkening=.24f;p.Smoothness=.63f;p.Opacity=true;p.WetOpacity=.31f;
                    p.Drying=false;p.DrySeconds=42;
                });
                var serialized=source.Serialize();int count=serialized.dataInputs.Count;
                string json=JsonConvert.SerializeObject(serialized);
                copy=StructuredData.Create<WetSurfaceProfile,WaterGunAsset>(parent);
                // A checkpoint restores the same identity; release source before Deserialize.Store.
                source.Destroy();source=null;
                copy.Deserialize(JsonConvert.DeserializeObject<SerializedStructuredData>(json));
                if(copy.Key!="Renamed%2FMesh[1]#slot=4" || copy.Enabled || !copy.BlockOnly || copy.Order!=7 ||
                    copy.Transmit || !copy.StopAfterWet || copy.TransferMultiplier!=.37f || copy.Darkening!=.24f ||
                    copy.Smoothness!=.63f || !copy.Opacity || copy.WetOpacity!=.31f || copy.Drying || copy.DrySeconds!=42)
                    throw new InvalidOperationException("Warudo profile roundtrip mismatch");
                return "SDK 프로필 JSON 왕복 확인: "+count+" 입력 · RT 저장 없음";
            }
            finally{source?.Destroy();copy?.Destroy();}
        }
    }
}
