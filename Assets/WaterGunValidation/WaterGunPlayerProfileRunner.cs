using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using SurfaceWetness.WaterGun;

// Dedicated Development Player benchmark. Never attached to shipping prefabs.
[DefaultExecutionOrder(500)]
public sealed class WaterGunPlayerProfileRunner : MonoBehaviour
{
    public GameObject CharacterPrefab, GunPrefab;
    public Material MaskUpdate, Coverage, Stream, Splash;
    public bool GraphicsJobsBuild;
    public int SampleFrames = 180, WarmupFrames = 60;
    public static readonly Guid MetadataId = new Guid("7a4dc99d-f058-4645-b14d-253881f29420");
    private readonly string[] modes = { "dry-opacity-off", "dry-opacity-on", "partial-wet", "full-wet", "continuous-fire", "drying" };
    private readonly string[] names = { "Main Thread", "Render Thread", "GC Allocated In Frame", "WaterGun.Tick", "WaterGun.BakeMesh", "WaterGun.BVHRefit", "WaterGun.Trace", "WaterGun.Brush", "WaterGun.Deposit", "WaterGun.Dry", "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "Batches Count" };
    private readonly Dictionary<string, ProfilerRecorder> recorders = new Dictionary<string, ProfilerRecorder>();
    private sealed class Row
    {
        public int scenario, frame, pathCount, deposits, geometryRefresh;
        public bool firing;
        public float elapsedMs;
        public long[] metrics;
    }
    private Row[] rows;
    private int written, scenario, frame, previousFrame, previousBakes, previousDeposits;
    private readonly int[] metadata = new int[3];
    private WetSurfaceBindings bindings;
    private WetGeometry[] geometries;
    private int[] refreshCounts;
    private WetBoneLoop loop;
    private WaterGunDriver driver;
    private Camera profileCamera;
    private RenderTexture cameraTarget;
    private Texture2D partialMask;
    private string output;
    private bool finished;
    private bool gpuCapture;

    private void Start()
    {
        try { Begin(); }
        catch (Exception e) { Fail(e); }
    }
    private void Begin()
    {
        var arguments = Environment.GetCommandLineArgs();
        gpuCapture = !GraphicsJobsBuild && !arguments.Contains("-profileCpuOnly");
        string run = "player-" + (GraphicsJobsBuild ? "on" : "off");
        output = Application.persistentDataPath;
        for (int i = 0; i + 1 < arguments.Length; i++)
        {
            if (arguments[i] == "-profileOutput") output = arguments[++i];
            else if (arguments[i] == "-profileRun") run = arguments[++i];
        }
        output = Path.Combine(output, run); Directory.CreateDirectory(output);
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
        var target = Instantiate(CharacterPrefab);
        loop = target.AddComponent<WetBoneLoop>(); loop.Animate = false; loop.ApplyPose(0);
        var catalog = WetCharacterCatalog.Scan(target);
        var profiles = catalog.Entries.Select(e => new WetSurfaceProfile {
            Key = e.Key, Order = e.Renderer.name == "OuterCloth" ? 0 : e.Renderer.name == "InnerCloth" ? 1 : 2,
            StopAfterWet = e.Renderer.name == "Skin", Opacity = false, DrySeconds = 20,
            WetOpacity = .35f
        }).ToArray();
        bindings = new WetSurfaceBindings(catalog, profiles, MaskUpdate, Coverage);
        if (bindings.Surfaces.Count != 3) throw new InvalidOperationException("Demo requires three registered slots: " + bindings.Report);
        geometries = bindings.Surfaces.Select(s => s.Geometry).Distinct().ToArray();
        refreshCounts = new int[geometries.Length];
        var bounds = bindings.Surfaces.First(s => s.Profile.Order == 0).Entry.Renderer.bounds;
        Ray ray = default(Ray); bool found = false;
        for (int side = 0; side < 2 && !found; side++) for (int y = 3; y < 8 && !found; y++) for (int x = 3; x < 8 && !found; x++)
        {
            var aim = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, x / 10f), Mathf.Lerp(bounds.min.y, bounds.max.y, y / 10f), bounds.center.z);
            ray = new Ray(aim + Vector3.forward * (side == 0 ? -3 : 3), side == 0 ? Vector3.forward : Vector3.back);
            found = bindings.TracePath(ray, 8, .15f, true) == 3;
        }
        if (!found) throw new InvalidOperationException("Three-layer ray required");
        profileCamera = new GameObject("ProfileCamera").AddComponent<Camera>();
        profileCamera.transform.SetPositionAndRotation(ray.origin, Quaternion.LookRotation(ray.direction));
        profileCamera.renderingPath = RenderingPath.Forward; profileCamera.depthTextureMode = DepthTextureMode.Depth;
        profileCamera.clearFlags = CameraClearFlags.SolidColor; profileCamera.backgroundColor = new Color(.15f, .3f, .5f);
        // Hidden benchmark windows may skip screen cameras. Explicit offscreen rendering
        // guarantees the workload; presentation/desktop composition is outside this test.
        cameraTarget = new RenderTexture(1200, 600, 24, RenderTextureFormat.ARGBHalf);
        cameraTarget.Create(); profileCamera.targetTexture = cameraTarget; profileCamera.enabled = false;
        var light = new GameObject("ProfileLight").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1; light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(40, 30, 0);
        var host = Instantiate(GunPrefab); driver = host.AddComponent<WaterGunDriver>();
        driver.Bindings = bindings; driver.DisplayCamera = profileCamera; driver.ShowHud = false;
        driver.ToggleKey = UnityEngine.InputSystem.Key.None; driver.Initialize(Stream, Splash);
        partialMask = new Texture2D(512, 512, TextureFormat.RGBA32, false, true);
        var pixels = new Color[512 * 512];
        for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++)
        {
            float distance = Vector2.Distance(new Vector2(x / 511f, y / 511f), new Vector2(.5f, .5f));
            pixels[y * 512 + x] = Color.white * Mathf.Clamp01((.25f - distance) / .04f);
        }
        partialMask.SetPixels(pixels); partialMask.Apply();
        // Warm-register custom markers before discovering counters.
        driver.Drawn = true; driver.Tick(1f / 60, Center(), false, true); driver.Tick(1f / 60, Center(), true, true);
        rows = new Row[SampleFrames * modes.Length * 2];
        for (int i = 0; i < rows.Length; i++) rows[i] = new Row { metrics = new long[names.Length] };
        var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
        foreach (string name in names) foreach (var handle in handles)
        {
            var description = ProfilerRecorderHandle.GetDescription(handle);
            if (description.Name != name) continue;
            recorders[name] = ProfilerRecorder.StartNew(description.Category, name, 1, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame); break;
        }
        Profiler.enabled = false; Profiler.SetAreaEnabled(ProfilerArea.GPU, gpuCapture);
        Prepare();
        File.WriteAllText(Path.Combine(output, "started.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new {
            graphicsJobsSetting = GraphicsJobsBuild, actualThreadingMode = SystemInfo.renderingThreadingMode.ToString(),
            available = handles.Select(h => ProfilerRecorderHandle.GetDescription(h).Name).ToArray()
        }));
    }
    private Vector2 Center() => new Vector2(Screen.width * .5f, Screen.height * .5f);
    private void Prepare()
    {
        // Unity 2021's Profiler history is limited to 2,000 frames. A file per
        // condition keeps every sample reloadable, rather than silently losing baselines.
        Profiler.enabled = false; Profiler.enableBinaryLog = false;
        Profiler.logFile = Path.Combine(output, "scenario-" + scenario + ".raw");
        Profiler.enableBinaryLog = true; Profiler.enabled = true;
        int mode = scenario % modes.Length;
        foreach (var s in bindings.Surfaces)
        {
            s.Profile.Opacity = mode != 0 && (s.Entry.Renderer.name == "Jacket" || s.Entry.Renderer.name == "Shirt");
            s.Binding.State.Clear();
            if (mode == 2) Graphics.Blit(partialMask, s.Binding.State.Mask);
            if (mode == 3 || mode == 5) Graphics.Blit(Texture2D.whiteTexture, s.Binding.State.Mask);
        }
        loop.StressTest = scenario >= modes.Length; loop.StressIntensity = 1; loop.ApplyPose(0);
        driver.Drawn = false; driver.Drying = false; driver.Tick(0, Center(), false, true);
    }
    private void LateUpdate()
    {
        if (finished || driver == null) return;
        try
        {
            if (frame > WarmupFrames)
            {
                var row = rows[written++]; row.scenario = scenario; row.frame = previousFrame;
                row.pathCount = bindings.PathCount; row.deposits = previousDeposits; row.geometryRefresh = previousBakes;
                row.firing = driver.Gun.Firing; row.elapsedMs = Time.unscaledDeltaTime * 1000;
                for (int i = 0; i < names.Length; i++) if (recorders.TryGetValue(names[i], out var recorder)) row.metrics[i] = recorder.LastValue;
            }
            if (++frame > WarmupFrames + SampleFrames)
            {
                frame = 1;
                if (++scenario == modes.Length * 2) { Finish(); return; }
                Prepare();
            }
            loop.ApplyPose(scenario >= modes.Length ? Mathf.Repeat(frame * 4 / 72f, 1) : 0);
            int mode = scenario % modes.Length;
            driver.Drawn = mode == 4; driver.Drying = mode == 5;
            int deposits = driver.Deposits;
            for (int i = 0; i < geometries.Length; i++) refreshCounts[i] = geometries[i].RefreshCount;
            driver.Tick(1f / 60, Center(), mode == 4 && frame > 1, true);
            previousDeposits = driver.Deposits - deposits; previousBakes = 0;
            for (int i = 0; i < geometries.Length; i++) previousBakes += geometries[i].RefreshCount - refreshCounts[i];
            previousFrame = Time.frameCount;
            metadata[0] = scenario; metadata[1] = Time.frameCount; metadata[2] = frame > WarmupFrames ? 1 : 0;
            Profiler.EmitFrameMetaData(MetadataId, 1, metadata);
            profileCamera.Render();
        }
        catch (Exception e) { Fail(e); }
    }
    private void Finish()
    {
        var report = new {
            date = "2026-10-04", version = "WaterGun 0.6.5", character = CharacterPrefab.name,
            unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(), graphicsJobsSetting = GraphicsJobsBuild,
            actualThreadingMode = SystemInfo.renderingThreadingMode.ToString(), screen = new { Screen.width, Screen.height },
            gpuProfilerEnabled = gpuCapture,
            developmentBuild = Debug.isDebugBuild, quality = QualitySettings.GetQualityLevel(),
            renderingPath = profileCamera.actualRenderingPath.ToString(), shadows = QualitySettings.shadows.ToString(),
            cameraTarget = "1200x600 ARGBHalf depth24; explicit Camera.Render; presentation excluded",
            SampleFrames, WarmupFrames, fixedSimulationDelta = 1f / 60, stressClock = "frame*4/72; not wall clock",
            surfaceCount = bindings.Surfaces.Count, geometryCount = geometries.Length,
            meshes = geometries.Select(g => new { vertices = g.Mesh.vertexCount, indices = Enumerable.Range(0, g.Mesh.subMeshCount).Sum(s => (long)g.Mesh.GetIndexCount(s)) }).ToArray(),
            modes, metricNames = names, availability = recorders.ToDictionary(p => p.Key, p => p.Value.Valid),
            units = "duration ns; GC bytes; render statistics counts; elapsedMs ms", rows
        };
        File.WriteAllText(Path.Combine(output, "samples.json"), Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        finished = true; Cleanup(); Application.Quit(0);
    }
    private void Fail(Exception exception)
    {
        Debug.LogException(exception);
        if (output != null) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "failure.txt"), exception.ToString()); }
        finished = true; Cleanup(); Application.Quit(1);
    }
    private void Cleanup()
    {
        Profiler.enabled = false; Profiler.enableBinaryLog = false;
        foreach (var recorder in recorders.Values) recorder.Dispose(); recorders.Clear();
        if (driver != null) driver.Bindings = null;
        bindings?.Dispose(); bindings = null;
        if (cameraTarget != null) { cameraTarget.Release(); Destroy(cameraTarget); cameraTarget = null; }
    }
    private void OnDestroy() { if (!finished) Cleanup(); }
}
