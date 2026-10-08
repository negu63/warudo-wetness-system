using System.Linq;
using Cysharp.Threading.Tasks;
using Warudo.Core.Attributes;
using Warudo.Core.Data;

namespace SurfaceWetness.WaterGun
{
    public sealed class WetSurfaceProfile : StructuredData<WaterGunAsset>, ICollapsibleStructuredData
    {
        [DataInput,Label("적용할 메쉬 / 머티리얼"),AutoComplete(nameof(SurfaceChoices), forceSelection: true)] public string Key;
        [DataInput,Label("젖음 사용")] public bool Enabled=true;
        [DataInput,Label("판정 전용 차단 (마스크 없음)")] public bool BlockOnly;
        [DataInput,Label("옷 레이어 순서 (바깥→안쪽 증가)")] public int Order;
        [DataInput,Label("안쪽으로 전달 허용")] public bool Transmit=true;
        [DataInput,Label("이 표면을 적신 후 종료")] public bool StopAfterWet;
        [DataInput,Label("다음 표면 공급 비율"),FloatSlider(0,1)] public float TransferMultiplier=1;
        [DataInput,Label("색 어두워짐"),FloatSlider(0,1)] public float Darkening=.2f;
        [DataInput,Label("광택 증가량"),FloatSlider(0,1)] public float Smoothness=.8f;
        [DataInput,Label("비침 허용")] public bool Opacity;
        [DataInput,Label("완전히 젖은 불투명도"),FloatSlider(0,1)] public float WetOpacity=.1f;
        [DataInput,Label("건조 사용")] public bool Drying=true;
        [DataInput,Label("건조 시간 (초)"),FloatSlider(1,120)] public float DrySeconds=10;
        public string GetHeader()
        {
            var entry=Parent?.SurfaceEntries?.FirstOrDefault(e=>e.Key==Key);
            string name=entry?.DisplayName ?? (string.IsNullOrEmpty(Key) ? "메쉬 / 머티리얼 선택" : "표면 연결 대기");
            var failure=entry==null ? "현재 캐릭터에서 표면을 찾을 수 없음" :
                BlockOnly ? WetCharacterCatalog.CheckGeometry(entry) : entry.Support==WetShaderAdapter.Supported ? null : entry.Support;
            return (BlockOnly ? "차단 · " : Enabled ? "젖음 · " : "꺼짐 · ")+name+
                ((Enabled || BlockOnly) && failure!=null ? " · 연결 불가: "+failure : "");
        }
        protected UniTask<AutoCompleteList> SurfaceChoices()
        {
            var entries=Parent?.SurfaceEntries;
            return UniTask.FromResult(entries==null ? AutoCompleteList.Message("캐릭터를 선택하세요") :
                AutoCompleteList.Single(entries.Select(e=>new AutoCompleteEntry{value=e.Key,label=e.DisplayName+
                    (e.Support==WetShaderAdapter.Supported ? "" : " · "+e.Support)})));
        }
    }
}
