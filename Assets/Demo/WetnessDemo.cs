using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using SurfaceWetness.WaterGun;

[DefaultExecutionOrder(500)]
public sealed class WetnessDemo : MonoBehaviour
{
    public GameObject Character, Gun;
    public Camera View;
    public Material MaskUpdate, Coverage, Stream, Splash;
    public bool Animate, Drying = true, Transfer = true, Translucency = true;
    public float Radius = .1f, Rate = 3, DrySeconds = 10;
    public WetSurfaceBindings Bindings { get; private set; }
    public WaterGunDriver Driver { get; private set; }
    private WetBoneLoop motion;
    private Font font;
    private GUISkin demoSkin;
    private bool probe, probeHeld;
    private Vector2 probePoint;
    private readonly Rect panel = new Rect(16,16,320,690);
    private float lastRate, lastRadius, lastDrySeconds;
    private bool lastDrying, lastTransfer;
    private sealed class OpacityPreference
    {
        public bool Requested, Applied;
    }
    private readonly Dictionary<WetSurfaceBindings.Surface,OpacityPreference> opacityPreferences =
        new Dictionary<WetSurfaceBindings.Surface,OpacityPreference>();

    private void Start()
    {
        Application.runInBackground = true;
        font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 16);
        motion = Character.AddComponent<WetBoneLoop>(); motion.Animate = false;
        var catalog = WetCharacterCatalog.Scan(Character);
        var profiles = catalog.Entries.Select(e => new WetSurfaceProfile {
            Key = e.Key, Order = e.Renderer.name == "OuterCloth" ? 0 : e.Renderer.name == "InnerCloth" ? 1 : 2,
            StopAfterWet = e.Renderer.name == "Skin", DrySeconds = DrySeconds,
            Darkening = e.Material.GetFloat("_WetDarkening"),
            Opacity = e.Material.GetFloat("_WetOpacityEnabled") > .5f
        }).ToArray();
        Bindings = new WetSurfaceBindings(catalog, profiles, MaskUpdate, Coverage);
        foreach(var surface in Bindings.Surfaces)
        {
            var preference = new OpacityPreference { Requested = surface.Profile.Opacity };
            preference.Applied = Translucency && preference.Requested;
            surface.Profile.Opacity = preference.Applied;
            opacityPreferences.Add(surface,preference);
        }
        Bindings.Update(0,false);
        Driver = Gun.GetComponent<WaterGunDriver>();
        if(Driver == null)
        {
            Driver = Gun.AddComponent<WaterGunDriver>();Driver.Drawn = true;
            Driver.Rate=Rate;Driver.Radius=Radius;Driver.DrySeconds=DrySeconds;Driver.Drying=Drying;Driver.Transfer=Transfer;
        }
        else
        {
            Rate=Driver.Rate;Radius=Driver.Radius;DrySeconds=Driver.DrySeconds;Drying=Driver.Drying;Transfer=Driver.Transfer;
        }
        lastRate=Rate;lastRadius=Radius;lastDrySeconds=DrySeconds;lastDrying=Drying;lastTransfer=Transfer;
        Driver.Bindings = Bindings; Driver.DisplayCamera = View;
        Driver.ShowHud = false;
        if(Driver.Gun == null)Driver.Initialize(Stream,Splash);
        if (Environment.GetCommandLineArgs().Contains("-demoSmoke")) StartCoroutine(Smoke());
        if (Environment.GetCommandLineArgs().Contains("-materialEditingSmoke")) StartCoroutine(MaterialEditingSmoke());
        if (Environment.GetCommandLineArgs().Contains("-gunEditingSmoke")) StartCoroutine(GunEditingSmoke());
    }
    private void LateUpdate()
    {
        if (Driver == null) return;
        motion.Animate = Animate;
        SyncControl(ref Rate,ref Driver.Rate,ref lastRate);
        SyncControl(ref Radius,ref Driver.Radius,ref lastRadius);
        SyncControl(ref DrySeconds,ref Driver.DrySeconds,ref lastDrySeconds);
        SyncControl(ref Drying,ref Driver.Drying,ref lastDrying);
        SyncControl(ref Transfer,ref Driver.Transfer,ref lastTransfer);
        foreach (var surface in Bindings.Surfaces)
        {
            var material = surface.Binding.RuntimeMaterial;
            var preference = opacityPreferences[surface];
            bool editedOpacity = material.GetFloat("_WetOpacityEnabled") > .5f;
            // Keep the material's preference separate from the temporary demo-wide visibility switch.
            if(editedOpacity != preference.Applied) preference.Requested = editedOpacity;
            surface.Profile.Darkening = material.GetFloat("_WetDarkening");
            surface.Profile.Smoothness = material.GetFloat("_WetSmoothness");
            surface.Profile.WetOpacity = material.GetFloat("_WetOpacity");
            surface.Profile.DrySeconds = DrySeconds;
            surface.Profile.Opacity = Translucency && preference.Requested;
            preference.Applied = surface.Profile.Opacity;
        }
        Vector2 pointer = probe ? probePoint : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        bool down = probe ? probeHeld : Mouse.current != null && Mouse.current.leftButton.isPressed;
        Vector2 guiPoint = new Vector2(pointer.x * 1200f / Screen.width, (Screen.height-pointer.y)*720f/Screen.height);
        // GUI events must never also deposit water on the character behind the panel.
        Driver.Tick(Time.deltaTime,pointer,down && !panel.Contains(guiPoint),probe);
    }
    [ContextMenu("Reset Wetness")]
    public void ResetWetness() { if (Bindings != null) foreach (var s in Bindings.Surfaces) s.Binding?.State.Clear(); }
    private void UseDemoSkin()
    {
        if(demoSkin)return;
        demoSkin=Instantiate(GUI.skin);demoSkin.font=font;
        demoSkin.label.font=font;demoSkin.label.fontSize=14;demoSkin.label.wordWrap=true;
    }
    private void OnGUI()
    {
        var oldSkin = GUI.skin; var old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width/1200f,Screen.height/720f,1));
        UseDemoSkin(); GUI.skin = demoSkin;
        GUI.color = new Color(.025f,.045f,.075f,.96f); GUI.DrawTexture(panel,Texture2D.whiteTexture); GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(32,28,288,668));
        GUILayout.Label("물총 · 젖음 테스트", new GUIStyle(GUI.skin.label){fontSize=23,fontStyle=FontStyle.Bold});
        GUILayout.Label("캐릭터 위에서 왼쪽 버튼으로 분사\nG: 물총 넣기 / 꺼내기");
        GUILayout.Space(12);
        GUILayout.Label("공급량: "+Rate.ToString("F2")+" / 초\n브러시 반경: "+Radius.ToString("F3")+" m");
        GUILayout.Label("건조: "+(Drying ? "ON · "+DrySeconds.ToString("F1")+"초" : "OFF")+
            "\n안쪽 전달: "+(Transfer ? "ON" : "OFF")+"\n재질별 비침: "+(Translucency ? "ON" : "OFF"));
        GUILayout.Space(10);
        GUILayout.Label(Driver == null ? "초기화 중" : Driver.Status);
        GUILayout.Label("분사 스탬프: "+(Driver != null ? Driver.Deposits : 0));
        GUILayout.Space(8); GUILayout.Label("표면별 젖음 마스크");
        if (Bindings != null) {
            GUILayout.BeginHorizontal();
            foreach (var s in Bindings.Surfaces.OrderBy(s=>s.Profile.Order).Take(3)) {
                GUILayout.BeginVertical(GUILayout.Width(88)); GUILayout.Label(s.Profile.Order==0 ? "겉옷" : s.Profile.Order==1 ? "안쪽 옷" : "피부");
                if (s.Binding != null) GUILayout.Label(s.Binding.State.Mask,GUILayout.Width(84),GUILayout.Height(84));
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndArea(); GUI.matrix = old; GUI.skin = oldSkin;
    }
    private static void SyncControl<T>(ref T demo,ref T driver,ref T previous)
    {
        // Only an explicit demo edit wins; an unchanged mirror must accept driver Inspector edits.
        if(!EqualityComparer<T>.Default.Equals(demo,previous))driver=demo;
        else demo=driver;
        previous=demo;
    }
    private float MaskValue()
    {
        var state = Bindings.Surfaces.First(s=>s.Profile.Order==0).Binding.State;
        var old = RenderTexture.active; var cpu = new Texture2D(512,512,TextureFormat.RGBAFloat,false,true);
        try { RenderTexture.active=state.Mask; cpu.ReadPixels(new Rect(0,0,512,512),0,0);cpu.Apply();return cpu.GetPixels().Max(c=>c.r); }
        finally { RenderTexture.active=old;Destroy(cpu); }
    }
    private IEnumerator Smoke()
    {
        Directory.CreateDirectory("Artifacts");
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        yield return new WaitForSeconds(.5f);
        yield return null;
        try {
            if (Bindings.Surfaces.Count != 3) throw new Exception("Expected three connected surfaces");
            var outer = Bindings.Surfaces.First(s=>s.Profile.Order==0).Entry.Renderer.bounds;
            bool found=false;
            for(int y=3;y<=8&&!found;y++) for(int x=3;x<=7&&!found;x++) {
                var world=new Vector3(Mathf.Lerp(outer.min.x,outer.max.x,x/10f),Mathf.Lerp(outer.min.y,outer.max.y,y/10f),outer.center.z);
                var pixel=View.WorldToScreenPoint(world);
                if (pixel.x/Screen.width<.32f) continue;
                if(Bindings.TracePath(View.ScreenPointToRay(pixel),8,.15f,true)>=3){probePoint=new Vector2(pixel.x,pixel.y);found=true;}
            }
            if(!found) throw new Exception("Visible three-layer aim point missing");
            probe=true; probeHeld=false; Drying=false;
        } catch(Exception e){Fail(e);yield break;}
        yield return null; probeHeld=true;
        for(int i=0;i<40;i++)yield return null;
        float wet=0;
        try { wet=MaskValue(); if(wet<=.05f || Driver.Deposits==0 || Bindings.PathCount<3)throw new Exception("Scene input did not wet three-layer target"); }
        catch(Exception e){Fail(e);yield break;}
        probeHeld=false;
        yield return new WaitForEndOfFrame();
        var capture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.GetFullPath("Artifacts/demo-preview.png"),capture.EncodeToPNG());Destroy(capture);
        yield return null;
        try {
            float inner=Bindings.Surfaces.First(s=>s.Profile.Order==1).Binding.State.StampCount;
            if(inner<=0)throw new Exception("Inner surface received no water");
            ResetWetness(); if(MaskValue()>.001f)throw new Exception("Reset failed");
            Bindings.Dispose(); if(!Character.GetComponentsInChildren<Renderer>().Any())throw new Exception("Character lost");
            Bindings=null; Driver.Bindings=null; Driver.Enabled=false; enabled=false;
            File.WriteAllText("Artifacts/demo-smoke.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,surfaces=3,wet,innerStamps=inner,deposits=Driver.Deposits,reset=true},Newtonsoft.Json.Formatting.Indented));
        } catch(Exception e){Fail(e);yield break;}
        yield return new WaitForSeconds(.5f); Application.Quit(0);
    }
    private IEnumerator MaterialEditingSmoke()
    {
        Directory.CreateDirectory("Artifacts");
        yield return null;
        var surface = Bindings.Surfaces.First(s=>s.Profile.Order==0);
        var material = surface.Binding.RuntimeMaterial;
        string[] properties = { "_WetDarkening", "_WetSmoothness", "_WetOpacity", "_WetOpacityEnabled" };
        try {
            foreach(var s in Bindings.Surfaces)
                foreach(string property in properties)
                    AssertMaterialValue(s.Binding.RuntimeMaterial,property,s.Entry.Material.GetFloat(property));
            material.SetFloat("_WetDarkening",.43f);
            material.SetFloat("_WetSmoothness",.88f);
            material.SetFloat("_WetOpacity",.21f);
            material.SetFloat("_WetOpacityEnabled",0);
        } catch(Exception e) { Fail(e);yield break; }
        yield return null;
        try {
            AssertMaterialValue(material,"_WetDarkening",.43f);
            AssertMaterialValue(material,"_WetSmoothness",.88f);
            AssertMaterialValue(material,"_WetOpacity",.21f);
            AssertMaterialValue(material,"_WetOpacityEnabled",0);
            material.SetFloat("_WetOpacityEnabled",1);
        } catch(Exception e) { Fail(e);yield break; }
        yield return null;
        try { AssertMaterialValue(material,"_WetOpacityEnabled",1);Translucency=false; }
        catch(Exception e) { Fail(e);yield break; }
        yield return null;
        try { AssertMaterialValue(material,"_WetOpacityEnabled",0);Translucency=true; }
        catch(Exception e) { Fail(e);yield break; }
        yield return null;
        try {
            AssertMaterialValue(material,"_WetOpacityEnabled",1);
            AssertMaterialValue(material,"_WetOpacity",.21f);
            ResetWetness();AssertMaterialValue(material,"_WetDarkening",.43f);
            var renderer=surface.Entry.Renderer;int slot=surface.Entry.Slot;var original=surface.Entry.Material;
            Driver.Enabled=false;enabled=false;Bindings.Dispose();Bindings=null;Driver.Bindings=null;
            if(renderer.sharedMaterials[slot]!=original)throw new Exception("Original material was not restored");
            File.WriteAllText("Artifacts/material-editing-smoke.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {
                passed=true,authoredValuesPreserved=true,runtimeEditsPreserved=true,opacityToggleRestoresPreference=true,
                resetPreservesAppearance=true,originalMaterialRestored=true
            },Newtonsoft.Json.Formatting.Indented));
            Application.Quit(0);
        } catch(Exception e) { Fail(e); }
    }
    private IEnumerator GunEditingSmoke()
    {
        Directory.CreateDirectory("Artifacts");yield return null;
        Driver.Rate=.37f;Driver.Radius=.033f;Driver.DrySeconds=17;Driver.Drying=false;Driver.Transfer=false;
        yield return null;
        try {
            AssertGunValue(Driver.Rate,.37f,"Driver Rate edit");AssertGunValue(Rate,.37f,"Demo Rate mirror");
            AssertGunValue(Driver.Radius,.033f,"Driver Radius edit");AssertGunValue(DrySeconds,17,"DrySeconds mirror");
            if(Driver.Drying || Driver.Transfer || Drying || Transfer)throw new Exception("Boolean driver controls were overwritten");
            Rate=4.5f;Radius=.005f;DrySeconds=45;Drying=true;Transfer=true;
        }catch(Exception e){Fail(e);yield break;}
        for(int i=0;i<3;i++)yield return null;
        try {
            AssertGunValue(Driver.Rate,4.5f,"Demo Rate edit outside slider range");
            AssertGunValue(Driver.Radius,.005f,"Radius outside slider range");AssertGunValue(Driver.DrySeconds,45,"DrySeconds edit");
            if(!Driver.Drying || !Driver.Transfer)throw new Exception("Demo boolean controls not connected");
            Driver.Rate=0;
        }catch(Exception e){Fail(e);yield break;}
        yield return null;
        try {
            AssertGunValue(Driver.Rate,0,"Zero Rate");AssertGunValue(Rate,0,"Zero Rate mirror");
            var aim=FindGunTestPoint();ResetWetness();Driver.Drying=false;Driver.Transfer=true;Driver.Drawn=true;Driver.Radius=.065f;
            Driver.Tick(0,aim,false,true);Driver.Tick(.2f,aim,true,true);
            float zeroMask=MaskValue();if(zeroMask>.001f)throw new Exception("Rate zero still supplies water");
            Driver.Rate=1;Driver.Tick(.2f,aim,true,true);
            float suppliedMask=MaskValue();if(suppliedMask<.005f)throw new Exception("Positive Rate supplies no water");
            File.WriteAllText("Artifacts/gun-editing-smoke.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {
                passed=true,driverInspectorEdits=true,demoInspectorEdits=true,inspectorValuesUnclamped=true,
                zeroRateSupported=true,zeroMask,suppliedMask,radius=true,drying=true,transfer=true,drySeconds=true
            },Newtonsoft.Json.Formatting.Indented));Application.Quit(0);
        }catch(Exception e){Fail(e);}
    }
    private Vector2 FindGunTestPoint()
    {
        var bounds=Bindings.Surfaces.First(s=>s.Profile.Order==0).Entry.Renderer.bounds;
        for(int y=3;y<=8;y++)for(int x=3;x<=7;x++)
        {
            var world=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x/10f),Mathf.Lerp(bounds.min.y,bounds.max.y,y/10f),bounds.center.z);
            var pixel=View.WorldToScreenPoint(world);
            if(pixel.x/Screen.width>=.32f && Bindings.TracePath(View.ScreenPointToRay(pixel),8,.15f,true)>=3)
                return new Vector2(pixel.x,pixel.y);
        }
        throw new Exception("Gun supply test target missing");
    }
    private static void AssertGunValue(float actual,float expected,string context)
    { if(Mathf.Abs(actual-expected)>.0001f)throw new Exception(context+": expected "+expected+" but was "+actual); }
    private static void AssertMaterialValue(Material material,string property,float expected)
    {
        float actual=material.GetFloat(property);
        if(Mathf.Abs(actual-expected)>.0001f)throw new Exception(material.name+" "+property+" expected "+expected+" but was "+actual);
    }
    private static void Fail(Exception e) { Debug.LogException(e); File.WriteAllText("Artifacts/demo-failure.txt",e.ToString());Application.Quit(1); }
    private void OnDestroy() { Bindings?.Dispose(); if(demoSkin)Destroy(demoSkin);if(font)Destroy(font); }
}
