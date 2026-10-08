using UnityEngine;
using System.Linq;
using Warudo.Core.Data;
using Warudo.Core.Attributes;
using Warudo.Core.Scenes;
using Warudo.Plugins.Core.Assets.Character;
using Warudo.Core;
using Warudo.Plugins.Core;

namespace SurfaceWetness.WaterGun
{
    [AssetType(Id="surfacewetness.water-gun.asset", Title="WaterGun")]
    public sealed class WaterGunAsset : Asset
    {
        [DataInput, Label("대상 캐릭터")] public CharacterAsset Character;
        [DataInput, Label("버전"), Disabled] public string Version=WaterGunPlugin.CurrentVersion;
        [DataInput, Label("등록할 표면 번호 (목록의 # 번호)")] public int SurfaceIndex=1;
        [DataInput, Label("물총 꺼냄")] public bool GunDrawn;
        [DataInput, Label("물총 사용")] public bool GunEnabled=true;
        [DataInput, Label("토글키")] public UnityEngine.InputSystem.Key ToggleKey=UnityEngine.InputSystem.Key.G;
        [DataInput, Label("건조 사용")] public bool Drying=true;
        [DataInput,Label("안쪽 표면 전달")] public bool Transfer=true;
        [DataInput,Label("직접 적중점부터 최대 전달 깊이 (m)"),FloatSlider(0,.5f)] public float TransferDepth=.15f;
        [DataInput, Label("등록 기본값: 건조 시간 (초)"), FloatSlider(1,120)] public float DrySeconds=10;
        [DataInput, Label("실제 브러시 반경 (m)"), FloatSlider(.005f,.3f)] public float Radius=.1f;
        [DataInput, Label("초당 공급량"), FloatSlider(0,3)] public float Supply=3;
        [DataInput, Label("등록 기본값: 색 어두워짐"), FloatSlider(0,1)] public float Darkening=.2f;
        [DataInput, Label("등록 기본값: 광택 증가량"), FloatSlider(0,1)] public float Smoothness=.8f;
        [DataInput, Label("등록 기본값: 비침 허용")] public bool Opacity=true;
        [DataInput, Label("등록 기본값: 젖은 불투명도"), FloatSlider(0,1)] public float WetOpacity=.1f;
        [DataInput, Label("진단 HUD")] public bool ShowHud=true;
        [DataInput, Label("상태"), Disabled] public string Status="캐릭터를 선택하세요";
        [DataInput, Label("메쉬 렌더러 수"), Disabled] public int RendererCount;
        [DataInput, Label("머티리얼 슬롯 수"), Disabled] public int MaterialSlotCount;
        [DataInput, Label("젖음 연결 후보 수"), Disabled] public int CandidateCount;
        [DataInput, Label("메쉬·머티리얼 목록"), MultilineInput, Disabled] public string SurfaceList="";
        private GameObject boundRoot;
        private CharacterAsset boundCharacter;
        private float nextScan;
        private GameObject host;
        private WaterGunDriver driver;
        private WetCharacterCatalog.Snapshot snapshot;
        private Material updateMaterial,coverageMaterial;
        private string bindingKeys;
        private WetSurfaceProfile[] boundProfiles;
        [DataInput,Label("표면별 설정")] public WetSurfaceProfile[] Profiles=new WetSurfaceProfile[0];
        [DataInput,Label("설정 저장 진단"),Disabled] public string PersistenceStatus;
        public System.Collections.Generic.List<WetCharacterCatalog.Entry> SurfaceEntries => snapshot?.Entries;
        private string bindingError;
        private float nextStatus;
        private string registrationIssue;

        protected override void OnCreate()
        {
            base.OnCreate();
            try
            {
                host=Plugin.ModHost.Assets.Instantiate<GameObject>("Assets/WaterGun/Prefabs/WaterGunRuntime.prefab");
                if(host==null)throw new System.InvalidOperationException("WaterGun prefab missing");
                driver=host.AddComponent<WaterGunDriver>();
                if(driver==null)throw new System.InvalidOperationException("WaterGunDriver 생성 실패 · 핫리로드 후 에셋을 다시 추가하세요");
                updateMaterial=Plugin.ModHost.Assets.Load<Material>("Assets/WaterGun/Materials/MaskUpdate.mat");
                coverageMaterial=Plugin.ModHost.Assets.Load<Material>("Assets/WaterGun/Materials/SurfaceCoverage.mat");
                driver.Initialize(Plugin.ModHost.Assets.Load<Material>("Assets/WaterGun/Materials/WaterStream.mat"),Plugin.ModHost.Assets.Load<Material>("Assets/WaterGun/Materials/WaterSplash.mat"));
                driver.DrawnChanged=value=>SetDataInput(nameof(GunDrawn),value,broadcast:true);
                try{PersistenceStatus=WetProfilePersistenceProbe.Run(this);Debug.Log("[WaterGun] "+PersistenceStatus);}
                catch(System.Exception e){PersistenceStatus="SDK 프로필 저장 검사 실패: "+e.Message;Debug.LogException(e);}
                SetActive(true);
            }
            catch{if(host!=null)Object.Destroy(host);throw;}
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if(Version!=WaterGunPlugin.CurrentVersion){Version=WaterGunPlugin.CurrentVersion;BroadcastDataInput(nameof(Version));}
            var root = Character != null ? Character.GameObject : null;
            bool changed = Character != boundCharacter || root != boundRoot;
            if (!changed && Time.unscaledTime < nextScan) return;
            if(changed){Unbind();bindingError=null;snapshot=null;registrationIssue=null;}
            boundCharacter=Character;boundRoot=root;nextScan=Time.unscaledTime+1;
            snapshot = WetCharacterCatalog.Scan(root);
            if (RendererCount != snapshot.RendererCount) { RendererCount=snapshot.RendererCount; BroadcastDataInput(nameof(RendererCount)); }
            if (MaterialSlotCount != snapshot.Entries.Count) { MaterialSlotCount=snapshot.Entries.Count; BroadcastDataInput(nameof(MaterialSlotCount)); }
            if (CandidateCount != snapshot.CandidateCount) { CandidateCount=snapshot.CandidateCount; BroadcastDataInput(nameof(CandidateCount)); }
            if (SurfaceList != snapshot.Report) { SurfaceList=snapshot.Report; BroadcastDataInput(nameof(SurfaceList)); }
        }

        public override void OnPostLateUpdate()
        {
            base.OnPostLateUpdate();
            if(driver==null)return;
            var profiles=Profiles ?? new WetSurfaceProfile[0];
            string keys=string.Join("\n",profiles.Select(p=>p==null ? "null" : p.Key+":"+p.Enabled+":"+p.BlockOnly));
            if(bindingKeys!=keys || boundProfiles==null || !profiles.SequenceEqual(boundProfiles) || (driver.Bindings!=null && !driver.Bindings.IsCurrent(snapshot)))
            {Unbind();bindingError=null;nextScan=0;snapshot=WetCharacterCatalog.Scan(boundRoot);}
            bindingKeys=keys;
            if(boundProfiles==null)boundProfiles=(WetSurfaceProfile[])profiles.Clone();
            if(driver.Bindings==null && bindingError==null && snapshot!=null && boundRoot!=null)
            {
                try{driver.Bindings=new WetSurfaceBindings(snapshot,profiles,updateMaterial,coverageMaterial);}
                catch(System.Exception e){bindingError=e.Message;Debug.LogException(e);}
            }
            var core=Context.PluginManager.GetPlugin<CorePlugin>();
            driver.DisplayCamera=core!=null ? core.MainCamera : null;
            driver.Drawn=GunDrawn;driver.Enabled=GunEnabled;driver.ToggleKey=ToggleKey;
            driver.Drying=Drying;driver.DrySeconds=DrySeconds;driver.Radius=Radius;driver.Rate=Supply;
            driver.Transfer=Transfer;driver.TransferDepth=TransferDepth;
            driver.ShowHud=ShowHud;
            driver.ConnectionIssue=bindingError!=null ? "연결 실패: "+bindingError : boundRoot==null ? "대상 캐릭터를 선택하세요" :
                registrationIssue ?? (profiles.Length==0 ? "표면 미등록 · 목록의 # 번호로 표면 등록" :
                driver.Bindings?.Report ?? "표면 연결 대기");
            driver.Tick(Time.deltaTime);
            var text=bindingError!=null ? "연결 실패: "+bindingError : (driver.Bindings?.Report ?? "")+driver.Status;
            if(registrationIssue!=null)text=registrationIssue+"\n"+text;
            if(Status!=text && Time.unscaledTime>=nextStatus){Status=text;BroadcastDataInput(nameof(Status));nextStatus=Time.unscaledTime+.3f;}
        }
        [Trigger,Label("번호의 표면 등록")]
        public void AddSurface()
        {
            if(snapshot==null || SurfaceIndex<0 || SurfaceIndex>=snapshot.Entries.Count)return;
            var entry=snapshot.Entries[SurfaceIndex];
            if(entry.Support!=WetShaderAdapter.Supported)
            {
                registrationIssue="등록하지 못함 · "+entry.DisplayName+": "+entry.Support+
                    " · 차단만 필요하면 표면별 설정에서 판정 전용 차단을 사용하세요";
                SetDataInput(nameof(Status),registrationIssue,broadcast:true);return;
            }
            registrationIssue=null;
            if((Profiles ?? new WetSurfaceProfile[0]).Any(p=>p!=null && p.Key==entry.Key))return;
            var p=StructuredData.Create<WetSurfaceProfile,WaterGunAsset>(this,value=>
            {value.Key=entry.Key;value.Darkening=Darkening;value.Smoothness=Smoothness;value.Opacity=Opacity;
             value.WetOpacity=WetOpacity;value.DrySeconds=DrySeconds;value.Order=(Profiles ?? new WetSurfaceProfile[0]).Length;});
            SetDataInput(nameof(Profiles),(Profiles ?? new WetSurfaceProfile[0]).Concat(new[]{p}).ToArray(),broadcast:true);
        }
        [Trigger,Label("다시 연결 (젖음 초기화)")]
        public void Rebind(){Unbind();bindingError=null;registrationIssue=null;nextScan=0;}
        private void Unbind()
        {
            if(driver?.Bindings!=null){driver.Bindings.Dispose();driver.Bindings=null;}
            bindingKeys=null;
            boundProfiles=null;
        }
        protected override void OnDestroy()
        {
            Unbind();if(host!=null)Object.Destroy(host);host=null;driver=null;
            boundRoot=null;boundCharacter=null;
            base.OnDestroy();
        }
    }
}
