using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class RunnerBalanceWindow : EditorWindow
{
    [SerializeField] MomentumSystem momentum;
    [SerializeField] DistanceScoreManager scoring;
    [SerializeField] RunnerMovementMotor motor;
    [SerializeField] GameplayManager manager;
    [SerializeField] MovementTuningProfile profile;
    [SerializeField] List<GameObject> prefabs=new();
    [SerializeField] int representative;
    [SerializeField] RunnerBalanceDraft draft=new();
    MovementTuningProfile previewProfile;
    RunnerBalanceSimulation simulation;
    readonly RunnerBalanceTrace[] traces=new RunnerBalanceTrace[3];
    RunnerBalanceMixed mixed;
    System.Random random;
    RunnerBalanceFitter fitter;
    IEnumerator fitting;
    Vector2 leftScroll,rightScroll;
    int graph,dragBoundary=-1;
    int balanceGraph;
    float[,] netChange=new float[4,101];
    float sustainTime;
    [SerializeField] float survivalTarget=-1;
    double survivalFitAt=double.PositiveInfinity;
    RunnerSurvivalFitter survivalFitter;
    IEnumerator survivalFit;
    string survivalReport;
    int badToDrop;
    [SerializeField] float diagnosticSpeed=45;
    bool timeAxis,showDiff=true,loaded;
    [SerializeField] bool[] visibleLines={true,true,true,true};
    [SerializeField] bool[] visibleDecay={true,true,true,true};
    [SerializeField] int settingsPage,previewPage;
    bool advancedSettings;
    static readonly string[] Qualities={"Bad","Good","Perfect","Average mix"};
    GUIStyle PlotLabel => new GUIStyle(EditorStyles.miniLabel) { normal={textColor=new Color(.88f,.9f,.94f)} };
    GUIStyle TooltipLabel => new GUIStyle(EditorStyles.label) { fontSize=12, normal={textColor=Color.white}, padding=new RectOffset(10,10,8,8) };
    double mixedStart;
    string status="",fingerprint="",previewError;
    static readonly Color[] Colors={new(0.95f,0.32f,0.18f),new(0.2f,0.65f,1f),new(1f,0.78f,0.16f),new(0.76f,0.48f,1f)};
    static readonly string[] Names={"Bad (solid)","Good (dashed)","Perfect (solid)","Average mix (dotted)"};

    [MenuItem("Tools/Funguy/Runner Balance Lab")]
    public static void Open() {var w=GetWindow<RunnerBalanceWindow>();w.titleContent=new GUIContent("Runner Balance Lab");w.minSize=new Vector2(1050,720);}
    void OnEnable()
    {
        wantsMouseMove=true;
        draft=RunnerBalanceLabStorage.instance.current.Copy();
        momentum=momentum!=null?momentum:FindFirstObjectByType<MomentumSystem>();
        scoring=scoring!=null?scoring:FindFirstObjectByType<DistanceScoreManager>();
        motor=motor!=null?motor:FindFirstObjectByType<RunnerMovementMotor>();
        manager=manager!=null?manager:FindFirstObjectByType<GameplayManager>();
        if(profile==null && manager!=null) profile=RunnerBalanceBindings.Reference<MovementTuningProfile>(manager,"defaultMovementTuningProfile");
        if(profile==null && motor!=null) profile=motor.TuningProfile;
        if(prefabs.Count==0) {var p=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Funguy.MushroomRunner/Prefabs/MushroomLauncher.prefab");if(p!=null) prefabs.Add(p);}
        EditorApplication.update+=UpdateJobs;Undo.undoRedoPerformed+=OnUndo;
        CaptureTargets();Rebuild();
    }
    void OnDisable()
    {
        EditorApplication.update-=UpdateJobs;Undo.undoRedoPerformed-=OnUndo;StopJobs();
        RunnerBalanceLabStorage.instance.Store(draft);
        if(previewProfile!=null) DestroyImmediate(previewProfile);
    }
    void OnUndo() {CaptureTargets();status="Undo/Redo updated the targets. Reload to inspect their current values; your draft is unchanged.";Rebuild();Repaint();}
    void StopJobs() {survivalFit=null;survivalFitter=null;survivalFitAt=double.PositiveInfinity;fitting=null;fitter=null;mixedStart=double.PositiveInfinity;}
    MushroomLauncher Representative => prefabs.Count>0 && representative>=0 && representative<prefabs.Count && prefabs[representative]!=null
        ? prefabs[representative].GetComponentInChildren<MushroomLauncher>(true):null;
    string TargetError()
    {
        if(momentum==null || scoring==null || motor==null || manager==null || profile==null) return "Select the scene momentum, scoring, motor, gameplay manager, and a movement profile.";
        if(!EditorUtility.IsPersistent(profile)) return "Choose a saved movement profile asset.";
        foreach(Component c in new Component[]{momentum,scoring,motor,manager})
            if(EditorUtility.IsPersistent(c)||!c.gameObject.scene.IsValid()||c.gameObject.scene!=momentum.gameObject.scene) return "Select components from the same loaded scene, not prefab assets.";
        if(prefabs.Count==0 || Representative==null) return "Select at least one mushroom prefab and a representative launcher.";
        var paths=new HashSet<string>();
        foreach(var p in prefabs)
        {
            if(p==null || !PrefabUtility.IsPartOfPrefabAsset(p)) return "Targets must be saved mushroom prefab assets.";
            string path=AssetDatabase.GetAssetPath(p);if(!paths.Add(path)) return "Remove duplicate prefab targets.";
            var launchers=p.GetComponentsInChildren<MushroomLauncher>(true);
            if(launchers.Length!=1 || AssetDatabase.GetAssetPath(launchers[0])!=path) return "Each target must own exactly one MushroomLauncher; nested external launchers are not edited.";
            if(launchers[0].ConfigurationWarning!=null) return p.name+": "+launchers[0].ConfigurationWarning;
        }
        return null;
    }
    string Fingerprint()
    {
        if(TargetError()!=null) return "";
        return string.Join("|",new Object[]{momentum,scoring,motor,manager,profile}.Concat(prefabs.Cast<Object>()).Select(o=>GlobalObjectId.GetGlobalObjectIdSlow(o)+EditorJsonUtility.ToJson(o)))
            +string.Join("|",prefabs.Select(p=>EditorJsonUtility.ToJson(p.GetComponentInChildren<MushroomLauncher>(true))+RunnerBalanceBindings.Arrow(p.GetComponentInChildren<MushroomLauncher>(true)).ToString("R")));
    }
    void CaptureTargets() {fingerprint=Fingerprint();loaded=true;}
    void Changed(bool custom=true)
    {
        if(custom) draft.preset="Custom";
        RunnerBalanceLabStorage.instance.Store(draft);status="Draft changed. Preview only - targets have not been edited.";Rebuild();
    }
    void Rebuild()
    {
        StopJobs();survivalReport=null;previewError=null;for(int i=0;i<3;i++) traces[i]=null;mixed=null;simulation=null;
        if(previewProfile!=null) DestroyImmediate(previewProfile);
        string error=TargetError()??draft.Validate();if(error!=null) {previewError=error;return;}
        try
        {
            previewProfile=Instantiate(profile);previewProfile.hideFlags=HideFlags.HideAndDontSave;
            simulation=new RunnerBalanceSimulation(draft.Copy(),previewProfile,RunnerBalanceBindings.Arrow(Representative));
            for(int i=0;i<3;i++) traces[i]=simulation.Run(i);
            RefreshDiagnostics();
            mixed=new RunnerBalanceMixed();random=new System.Random(draft.seed);mixedStart=EditorApplication.timeSinceStartup+0.25;
        }
        catch(Exception e) {previewError=e.Message;simulation=null;}
    }
    void UpdateJobs()
    {
        double end=EditorApplication.timeSinceStartup+0.008;
        try
        {
            if(EditorApplication.timeSinceStartup>=survivalFitAt && simulation!=null)
            {
                survivalFitAt=double.PositiveInfinity;
                survivalFitter=new RunnerSurvivalFitter(draft,previewProfile,RunnerBalanceBindings.Arrow(Representative),diagnosticSpeed,survivalTarget);
                survivalFit=survivalFitter.Fit();
            }
            if(survivalFit!=null)
            {
                while(EditorApplication.timeSinceStartup<end)
                {
                    if(!survivalFit.MoveNext())
                    {
                        var result=survivalFitter.Result;string report=survivalFitter.Report;
                        // An infeasible result is diagnostic only; never silently apply an approximation.
                        bool met=survivalFitter.Met;
                        if(met){draft=result;RunnerBalanceLabStorage.instance.Store(draft);}
                        Rebuild();survivalReport=report;status=report;break;
                    }
                }
                Repaint();return;
            }
            if(fitting!=null)
            {
                while(EditorApplication.timeSinceStartup<end)
                {
                    if(!fitting.MoveNext())
                    {
                        var result=fitter.Result;string report=fitter.Report;
                        fitting=null;draft=result;RunnerBalanceLabStorage.instance.Store(draft);Rebuild();status=report;break;
                    }
                }
                Repaint();return;
            }
            if(simulation==null || mixed==null || mixed.completed>=draft.trials || EditorApplication.timeSinceStartup<mixedStart) return;
            do {mixed.Add(simulation.Run(0,random,false));} while(mixed.completed<draft.trials && EditorApplication.timeSinceStartup<end);
            Repaint();
        }
        catch(Exception e) {previewError=e.Message;StopJobs();Repaint();}
    }
    void OnGUI()
    {
        GUILayout.Space(8);GUILayout.Label("RUNNER BALANCE LAB",EditorStyles.boldLabel);
        GUILayout.Label("Tune a draft, compare the preview, then review and apply. Scene values change only on Apply.",EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if(GUILayout.Button("Reload selected targets",EditorStyles.toolbarButton))
        {
            string error=TargetError();if(error!=null) status=error;
            else {draft=RunnerBalanceBindings.Load(momentum,scoring,profile,Representative);CaptureTargets();Changed();status="Loaded authored values. New tier decay is previewed but is not enabled on the targets until Apply.";}
        }
        using(new EditorGUI.DisabledScope(simulation==null || fitting!=null || survivalFit!=null || survivalFitAt<double.PositiveInfinity || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("Auto-Balance",EditorStyles.toolbarButton))
            {
                mixedStart=double.PositiveInfinity;fitter=new RunnerBalanceFitter(draft,previewProfile,RunnerBalanceBindings.Arrow(Representative));fitting=fitter.Fit();
                status="Fitting Add then Multiply proposals. Existing rule lists remain unchanged on disk until Apply.";
            }
        }
        if(GUILayout.Button("Cancel calculations",EditorStyles.toolbarButton)) {StopJobs();status="Calculations cancelled; the current draft is unchanged.";}
        if(GUILayout.Button("Refresh preview",EditorStyles.toolbarButton)) Rebuild();
        GUILayout.FlexibleSpace();GUILayout.Label(survivalFit!=null || survivalFitAt<double.PositiveInfinity?"Fitting survival target...":fitting!=null?$"Fitting: {fitter.Evaluations} evaluations":mixed!=null?$"Mixed runs: {mixed.completed}/{draft.trials}":"Preview unavailable",EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(370));
        settingsPage=GUILayout.Toolbar(settingsPage,new[]{"Tune","Targets"},GUILayout.Height(28));
        leftScroll=EditorGUILayout.BeginScrollView(leftScroll);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if(settingsPage==0) SettingsGUI();else TargetsGUI();
        EditorGUILayout.EndVertical();EditorGUILayout.EndScrollView();EditorGUILayout.EndVertical();
        EditorGUILayout.BeginVertical();rightScroll=EditorGUILayout.BeginScrollView(rightScroll);
        if(!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status,MessageType.Info);
        if(previewError!=null) EditorGUILayout.HelpBox(previewError,MessageType.Warning);
        SceneStatusGUI();
        previewPage=GUILayout.Toolbar(previewPage,new[]{"Progression","Difficulty & sustain","Review & apply"},GUILayout.Height(30));
        GUILayout.Space(8);
        if(previewPage==2) ApplyGUI();
        else if(simulation!=null)
        {
            if(previewPage==0)
            {
                graph=GUILayout.Toolbar(graph,new[]{"Momentum","Forward speed","Earned score"},GUILayout.Height(25));
                GUILayout.Space(6);
                GUILayout.Label(graph==0?"How quickly does each landing quality climb the tiers?":graph==1?"Forward speed after launches and movement losses":"Total earned score - never removed by decay",EditorStyles.boldLabel);
                timeAxis=EditorGUILayout.ToggleLeft("Show time in seconds (otherwise landings)",timeAxis);
                Legend();Graph();
                GUILayout.Label(timeAxis?"Horizontal axis: elapsed seconds":"Horizontal axis: total landings",EditorStyles.centeredGreyMiniLabel);
                ResultsGUI();
            }
            else
            {
                DifficultyGUI();
                GUILayout.Space(12);GUILayout.Label("Expected change per landing + flight",EditorStyles.boldLabel);
                GUILayout.Label("Measured at each band's midpoint, using your landing probabilities.",EditorStyles.wordWrappedMiniLabel);
                for(int t=0;t<4;t++){var drift=simulation.Drift(t);GUILayout.Label($"x{t+1}: momentum {drift.x:+0.###;-0.###;0} points   |   speed {drift.y:P2}");}
            }
        }
        EditorGUILayout.EndScrollView();EditorGUILayout.EndVertical();EditorGUILayout.EndHorizontal();
    }
    void SceneStatusGUI()
    {
        if(momentum==null)return;
        if(!momentum.UsesTierSpeedDecay)
            EditorGUILayout.HelpBox("SCENE: legacy momentum is active. This preview uses tier decay, but gameplay does not yet. Review & apply enables it; save the scene afterward.",MessageType.Warning);
        else GUILayout.Label((momentum.UsesSmoothDifficulty?"SCENE: smooth upkeep + speed penalty enabled":"SCENE: speed-only tier decay enabled")+" | Draft changes require Apply",EditorStyles.wordWrappedMiniLabel);
        if(EditorApplication.isPlaying && motor!=null)
        {
            float speed=Mathf.Max(0,motor.Velocity.z);
            bool ticking=momentum.UsesTierSpeedDecay && momentum.isActiveAndEnabled && momentum.IsRunning && !momentum.IsPaused && Time.timeScale>0;
            float loss=ticking?momentum.LossPerSecond(speed):0;
            if(motor.MomentumSource!=momentum)EditorGUILayout.HelpBox("The selected motor is not bound to this momentum source. Live readings refer to different systems.",MessageType.Warning);
            if(!RunnerBalanceMath.ValidBands(RunnerBalanceMath.Caps(motor.TuningProfile)))EditorGUILayout.HelpBox("The active profile has invalid or equal speed caps. Linked-band decay returns zero until they are strictly increasing.",MessageType.Warning);
            if(!ticking)GUILayout.Label("Tier decay is inactive, paused, or the run has not started.",EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label($"LIVE: x{(int)momentum.CurrentTier+1} | momentum {momentum.CurrentMomentum:0.0} | forward speed {speed:0.0} | tier loss {loss:0.00}/s",EditorStyles.boldLabel);
        }
    }
    void TargetsGUI()
    {
        GUILayout.Label("Scene and asset targets",EditorStyles.boldLabel);EditorGUI.BeginChangeCheck();
        momentum=(MomentumSystem)EditorGUILayout.ObjectField("Momentum",momentum,typeof(MomentumSystem),true);
        scoring=(DistanceScoreManager)EditorGUILayout.ObjectField("Scoring",scoring,typeof(DistanceScoreManager),true);
        motor=(RunnerMovementMotor)EditorGUILayout.ObjectField("Motor",motor,typeof(RunnerMovementMotor),true);
        manager=(GameplayManager)EditorGUILayout.ObjectField("Gameplay manager",manager,typeof(GameplayManager),true);
        profile=(MovementTuningProfile)EditorGUILayout.ObjectField("Movement profile",profile,typeof(MovementTuningProfile),false);
        for(int i=0;i<prefabs.Count;i++)
        {
            EditorGUILayout.BeginHorizontal();prefabs[i]=(GameObject)EditorGUILayout.ObjectField(prefabs[i],typeof(GameObject),false);
            if(GUILayout.Button("X",GUILayout.Width(24))) {prefabs.RemoveAt(i);GUI.changed=true;i--;}
            EditorGUILayout.EndHorizontal();
        }
        if(GUILayout.Button("Add target prefab")) {prefabs.Add(null);GUI.changed=true;}
        representative=Mathf.Clamp(representative,0,Mathf.Max(0,prefabs.Count-1));
        if(prefabs.Count>0) representative=EditorGUILayout.Popup("Preview mushroom",representative,prefabs.Select(p=>p!=null?p.name:"Missing").ToArray());
        if(EditorGUI.EndChangeCheck()) {CaptureTargets();Rebuild();}
        GUILayout.Label("All listed prefabs receive the draft's three rule lists.",EditorStyles.wordWrappedMiniLabel);
    }
    void SettingsGUI()
    {
        GUILayout.Space(8);GUILayout.Label("Balance draft",EditorStyles.boldLabel);
        int preset=Array.IndexOf(new[]{"Negative Sum","Zero Sum","Positive Sum","Custom"},draft.preset);if(preset<0)preset=3;
        int next=EditorGUILayout.Popup("Preset",preset,new[]{"Negative Sum","Zero Sum","Positive Sum","Custom"});
        if(next!=preset) {if(next<3) draft.Preset(next);else draft=RunnerBalanceLabStorage.instance.custom.Copy();Changed(false);}
        EditorGUI.BeginChangeCheck();
        GUILayout.Space(8);GUILayout.Label("Tier speeds and promotion thresholds",EditorStyles.boldLabel);
        for(int i=0;i<4;i++)
        {
            float minimum=i==0?0.1f:draft.caps[i-1]+0.1f;
            float maximum=i==3?Mathf.Max(120,draft.caps.w*1.5f):Mathf.Max(minimum,draft.caps[i+1]-0.1f);
            draft.caps[i]=LinkedSlider($"x{i+1} max speed",draft.caps[i],minimum,maximum);
        }
        for(int i=0;i<3;i++) draft.thresholds[i]=LinkedSlider($"x{i+2} momentum",draft.thresholds[i],i==0?1:draft.thresholds[i-1]+1,i==2?99:draft.thresholds[i+1]-1);
        GUILayout.Space(8);GUILayout.Label("Landing momentum rewards",EditorStyles.boldLabel);
        draft.rewards.x=EditorGUILayout.Slider("Bad momentum",draft.rewards.x,-100,0);
        draft.rewards.y=EditorGUILayout.Slider("Good momentum",draft.rewards.y,0,100);
        draft.rewards.z=LinkedSlider("Perfect momentum",draft.rewards.z,0.1f,100);
        GUILayout.Space(8);GUILayout.Label("Difficulty over momentum",EditorStyles.boldLabel);
        draft.smoothDifficulty=EditorGUILayout.Toggle("Smooth upkeep + speed loss",draft.smoothDifficulty);
        if(draft.smoothDifficulty)
        {
            GUILayout.Label("Upkeep points/second at momentum 0 / 33 / 67 / 100",EditorStyles.wordWrappedMiniLabel);
            for(int i=0;i<4;i++)draft.upkeep[i]=LinkedSlider($"Upkeep at {100*i/3}%",draft.upkeep[i],i==0?0:draft.upkeep[i-1],i==3?20:draft.upkeep[i+1]);
            draft.highPressure=LinkedSlider("High-momentum pressure",draft.highPressure,1,10);
        }
        draft.decay=LinkedSlider("Speed penalty strength",draft.decay,0,20);
        draft.hysteresis=EditorGUILayout.Slider("Demotion margin",draft.hysteresis,0,10);
        draft.transition=EditorGUILayout.Slider("Cap time constant",draft.transition,0,2);
        draft.pointsPerUnit=EditorGUILayout.FloatField("Points / distance unit",draft.pointsPerUnit);
        GUILayout.Label("Perfect-streak difficulty targets",EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        draft.firstTierLandings=EditorGUILayout.Slider("First tier landings",draft.firstTierLandings,1,10);
        draft.difficultyGrowth=EditorGUILayout.Slider("Stage cost growth",draft.difficultyGrowth,1,3);
        if(EditorGUI.EndChangeCheck())draft.UpdateTargets();
        GUILayout.Label("Changing these two controls generates total landing targets. Auto-Balance fits them; they are not guarantees.",EditorStyles.wordWrappedMiniLabel);
        draft.targets.x=EditorGUILayout.IntField("Reach x2 by landing",draft.targets.x);
        draft.targets.y=EditorGUILayout.IntField("Reach x3 by landing",draft.targets.y);
        draft.targets.z=EditorGUILayout.IntField("Reach x4 by landing",draft.targets.z);
        GUILayout.Label($"Additional landings: {draft.targets.x} / {draft.targets.y-draft.targets.x} / {draft.targets.z-draft.targets.y}",EditorStyles.wordWrappedMiniLabel);
        draft.targetMomentumDrift=EditorGUILayout.FloatField("Target momentum drift",draft.targetMomentumDrift);
        draft.targetSpeedDrift=EditorGUILayout.FloatField("Target speed drift (%)",draft.targetSpeedDrift*100)/100;
        GUILayout.Label("Calibration: one landing + flight at x2 midpoint.",EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(6);GUILayout.Label("Preview assumptions",EditorStyles.boldLabel);
        string[] q={"Bad","Good","Perfect"};
        for(int i=0;i<3;i++) {float v=EditorGUILayout.Slider(q[i]+" chance (%)",draft.probabilities[i]*100,0,100)/100;if(!Mathf.Approximately(v,draft.probabilities[i])) draft.Probability(i,v);}
        draft.cadence=EditorGUILayout.FloatField("Seconds / landing",draft.cadence);
        bool priorChanged=GUI.changed;
        advancedSettings=EditorGUILayout.Foldout(advancedSettings,"Advanced simulation settings",true);
        GUI.changed=priorChanged;
        if(advancedSettings)
        {
        draft.initialSpeed=EditorGUILayout.FloatField("Starting forward speed",draft.initialSpeed);
        draft.initialMomentum=EditorGUILayout.FloatField("Starting momentum",draft.initialMomentum);
        draft.input=Vector2.ClampMagnitude(EditorGUILayout.Vector2Field("Assumed joystick input",draft.input),1);
        draft.horizon=EditorGUILayout.IntField("Landing horizon",draft.horizon);draft.trials=EditorGUILayout.IntField("Mixed trials",draft.trials);draft.seed=EditorGUILayout.IntField("Random seed",draft.seed);
        }
        GUILayout.Space(8);GUILayout.Label("Launch speed rules (applied top to bottom)",EditorStyles.boldLabel);
        for(int i=0;i<3;i++) RulesGUI(q[i],draft.Rules(i));
        if(EditorGUI.EndChangeCheck()) Changed();
    }
    static float LinkedSlider(string label,float value,float min,float max)
    {
        // Do not silently repair imported invalid/equal boundaries merely by drawing the window.
        return min>max || value<min || value>max ? EditorGUILayout.FloatField(label,value) : EditorGUILayout.Slider(label,value,min,max);
    }
    void RulesGUI(string label,List<MushroomSpeedRule> rules)
    {
        GUILayout.Label(label,EditorStyles.boldLabel);
        for(int i=0;i<rules.Count;i++)
        {
            EditorGUILayout.BeginHorizontal();var r=rules[i];r.mode=(MushroomSpeedMode)EditorGUILayout.EnumPopup(r.mode,GUILayout.Width(85));r.value=EditorGUILayout.FloatField(r.value);rules[i]=r;
            using(new EditorGUI.DisabledScope(i==0)) if(GUILayout.Button("Up",GUILayout.Width(30))) {var a=rules[i-1];rules[i-1]=r;rules[i]=a;GUI.changed=true;}
            if(GUILayout.Button("X",GUILayout.Width(24))) {rules.RemoveAt(i);i--;GUI.changed=true;}
            EditorGUILayout.EndHorizontal();
        }
        if(GUILayout.Button("+ rule",EditorStyles.miniButton)) {rules.Add(new(MushroomSpeedMode.Add,0));GUI.changed=true;}
    }
    void Legend()
    {
        GUILayout.Space(5);EditorGUILayout.BeginHorizontal();
        for(int i=0;i<4;i++)
        {
            var style=new GUIStyle(EditorStyles.miniButton) {fontStyle=FontStyle.Bold};
            style.normal.textColor=style.onNormal.textColor=Colors[i];
            visibleLines[i]=GUILayout.Toggle(visibleLines[i],new GUIContent(Names[i],"Click to show or hide this curve"),style,GUILayout.Height(26));
        }
        if(GUILayout.Button("All",GUILayout.Width(35)))for(int i=0;i<4;i++)visibleLines[i]=true;
        EditorGUILayout.EndHorizontal();
    }
    float Value(RunnerBalancePoint p) => graph==0?p.momentum:graph==1?p.speed:p.score;
    void Graph()
    {
        Rect outer=GUILayoutUtility.GetRect(100,350,GUILayout.ExpandWidth(true));Rect r=new(outer.x+45,outer.y+12,outer.width-58,outer.height-38);
        EditorGUI.DrawRect(r,new Color(0.09f,0.1f,0.13f));
        var series=new List<RunnerBalancePoint>[4];for(int i=0;i<3;i++)series[i]=traces[i].points;series[3]=mixed?.mean;
        float max=graph==0?100:Mathf.Max(graph==1?draft.caps.w:1,series.Where(s=>s!=null && s.Count>0).SelectMany(s=>s).Max(Value))*1.08f;
        Vector3 Pos(float x,float y) => new(r.x+x/draft.horizon*r.width,r.yMax-Mathf.Clamp01(y/max)*r.height,0);
        if(graph<2)
        {
            float lower=0;
            for(int band=0;band<4;band++)
            {
                float upper=graph==0?(band==3?100:draft.thresholds[band]):draft.caps[band];
                Color color=Colors[band];color.a=0.065f;
                EditorGUI.DrawRect(new Rect(r.x,Pos(0,upper).y,r.width,Pos(0,lower).y-Pos(0,upper).y),color);
                lower=upper;
            }
        }
        Handles.BeginGUI();
        for(int i=0;i<=4;i++) {float v=max*i/4;Handles.color=new Color(1,1,1,0.12f);Handles.DrawLine(Pos(0,v),Pos(draft.horizon,v));GUI.Label(new Rect(outer.x,r.yMax-i*r.height/4-8,43,20),v.ToString("0.#"),PlotLabel);}
        if(graph<2)
        {
            int count=graph==0?3:4;
            for(int i=0;i<count;i++)
            {
                float boundary=graph==0?draft.thresholds[i]:draft.caps[i];float y=Pos(0,boundary).y;
                Handles.color=new Color(0.7f,0.8f,0.9f,0.65f);Handles.DrawDottedLine(Pos(0,boundary),Pos(draft.horizon,boundary),4);
                GUI.Label(new Rect(r.x+4,y-16,220,18),graph==0?$"x{i+2} @ {boundary:0.#}":$"x{i+1} cap {boundary:0.#}",PlotLabel);
                var ev=Event.current;
                if(ev.type==EventType.MouseDown && ev.button==0 && r.Contains(ev.mousePosition) && Mathf.Abs(ev.mousePosition.y-y)<6) {dragBoundary=i;ev.Use();}
            }
            if(dragBoundary>=0 && Event.current.type==EventType.MouseDrag)
            {
                float v=Mathf.Clamp01((r.yMax-Event.current.mousePosition.y)/r.height)*max;int i=dragBoundary;
                if(graph==0)draft.thresholds[i]=Mathf.Clamp(v,i==0?1:draft.thresholds[i-1]+1,i==2?99:draft.thresholds[i+1]-1);
                else draft.caps[i]=Mathf.Clamp(v,i==0?0.1f:draft.caps[i-1]+0.1f,i==3?Mathf.Max(120,max):draft.caps[i+1]-0.1f);
                Event.current.Use();Changed();Repaint();
            }
        }
        if(Event.current.type==EventType.MouseUp) dragBoundary=-1;
        for(int s=0;s<4;s++)
        {
            if(!visibleLines[s] || series[s]==null || series[s].Count<2)continue;
            Handles.color=Colors[s];int stride=Mathf.Max(1,series[s].Count/700);
            Vector3 last=Pos(series[s][0].jumps,Value(series[s][0]));int lastTier=series[s][0].tier;
            for(int i=stride;i<series[s].Count;i+=stride)
            {
                var p=series[s][i];Vector3 point=Pos(p.jumps,Value(p));
                if(s==1||s==3)Handles.DrawDottedLine(last,point,s==1?5:2);else Handles.DrawAAPolyLine(2,last,point);
                if(s<3 && p.tier!=lastTier) {Handles.DrawSolidDisc(point,Vector3.forward,3);lastTier=p.tier;}
                last=point;
            }
        }
        for(int i=0;i<=4;i++)GUI.Label(new Rect(r.x+r.width*i/4-10,r.yMax+3,85,20),(draft.horizon*i/4f*(timeAxis?draft.cadence:1)).ToString("0.#"),PlotLabel);
        if(r.Contains(Event.current.mousePosition))
        {
            float jump=(Event.current.mousePosition.x-r.x)/r.width*draft.horizon;Handles.color=Color.gray;Handles.DrawLine(Pos(jump,0),Pos(jump,max));
            string info=$"{jump:0.0} jumps / {jump*draft.cadence:0.0}s";
            for(int i=0;i<4;i++)if(visibleLines[i] && series[i]!=null && series[i].Count>0){var p=Nearest(series[i],jump);info+=$"\n{Names[i].Split(' ')[0]}: {Value(p):0.##}";}
            Rect tip=new Rect(Mathf.Clamp(Event.current.mousePosition.x+14,r.x,r.xMax-230),Mathf.Clamp(Event.current.mousePosition.y+14,r.y,r.yMax-118),230,118);
            EditorGUI.DrawRect(tip,new Color(.055f,.065f,.09f,1));
            GUI.Label(tip,info,TooltipLabel);
            if(Event.current.type==EventType.MouseMove)Repaint();
        }
        Handles.EndGUI();
    }
    void DecayGraph()
    {
        GUILayout.Label("Loss vs speed at each tier's momentum midpoint",EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        for(int t=0;t<4;t++)visibleDecay[t]=GUILayout.Toggle(visibleDecay[t],$"x{t+1}",EditorStyles.miniButton,GUILayout.Height(25));
        EditorGUILayout.EndHorizontal();
        Rect r=GUILayoutUtility.GetRect(100,240,GUILayout.ExpandWidth(true));EditorGUI.DrawRect(r,new Color(0.09f,0.1f,0.13f));
        float max=Mathf.Max(1,Enumerable.Range(0,4).Max(t=>draft.Loss(BandMidpoint(t),0,(MomentumTier)t)));
        Handles.BeginGUI();
        for(int t=0;t<4;t++)
        {
            if(!visibleDecay[t])continue;
            Handles.color=Colors[t];var points=new Vector3[101];
            for(int i=0;i<=100;i++)points[i]=new Vector3(r.x+r.width*i/100f,r.yMax-draft.Loss(BandMidpoint(t),draft.caps.w*i/100f,(MomentumTier)t)/max*(r.height-18),0);
            Handles.DrawAAPolyLine(2,points);GUI.Label(new Rect(r.x+8+t*130,r.y+3,125,20),$"x{t+1}: {draft.Loss(BandMidpoint(t),0,(MomentumTier)t):0.##}/s at 0",PlotLabel);
        }
        Handles.EndGUI();GUILayout.Label($"Forward speed: 0 to {draft.caps.w:0.#}     Loss: 0 to {max:0.##} momentum/s",PlotLabel);
    }
    static RunnerBalancePoint Nearest(List<RunnerBalancePoint> points,float jump)
    {
        int lo=0,hi=points.Count-1;
        while(lo<hi){int mid=(lo+hi)/2;if(points[mid].jumps<jump)lo=mid+1;else hi=mid;}
        return lo>0 && jump-points[lo-1].jumps<points[lo].jumps-jump?points[lo-1]:points[lo];
    }
    float BandMidpoint(int tier) => ((tier==0?0:draft.thresholds[tier-1])+(tier==3?100:draft.thresholds[tier]))*.5f;
    void RefreshDiagnostics()
    {
        for(int q=0;q<4;q++)for(int m=0;m<=100;m++)netChange[q,m]=simulation.NetMomentum(m,diagnosticSpeed,q);
        sustainTime=simulation.DropTime(diagnosticSpeed);badToDrop=simulation.BadTolerance(diagnosticSpeed);
    }
    void DifficultyGUI()
    {
        EditorGUILayout.HelpBox(draft.smoothDifficulty
            ? "Upkeep always consumes momentum. Low forward speed adds a quadratic penalty, weighted more strongly near 100 momentum. Landing rewards must cover both losses."
            : "Legacy tier model: loss is zero at sufficient speed. Enable Smooth upkeep + speed loss in the draft to preview the new difficulty model.",MessageType.Info);
        EditorGUI.BeginChangeCheck();
        diagnosticSpeed=EditorGUILayout.Slider("Test starting forward speed",diagnosticSpeed,0,draft.caps.w);
        if(EditorGUI.EndChangeCheck()){StopJobs();RefreshDiagnostics();survivalReport=null;mixedStart=EditorApplication.timeSinceStartup+.25;}
        balanceGraph=GUILayout.Toolbar(balanceGraph,new[]{"Net gain / loss","Smooth requirements","Loss vs speed"});
        if(balanceGraph==2)DecayGraph();
        else if(balanceGraph==1)
        {
            if(draft.smoothDifficulty)RequirementsGraph();
            else EditorGUILayout.HelpBox("Enable smooth difficulty to use these curves.",MessageType.Info);
        }
        else
        {
            Legend();
            GUILayout.Label("Net momentum per landing + flight (movement losses included)",EditorStyles.boldLabel);
            Rect outer=GUILayoutUtility.GetRect(100,270,GUILayout.ExpandWidth(true));
            Rect r=new(outer.x+48,outer.y+12,outer.width-65,outer.height-40);
            EditorGUI.DrawRect(r,new Color(.09f,.1f,.13f));
            float extent=1;for(int q=0;q<4;q++)if(visibleLines[q])for(int m=0;m<=100;m++)extent=Mathf.Max(extent,Mathf.Abs(netChange[q,m]));
            Vector3 P(float m,float v)=>new(r.x+r.width*m/100,r.center.y-v/extent*r.height*.5f,0);
            Handles.BeginGUI();
            for(int n=-2;n<=2;n++)
            {
                float y=n*extent/2;Handles.color=n==0?Color.white:new Color(1,1,1,.15f);Handles.DrawLine(P(0,y),P(100,y));
                GUI.Label(new Rect(outer.x,P(0,y).y-8,46,18),y.ToString("+0.#;-0.#;0"),EditorStyles.miniLabel);
            }
            for(int q=0;q<4;q++)if(visibleLines[q])
            {
                Handles.color=Colors[q];for(int m=1;m<=100;m++)
                    if(q==1||q==3)Handles.DrawDottedLine(P(m-1,netChange[q,m-1]),P(m,netChange[q,m]),q==1?5:2);
                    else Handles.DrawAAPolyLine(2,P(m-1,netChange[q,m-1]),P(m,netChange[q,m]));
            }
            for(int t=0;t<3;t++){Handles.color=new Color(1,1,1,.3f);Handles.DrawDottedLine(P(draft.thresholds[t],-extent),P(draft.thresholds[t],extent),4);GUI.Label(new Rect(P(draft.thresholds[t],extent).x+3,r.y,40,18),$"x{t+2}",PlotLabel);}
            for(int m=0;m<=100;m+=25)GUI.Label(new Rect(P(m,0).x-8,r.yMax+3,40,18),m.ToString(),EditorStyles.miniLabel);
            if(r.Contains(Event.current.mousePosition))
            {
                int m=Mathf.Clamp(Mathf.RoundToInt((Event.current.mousePosition.x-r.x)/r.width*100),0,100);
                string text=$"Momentum {m}";for(int q=0;q<4;q++)if(visibleLines[q])text+=$"\n{Qualities[q]}: {netChange[q,m]:+0.00;-0.00;0}";
                Rect tip=new(Mathf.Clamp(Event.current.mousePosition.x+12,r.x,r.xMax-230),r.y+20,230,118);EditorGUI.DrawRect(tip,new Color(.055f,.065f,.09f));GUI.Label(tip,text,TooltipLabel);
                if(Event.current.type==EventType.MouseMove)Repaint();
            }
            Handles.EndGUI();
            GUILayout.Label("Horizontal: starting momentum | Above zero climbs; below zero falls",EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label("Downward zero crossings are possible settling points at this test speed, not full-run predictions.",EditorStyles.wordWrappedMiniLabel);
            for(int q=0;q<4;q++)if(visibleLines[q])
            {
                var crossings=new List<string>();for(int m=1;m<100;m++)if(netChange[q,m-1]>0 && netChange[q,m]<=0)crossings.Add(m.ToString());
                GUILayout.Label(Qualities[q]+": "+(crossings.Count==0?"no interior settling crossing": "settling momentum near "+string.Join(", ",crossings)),EditorStyles.miniLabel);
            }
        }
        GUILayout.Space(10);GUILayout.Label("How long can x4 survive?",EditorStyles.boldLabel);
        GUILayout.Label("Starts at 100 momentum and the test speed. Normal movement and losses continue.",EditorStyles.wordWrappedMiniLabel);
        GUILayout.Label(sustainTime<0?"No landings: still x4 after 120 seconds":$"No landings: drops from x4 after {sustainTime:0.0} seconds");
        using(new EditorGUI.DisabledScope(!draft.smoothDifficulty || fitting!=null || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            float displayed=survivalTarget>0?survivalTarget:(sustainTime>0?sustainTime:10);
            float requested=EditorGUILayout.DelayedFloatField("Target survival (seconds)",displayed);
            bool edited=EditorGUI.EndChangeCheck();
            bool fitAgain=GUILayout.Button("Fit target",GUILayout.Width(85));
            EditorGUILayout.EndHorizontal();
            if(edited||fitAgain)
            {
                if(float.IsFinite(requested) && requested>=.1f && requested<=119)
                {
                    StopJobs();survivalTarget=requested;survivalFitAt=EditorApplication.timeSinceStartup+.3;
                    survivalReport="Fitting draft loss strength to the requested time...";
                }
                else survivalReport="Enter a finite survival time from 0.1 to 119 seconds.";
            }
        }
        GUILayout.Label("Type a time and press Enter or leave the field to fit automatically. Scales upkeep and speed penalty together, preserving their shape. Other balance settings stay fixed.",EditorStyles.wordWrappedMiniLabel);
        if(survivalTarget>0)GUILayout.Label($"Requested {survivalTarget:0.00}s | Current {(sustainTime<0?">120":sustainTime.ToString("0.00"))}s | starting speed {diagnosticSpeed:0.##}",EditorStyles.boldLabel);
        if(!string.IsNullOrEmpty(survivalReport))EditorGUILayout.HelpBox(survivalReport,MessageType.Info);
        GUILayout.Label("This is a one-time draft fit, not a locked constraint. Later edits or Auto-Balance can change the achieved time.",EditorStyles.wordWrappedMiniLabel);
        GUILayout.Label(badToDrop<0?$"Bad streak: still x4 after {draft.horizon} landings":$"Bad streak: drops by landing {badToDrop} (landing + flight interval)");
    }
    void RequirementsGraph()
    {
        GUILayout.Label("Smooth curves across momentum (no jumps at tier boundaries)",EditorStyles.boldLabel);
        foreach(bool speed in new[]{false,true})
        {
            GUILayout.Label(speed?"Required forward speed":"Upkeep: momentum points per second");
            Rect outer=GUILayoutUtility.GetRect(100,125,GUILayout.ExpandWidth(true));Rect r=new(outer.x+45,outer.y+8,outer.width-60,outer.height-30);
            EditorGUI.DrawRect(r,new Color(.09f,.1f,.13f));float max=Mathf.Max(.1f,speed?draft.caps.z:draft.upkeep.w);
            var points=new Vector3[101];for(int m=0;m<=100;m++){float v=RunnerBalanceMath.SmoothCurve(m,speed?new Vector4(0,draft.caps.x,draft.caps.y,draft.caps.z):draft.upkeep);points[m]=new Vector3(r.x+r.width*m/100,r.yMax-v/max*r.height,0);}
            Handles.BeginGUI();Handles.color=speed?Colors[1]:Colors[2];Handles.DrawAAPolyLine(2,points);Handles.EndGUI();
            GUI.Label(new Rect(outer.x,r.y,43,18),max.ToString("0.##"));GUI.Label(new Rect(outer.x,r.yMax-15,43,18),"0");
            GUI.Label(new Rect(r.x,r.yMax,200,18),"0 momentum");GUI.Label(new Rect(r.xMax-100,r.yMax,100,18),"100 momentum");
        }
    }
    void ResultsGUI()
    {
        GUILayout.Space(12);GUILayout.Label("Landings needed to reach each tier",EditorStyles.boldLabel);
        GUILayout.Label("Cells show total landings / additional landings since the previous tier. Lower is easier.",EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Landing quality",GUILayout.Width(145));
        for(int t=0;t<3;t++)GUILayout.Label($"Reach x{t+2}");
        EditorGUILayout.EndHorizontal();
        for(int q=0;q<3;q++)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var style=new GUIStyle(EditorStyles.boldLabel);style.normal.textColor=Colors[q];
            GUILayout.Label(Qualities[q],style,GUILayout.Width(145));
            for(int t=0;t<3;t++)
            {
                int n=traces[q].reached[t],previous=t==0?0:traces[q].reached[t-1];
                GUILayout.Label(n<0?"Not reached":$"{n} total / +{n-previous}");
            }
            EditorGUILayout.EndHorizontal();
        }
        GUILayout.Label($"Not reached = not reached within {draft.horizon} landings. First reach does not mean the tier was sustained.",EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(8);GUILayout.Label("Time spent in each tier (whole run)",EditorStyles.boldLabel);
        for(int q=0;q<4;q++)
        {
            if(q==3 && (mixed==null||mixed.completed==0))continue;
            double total=q==3?mixed.tierSeconds.Sum():traces[q].tierSeconds.Sum();
            string row=Qualities[q]+": ";for(int t=0;t<4;t++){double seconds=q==3?mixed.tierSeconds[t]:traces[q].tierSeconds[t];row+=$"x{t+1} {(total>0?100*seconds/total:0):0.#}%   ";}
            GUILayout.Label(row,EditorStyles.wordWrappedMiniLabel);
        }
        GUILayout.Space(8);GUILayout.Label("Mixed landings: chance of reaching each tier",EditorStyles.boldLabel);
        if(mixed!=null && mixed.completed>0)for(int t=0;t<3;t++)
            GUILayout.Label($"x{t+2}: "+mixed.Summary(t,draft.horizon).Replace("?","-"),EditorStyles.wordWrappedLabel);
        GUILayout.Label("Mean and median include only runs that reached the tier; failed runs are excluded. SD measures variation in landing count.",EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.HelpBox("Preview assumes regular successful landings at the selected cadence. It does not predict collisions or course difficulty. Average mix is an average across runs, not one playable run.",MessageType.None);
        if(!simulation.OrderedRewards())EditorGUILayout.HelpBox("Launch speed must be ordered Perfect >= Good >= Bad across the speed range before Apply.",MessageType.Warning);
    }
    void ApplyGUI()
    {
        GUILayout.Space(10);GUILayout.Label("Review changes before applying",EditorStyles.boldLabel);
        string error=TargetError()??draft.Validate()??previewError;
        if(error!=null){EditorGUILayout.HelpBox(error,MessageType.Warning);return;}
        var edits=RunnerBalanceBindings.Edits(draft,momentum,scoring,motor,manager,profile,prefabs);
        showDiff=EditorGUILayout.Foldout(showDiff,"Before -> After (explicit target list)",true);
        if(showDiff)
        {
            Object last=null;
            foreach(var e in edits)
            {
                if(e.target!=last){GUILayout.Label(e.target.name+" | "+(EditorUtility.IsPersistent(e.target)?AssetDatabase.GetAssetPath(e.target):"Scene: "+((Component)e.target).gameObject.scene.path),EditorStyles.wordWrappedLabel);last=e.target;}
                string before=e.Before(),after=e.After();if(before!=after)GUILayout.Label($"  {ObjectNames.NicifyVariableName(e.field)}: {before} -> {after}",EditorStyles.wordWrappedMiniLabel);
            }
        }
        GUILayout.Label("Apply saves only the selected assets and marks the scene dirty. Normal Unity scene saving and Undo remain available.",EditorStyles.wordWrappedMiniLabel);
        bool ordered=simulation!=null && simulation.OrderedRewards();
        using(new EditorGUI.DisabledScope(!ordered || fitting!=null || survivalFit!=null || survivalFitAt<double.PositiveInfinity || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("Apply reviewed draft",GUILayout.Height(32)))
            {
                if(loaded && fingerprint!=Fingerprint()) {status="Targets changed outside the window. Reload them before applying so external edits can be reviewed.";return;}
                try {RunnerBalanceBindings.Apply(edits);CaptureTargets();status="Applied to selected targets. Assets saved; save the scene normally. Undo restores the complete edit group.";}
                catch(Exception e){status="Apply failed: "+e.Message;}
            }
        }
    }
}
