using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Editor-only measurement tools. Experiments use temporary objects and restore on exit.
[McpForUnityTool("funguy_performance_audit", Description = "Measure FunGuy performance and inspect scene costs. Rendering experiments are temporary and Play-mode-only. Use restore to abort and restore simulation. Other actions support Profiler captures and Android USB analysis.")]
public static class FunGuyPerformanceAudit
{
    public class Parameters
    {
        [ToolParameter("inventory, begin, status, analyze, restore, freeze_world, restore_world, prepare_view, restore_view, connections, connect_android, capture_remote, save_remote, analyze_android_threads, save_project")] public string action { get; set; }
        [ToolParameter("baseline, no_debug, short_shadows, or no_post", Required=false, DefaultValue="baseline")] public string variant { get; set; }
        [ToolParameter("Number of rendered frames to measure", Required=false, DefaultValue="300")] public int frames { get; set; }
    }
    sealed class Counter : IDisposable
    {
        public string name, unit;
        public ProfilerRecorder recorder;
        public readonly List<double> values = new();
        public void Dispose() => recorder.Dispose();
    }
    sealed class Marker
    {
        public string name;
        public double totalMs, selfMs, maxMs;
        public int calls;
    }
    static readonly List<Counter> counters = new();
    static readonly List<double> frameMs = new();
    static readonly Dictionary<Behaviour, bool> oldDebug = new();
    static readonly Dictionary<Camera, bool> oldPost = new();
    static RenderPipelineAsset oldPipeline;
    static UniversalRenderPipelineAsset temporaryPipeline;
    static bool running, ownsRecording;
    static EditorWindow gameWindow;
    static bool wasMaximized;
    static int targetFrames, warmup, lastFrame, firstFrame;
    static string variant, resultPath;
    static double started;
    static float? originalTimeScale;

    static FunGuyPerformanceAudit()
    {
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) { Restore(); RestoreWorld(); } };
        AssemblyReloadEvents.beforeAssemblyReload += () => { Restore(); RestoreWorld(); };
    }

    public static object HandleCommand(JObject args)
    {
        switch ((string)args["action"])
        {
            case "inventory": return Inventory();
            case "begin": return Begin((string)args["variant"] ?? "baseline", (int?)args["frames"] ?? 300);
            case "status": return new { running, variant, capturedFrames=frameMs.Count, targetFrames, unityFrame=Time.frameCount, resultPath };
            case "analyze": return Analyze(120);
            case "restore": Restore(); RestoreWorld(); return new { restored=true };
            case "freeze_world": originalTimeScale ??= Time.timeScale; Time.timeScale=0; return new { frozen=true };
            case "restore_world": RestoreWorld(); return new { restored=true };
            case "prepare_view":
                gameWindow=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                wasMaximized=gameWindow.maximized; SessionState.SetBool("FunGuy.Audit.wasMaximized",wasMaximized);
                gameWindow.maximized=true; gameWindow.Focus();
                foreach(var w in Resources.FindObjectsOfTypeAll<EditorWindow>())if(w.GetType().Name=="ProfilerWindow")w.Close();
                return new { prepared=true };
            case "restore_view":
                gameWindow=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                gameWindow.maximized=SessionState.GetBool("FunGuy.Audit.wasMaximized",false);
                EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler"); return new { restored=true };
            case "connections":
                return new { current=ProfilerDriver.connectedProfiler, connections=ProfilerDriver.GetAvailableProfilers().Select(id=>new {id,name=ProfilerDriver.GetConnectionIdentifier(id)}).ToArray() };
            case "connect_android": ProfilerDriver.DirectIPConnect("127.0.0.1:34999"); return new { connecting=true };
            case "capture_remote": ProfilerDriver.ClearAllFrames(); UnityEngine.Profiling.Profiler.enabled=true; return new { recording=true };
            case "save_remote":
                UnityEngine.Profiling.Profiler.enabled=false; variant="android";
                var analysis=Analyze(300); ProfilerDriver.SaveProfile(".utmp/profiling/android/device-cpu.raw"); return analysis;
            case "analyze_android_threads":
                ProfilerDriver.LoadProfile(".utmp/profiling/android/device-cpu.raw",false); return AnalyzeThreads();
            case "save_project": AssetDatabase.SaveAssets(); return new { saved=true };
            default: throw new ArgumentException("Unknown audit action");
        }
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static Mesh MeshOf(Renderer r)
    {
        if(r is SkinnedMeshRenderer skinned)return skinned.sharedMesh;
        var filter=r.GetComponent<MeshFilter>(); return filter!=null ? filter.sharedMesh : null;
    }
    static object Inventory()
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        var camera = Camera.main ?? cameras.FirstOrDefault(c=>c.enabled);
        var planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
        var materials = renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
        var meshes = renderers.Select(r => new {r,mesh=MeshOf(r)})
            .Where(x=>x.mesh!=null).Select(x=>new {path=PathOf(x.r.transform),mesh=x.mesh.name,
                triangles=Enumerable.Range(0,x.mesh.subMeshCount).Sum(i=>(long)x.mesh.GetIndexCount(i)/3),
                materials=x.r.sharedMaterials.Length,shadows=x.r.shadowCastingMode.ToString(),
                inCameraFrustum=planes!=null && GeometryUtility.TestPlanesAABB(planes,x.r.bounds),
                distance=camera!=null ? Vector3.Distance(camera.transform.position,x.r.bounds.center):0}).ToArray();
        var result = new {
            playing=EditorApplication.isPlaying, unityFrame=Time.frameCount, targetFrameRate=Application.targetFrameRate,
            vSync=QualitySettings.vSyncCount, quality=QualitySettings.names[QualitySettings.GetQualityLevel()],
            resolution=new {Screen.width,Screen.height}, gpu=SystemInfo.graphicsDeviceName, graphicsApi=SystemInfo.graphicsDeviceType.ToString(),
            rendererCount=renderers.Length, frustumRendererCount=meshes.Count(x=>x.inCameraFrustum), materialCount=materials.Length,
            totalActiveMeshTriangles=meshes.Sum(x=>x.triangles), frustumMeshTriangles=meshes.Where(x=>x.inCameraFrustum).Sum(x=>x.triangles),
            lodGroups=Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Length,
            shadowCasters=renderers.Count(r=>r.shadowCastingMode!=ShadowCastingMode.Off),
            colliders=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length,
            lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(l=>new {path=PathOf(l.transform),l.enabled,
                active=l.gameObject.activeInHierarchy,type=l.type.ToString(),shadows=l.shadows.ToString(),l.intensity,l.range,l.shadowStrength,
                lightmapBakeType=l.lightmapBakeType.ToString()}).ToArray(),
            cameras=cameras.Select(c=>new {path=PathOf(c.transform),c.enabled,c.farClipPlane,c.nearClipPlane,c.fieldOfView,c.allowHDR,c.allowMSAA,
                post=c.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderPostProcessing}).ToArray(),
            largestMeshes=meshes.OrderByDescending(x=>x.triangles).Take(25).ToArray(),
            materialSummary=materials.Select(m=>new {m.name,shader=m.shader!=null ? m.shader.name : "<missing>",m.enableInstancing,m.renderQueue,passes=m.passCount}).ToArray(),
            volumes=Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Select(v=>new {path=PathOf(v.transform),v.enabled,v.weight,
                v.priority,profile=v.sharedProfile!=null ? v.sharedProfile.name : null,
                effects=v.sharedProfile!=null ? v.sharedProfile.components.Select(c=>new {type=c.GetType().Name,c.active,
                    isActive=c is IPostProcessComponent pp && pp.IsActive()}).ToArray():null}).ToArray()
        };
        Save(".utmp/profiling/inventory-"+(EditorApplication.isPlaying?"play":"edit")+".json",result);
        return result;
    }

    static object Begin(string requestedVariant, int frames)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first");
        if(requestedVariant!="baseline" && requestedVariant!="no_debug" && requestedVariant!="short_shadows" && requestedVariant!="no_post")
            throw new ArgumentException("Unknown variant");
        Restore(); variant=requestedVariant; targetFrames=Mathf.Clamp(frames,30,1800); warmup=40; lastFrame=-1;
        ProfilerDriver.ClearAllFrames();
        Directory.CreateDirectory(".utmp/profiling");
        UnityEngine.Profiling.Profiler.logFile=".utmp/profiling/capture-"+variant+".raw";
        UnityEngine.Profiling.Profiler.enableBinaryLog=true;
        UnityEngine.Profiling.Profiler.enabled=true; ownsRecording=true;
        frameMs.Clear(); counters.Clear(); resultPath=null;
        if (variant=="no_debug")
        {
            foreach(var a in Object.FindObjectsByType<RunnerLandingAssist>(FindObjectsSortMode.None)) {
                // Disable only the debug preview, never assistance or gameplay.
                var field=typeof(RunnerLandingAssist).GetField("drawDebugGizmos",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                oldDebug[a]= (bool)field.GetValue(a); field.SetValue(a,false);
            }
        }
        else if(variant=="short_shadows")
        {
            oldPipeline=QualitySettings.renderPipeline;
            temporaryPipeline=Object.Instantiate(GraphicsSettings.currentRenderPipeline) as UniversalRenderPipelineAsset;
            temporaryPipeline.hideFlags=HideFlags.HideAndDontSave;
            temporaryPipeline.shadowDistance=40f;
            QualitySettings.renderPipeline=temporaryPipeline;
        }
        else if(variant=="no_post")
        {
            foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) {
                if(!c.TryGetComponent<UniversalAdditionalCameraData>(out var data))continue;
                oldPost[c]=data.renderPostProcessing; data.renderPostProcessing=false;
            }
        }
        else if(variant!="baseline") throw new ArgumentException("Unknown variant");
        var handles=new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
        var requested=new HashSet<string>{"Main Thread","Render Thread","GC Allocated In Frame","GC Used Memory","Total Used Memory",
            "Draw Calls Count","Batches Count","SetPass Calls Count","Triangles Count","Vertices Count","Shadow Casters Count",
            "Instanced Batches Count","Physics.Simulate","Funguy.LandingAssist.Search","Funguy.LandingAssist.Correction"};
        foreach(var h in handles) {
            var d=ProfilerRecorderHandle.GetDescription(h); if(!requested.Contains(d.Name))continue;
            var recorder=ProfilerRecorder.StartNew(d.Category,d.Name,1);
            if(recorder.Valid)counters.Add(new Counter{name=d.Name,unit=d.UnitType.ToString(),recorder=recorder}); else recorder.Dispose();
        }
        started=EditorApplication.timeSinceStartup; firstFrame=Time.frameCount; running=true;
        EditorApplication.update+=Tick;
        return new {started=true,variant,targetFrames,counters=counters.Select(c=>c.name).ToArray()};
    }
    static void Tick()
    {
        if(!running || !EditorApplication.isPlaying){Restore();return;}
        if(Time.frameCount==lastFrame)return; lastFrame=Time.frameCount;
        if(warmup-->0)return;
        frameMs.Add(Time.unscaledDeltaTime*1000.0);
        foreach(var c in counters)c.values.Add(c.recorder.LastValue);
        if(frameMs.Count<targetFrames && EditorApplication.timeSinceStartup-started<90)return;
        resultPath=".utmp/profiling/sample-"+variant+".json";
        Save(resultPath,new {variant,source="Unity Editor Play mode",simulationFrozen=originalTimeScale.HasValue, resolution=new {Screen.width,Screen.height},firstFrame,lastFrame,elapsed=EditorApplication.timeSinceStartup-started,
            frameTimeMs=Summary(frameMs),counters=counters.Select(c=>new {c.name,c.unit,stats=Summary(c.values)}).ToArray()});
        running=false; EditorApplication.update-=Tick;
        foreach(var c in counters)c.Dispose(); counters.Clear();
        UnityEngine.Profiling.Profiler.enableBinaryLog=false;
        UnityEngine.Profiling.Profiler.enabled=false; ownsRecording=false;
        try {
            ProfilerDriver.LoadProfile(".utmp/profiling/capture-"+variant+".raw",false);
            Analyze(Math.Min(targetFrames,120));
        } finally { RestoreVariant(); }
    }
    static object Summary(List<double> values)
    {
        if(values.Count==0)return new {count=0};
        var ordered=values.OrderBy(v=>v).ToArray();
        return new {count=ordered.Length,mean=values.Average(),median=ordered[ordered.Length/2],
            p95=ordered[(int)((ordered.Length-1)*0.95)],max=ordered.Last(),min=ordered.First(),nonZero=values.Count(v=>v!=0)};
    }
    static object Analyze(int frames)
    {
        int last=ProfilerDriver.lastFrameIndex, first=Math.Max(ProfilerDriver.firstFrameIndex,last-frames+1),count=0;
        var markers=new Dictionary<string,Marker>(); var times=new List<double>(); var gpu=new List<double>();
        var threadNames=new HashSet<string>();
        for(int f=first;f<=last;f++) {
            using(var view=ProfilerDriver.GetRawFrameDataView(f,0)) {
                if(!view.valid)continue; count++; times.Add(view.frameTimeMs); if(view.frameGpuTimeMs>0)gpu.Add(view.frameGpuTimeMs);
                threadNames.Add(view.threadName);
                for(int i=0;i<view.sampleCount;i++) {
                    string name=view.GetSampleName(i); double total=view.GetSampleTimeMs(i),self=total;
                    int child=i+1;
                    for(int n=0;n<view.GetSampleChildrenCount(i);n++) { self-=view.GetSampleTimeMs(child); child+=1+view.GetSampleChildrenCountRecursive(child); }
                    if(!markers.TryGetValue(name,out var m))markers[name]=m=new Marker{name=name};
                    m.totalMs+=total; m.selfMs+=Math.Max(0,self); m.maxMs=Math.Max(m.maxMs,total); m.calls++;
                }
            }
        }
        var result=new {first,last,frames=count,threads=threadNames.ToArray(),frameTimeMs=Summary(times),gpuTimeMs=Summary(gpu),
            inclusive=markers.Values.OrderByDescending(m=>m.totalMs).Take(65).Select(m=>new {m.name,meanMs=m.totalMs/Math.Max(1,count),m.maxMs,m.calls}).ToArray(),
            self=markers.Values.OrderByDescending(m=>m.selfMs).Take(65).Select(m=>new {m.name,meanSelfMs=m.selfMs/Math.Max(1,count),m.calls}).ToArray()};
        Save(".utmp/profiling/cpu-"+(variant??"capture")+".json",result); return result;
    }
    static object AnalyzeThreads()
    {
        int last=ProfilerDriver.lastFrameIndex,first=Math.Max(ProfilerDriver.firstFrameIndex,last-119);
        var threads=new List<object>();
        for(int t=0;t<128;t++) {
            using var probe=ProfilerDriver.GetRawFrameDataView(last,t);
            if(!probe.valid)break;
            var markers=new Dictionary<string,Marker>(); int count=0;
            for(int f=first;f<=last;f++) {
                using var v=ProfilerDriver.GetRawFrameDataView(f,t); if(!v.valid)continue; count++;
                for(int i=0;i<v.sampleCount;i++) {
                    var name=v.GetSampleName(i); if(string.IsNullOrEmpty(name))continue; var ms=v.GetSampleTimeMs(i);
                    if(!markers.TryGetValue(name,out var m))markers[name]=m=new Marker{name=name};
                    m.totalMs+=ms;m.maxMs=Math.Max(m.maxMs,ms);m.calls++;
                }
            }
            threads.Add(new {index=t,name=probe.threadName,frames=count,markers=markers.Values.OrderByDescending(m=>m.totalMs).Take(16).Select(m=>new {m.name,meanMs=m.totalMs/Math.Max(1,count),m.maxMs,m.calls}).ToArray()});
        }
        Save(".utmp/profiling/android/threads.json",threads);return threads;
    }
    static void RestoreVariant()
    {
        foreach(var pair in oldDebug)if(pair.Key!=null)typeof(RunnerLandingAssist).GetField("drawDebugGizmos",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(pair.Key,pair.Value);
        oldDebug.Clear();
        foreach(var pair in oldPost)if(pair.Key!=null && pair.Key.TryGetComponent<UniversalAdditionalCameraData>(out var d))d.renderPostProcessing=pair.Value;
        oldPost.Clear();
        if(temporaryPipeline!=null){QualitySettings.renderPipeline=oldPipeline;Object.DestroyImmediate(temporaryPipeline);temporaryPipeline=null;}
    }
    static void Restore()
    {
        running=false;EditorApplication.update-=Tick;
        if(ownsRecording){UnityEngine.Profiling.Profiler.enableBinaryLog=false;UnityEngine.Profiling.Profiler.enabled=false;ownsRecording=false;}
        foreach(var c in counters)c.Dispose();counters.Clear();RestoreVariant();
    }
    static void RestoreWorld(){if(originalTimeScale.HasValue){Time.timeScale=originalTimeScale.Value;originalTimeScale=null;}}
    static void Save(string path,object data){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonConvert.SerializeObject(data,Formatting.Indented));}
}
