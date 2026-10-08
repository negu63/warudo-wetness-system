using UnityEngine;

namespace SurfaceWetness.WaterGun
{
    public static class WetShaderAdapter
    {
        public const string Supported="젖음 계약 v1 · 연결 후보";
        private static readonly string[] ContractPrefixes={"Wetness","LifeIsWet","SurfaceWetness"};
        private static readonly string[] RequiredInputs={"_WetMask","_UseWetMask","_Wetness","_WetDarkening","_WetSmoothness"};
        public static string Check(Material material)
        {
            if(material==null || material.shader==null)return "재질/셰이더 없음";
            if(!material.shader.isSupported)return "현재 GPU에서 셰이더 미지원";
            // Neutral tags are authoritative; legacy tags keep existing character packages usable.
            string prefix=null;
            foreach(string candidate in ContractPrefixes)
                if(material.GetTag(candidate+"Contract",false,"")!=""){prefix=candidate;break;}
            if(prefix==null)return "젖음 지원 계약 없음 · 젖음 기능을 구현한 셰이더 필요";
            if(material.GetTag(prefix+"Contract",false,"")!="1")return "젖음 계약 버전 불일치";
            if(material.GetTag(prefix+"Features",false,"")!="MaskColorPbrOpacity" || material.GetTag(prefix+"RenderMode",false,"")!="MaterialToggle")return "젖음 기능 계약 불일치";
            foreach(string property in RequiredInputs)
                if(!material.HasProperty(property))return "젖음 입력 누락: "+property;
            if(!material.HasProperty("_WetOpacity") || !material.HasProperty("_WetOpacityEnabled"))return "비침 계약 입력 누락";
            if(!material.HasProperty("_WetSplitSupported") || material.GetFloat("_WetSplitSupported")<.5f)return "불투명·비침 분리 렌더링 지원 필요";
            return Supported;
        }
        public static void SetOpacityMode(Material material,bool enabled,int layerOrder=0)
        {
            if(Check(material)!=Supported)return;
            bool current=material.IsKeywordEnabled("_WET_TRANSPARENT");
            // Standalone preview. Runtime bindings split opaque and translucent renderers.
            int queue=enabled ? 3000-Mathf.Clamp(layerOrder,0,499) : 2000;
            if(current==enabled && material.renderQueue==queue)return;
            if(enabled)material.EnableKeyword("_WET_TRANSPARENT");else material.DisableKeyword("_WET_TRANSPARENT");
            material.renderQueue=queue;
            material.SetOverrideTag("RenderType",enabled ? "Transparent" : "Opaque");
        }
    }
}
