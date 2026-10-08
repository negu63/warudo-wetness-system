using UnityEngine;
using UnityEngine.InputSystem;

namespace SurfaceWetness.WaterGun
{
    public sealed class WaterGunDriver : MonoBehaviour
    {
        public WetSingleSurface Surface;
        public WetSurfaceBindings Bindings;
        public Camera DisplayCamera;
        public bool Drawn, Enabled=true, Drying=true, ShowHud=true, Opacity;
        public bool Transfer=true;
        public float TransferDepth=.15f;
        public Key ToggleKey=Key.G;
        public float Range=8, Radius=.1f, Rate=3, Hardness=.5f, DrySeconds=10, Darkening=.2f, Smoothness=.8f, WetOpacity=.1f;
        public System.Action<bool> DrawnChanged;
        public WetWaterGun Gun { get; private set; }
        public string Status { get; private set; }="연결 표면을 선택하세요";
        public string ConnectionIssue;
        private readonly WetGunToggle toggle=new WetGunToggle();
        private static readonly Unity.Profiling.ProfilerMarker TickMarker=new Unity.Profiling.ProfilerMarker("WaterGun.Tick");
        private bool blocked,previousDrawn;
        public bool Held { get; private set; }
        public int Deposits { get; private set; }

        public void Initialize(Material stream,Material splash)
        {Gun=new WetWaterGun(transform.Find("WaterGunRig"),stream,splash);Gun.SetVisible(false);}

        // Warudo invokes this after character update; editor checks can supply deterministic input.
        public void Tick(float delta,Vector2? pointerOverride=null,bool? buttonOverride=null,bool ignoreFocus=false)
        {
            using(var scope=TickMarker.Auto())
            {
            bool focused=ignoreFocus || Application.isFocused;
            var keyboard=Keyboard.current;
            bool key=keyboard!=null && ToggleKey!=Key.None && keyboard[ToggleKey].isPressed;
            if(toggle.UpdateButton(ToggleKey,key,focused && Enabled)){Drawn=!Drawn;DrawnChanged?.Invoke(Drawn);}
            if(previousDrawn!=Drawn){blocked=true;Held=false;Gun?.Stop();previousDrawn=Drawn;}
            bool down=buttonOverride ?? (Mouse.current!=null && Mouse.current.leftButton.isPressed);
            Vector2 pointer=pointerOverride ?? (Mouse.current!=null ? Mouse.current.position.ReadValue() : Vector2.zero);
            bool inside=pointer.x>=0 && pointer.y>=0 && pointer.x<Screen.width && pointer.y<Screen.height;
            bool overHud=ShowHud && new Rect(12,12,430,270).Contains(new Vector2(pointer.x,Screen.height-pointer.y));
            bool hasSurface=Bindings!=null ? Bindings.Surfaces.Count>0 : Surface!=null;
            // Keep the camera-mounted gun posed even when input or wetness binding is blocked.
            // Visibility must never expose the prefab's unposed world position.
            Ray ray=default(Ray);
            if(DisplayCamera!=null)
            {
                var viewport=inside ? new Vector3(pointer.x/Screen.width,pointer.y/Screen.height,0) : new Vector3(.5f,.5f,0);
                ray=DisplayCamera.ViewportPointToRay(viewport);
                Gun?.Aim(DisplayCamera,ray.GetPoint(Range),new Vector3(.2f,-.13f,.65f),new Vector3(0,.02f,.14f));
            }
            bool allowed=Enabled && Drawn && focused && inside && !overHud && DisplayCamera!=null && hasSurface;
            if(!down)blocked=false;if(down && !allowed)blocked=true;
            Held=down && allowed && !blocked;
            if(Bindings!=null)Bindings.Update(delta,Drying);
            else if(Surface!=null)
            {
                if(Drying)Surface.State.Dry(delta,DrySeconds);
                Surface.Apply(Darkening,Smoothness,Opacity,WetOpacity);
            }
            Gun?.SetVisible(Enabled && Drawn && DisplayCamera!=null);
            if(!allowed)
            {
                Gun?.Stop();Bindings?.ClearPath();
                Status=!Enabled ? "물총 사용 OFF" : !Drawn ? "물총 넣음 · 건조 유지" : DisplayCamera==null ? "Warudo 카메라 연결 대기" :
                    !hasSurface ? (string.IsNullOrEmpty(ConnectionIssue) ? "연결 표면 0개 · 번호의 표면 등록 필요" : ConnectionIssue) :
                    !focused ? "Warudo 렌더 창에 포커스 필요" : !inside ? "마우스가 렌더 창 밖에 있음" : "HUD 위 입력 차단 · 캐릭터 위로 이동";
                return;
            }
            WetRayHit hit;
            WetSurfaceBindings.Surface selected=null;
            // Screen aim owns the direct hit and the entire transfer path. The muzzle
            // only starts the visual stream; proximity must not change the wet target.
            bool found;
            if(Bindings!=null)
            {
                found=Bindings.TracePath(ray,Range,TransferDepth,Transfer)>0;
                if(found){hit=Bindings.Path[0].Hit;selected=Bindings.Path[0].Surface;}
                else hit=default(WetRayHit);
            }
            else found=Surface.Trace(ray,Range,out hit);
            var aim=found ? hit.point : ray.GetPoint(Range);
            Gun.Aim(DisplayCamera,aim,new Vector3(.2f,-.13f,.65f),new Vector3(0,.02f,.14f));
            Gun.Draw(aim,Held,found,found ? hit.normal : -ray.direction,.012f);
            var target=Bindings!=null ? selected?.Binding : Surface;
            if(found && Held)
            {
                if(Bindings!=null)Bindings.SupplyPath(ray.direction,Radius,Hardness,Rate*delta);
                else target.Deposit(hit,ray.direction,Radius,Hardness,Rate*delta);
                Deposits++;
                if(Bindings!=null)Bindings.Update(0,false);else Surface.Apply(Darkening,Smoothness,Opacity,WetOpacity);
            }
            Status=found ? (Held ? "분사 · " : down && blocked ? "버튼을 놓고 다시 누르세요 · " : "조준 · ")+(Bindings!=null ? selected.Entry.DisplayName : target.Entry.DisplayName)+" UV "+hit.textureCoord.ToString("F3") : "표면 없음";
            if(found && Bindings!=null)Status+=" · 경로 "+Bindings.PathCount;
            }
        }
        private void OnGUI()
        {
            if(!ShowHud)return;
            var old=GUI.color;GUI.color=new Color(.02f,.03f,.04f,.85f);
            GUI.DrawTexture(new Rect(12,12,430,270),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(24,20,405,90),"WaterGun "+WaterGunPlugin.CurrentVersion+" · 표면 전달\n"+Status+"\n총 "+(Drawn ? "꺼냄" : "넣음")+" ["+ToggleKey+"] 공급 "+Held+" / 스탬프 "+Deposits);
            if(Bindings!=null)
                for(int i=0;i<Mathf.Min(5,Bindings.Surfaces.Count);i++)
                {
                    var b=Bindings.Surfaces[i].Binding;
                    GUI.Label(new Rect(24+80*i,92,78,20),Bindings.Surfaces[i].Entry.Renderer.name+":"+Bindings.Surfaces[i].Entry.Slot);
                    if(b!=null)GUI.DrawTexture(new Rect(24+80*i,110,76,76),b.State.Mask,ScaleMode.StretchToFill,false);
                    else GUI.Label(new Rect(24+80*i,130,76,40),"차단 전용");
                }
            else if(Surface!=null)GUI.DrawTexture(new Rect(24,110,76,76),Surface.State.Mask,ScaleMode.StretchToFill,false);
            if(Bindings!=null)
                for(int i=0;i<Mathf.Min(3,Bindings.PathCount);i++)
                {
                    var item=Bindings.Path[i];
                    GUI.Label(new Rect(24,194+24*i,405,24),(i==0 ? "직접 " : "전달 ")+item.Surface.Entry.Renderer.name+":"+item.Surface.Entry.Slot+" UV "+item.Hit.textureCoord.ToString("F3")+(item.Surface.Profile.BlockOnly ? " · 차단" : item.Surface.Profile.StopAfterWet ? " · 종료" : ""));
                }
            GUI.color=old;
        }
        private void OnDisable(){Held=false;blocked=true;Gun?.Stop();}
        private void OnDestroy(){Gun?.Dispose();Gun=null;}
    }
}
