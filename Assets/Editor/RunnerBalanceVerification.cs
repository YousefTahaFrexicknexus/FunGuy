using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Batch-mode regression checks. Run in an isolated project, never against a live play session.</summary>
public static class RunnerBalanceVerification
{
    static int checks;
    static readonly List<string> reports=new();
    static void Check(bool condition,string message) {if(!condition)throw new Exception(message);checks++;}
    static void Near(float a,float b,string name,float epsilon=0.0005f) => Check(Mathf.Abs(a-b)<=epsilon,name+$" expected {b}, got {a}");
    static void Set(Object target,string field,object value) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(target,value);
    static object Call(Object target,string method,params object[] values) => target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,values);
    static string folder;
    public static void Run()
    {
        if(!Application.isBatchMode) throw new InvalidOperationException("Run these checks in an isolated batch project.");
        string output=Environment.GetEnvironmentVariable("RUNNER_BALANCE_RESULTS");
        if(string.IsNullOrEmpty(output)) output=Path.GetFullPath("../results.txt");
        int code=0;var oldScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        try
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Use batch edit mode for these checks.");
            folder="Assets/BalanceVerification_"+Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
            var profile=ScriptableObject.CreateInstance<MovementTuningProfile>();
            Set(profile,"maxSpeedX1",15f);Set(profile,"maxSpeedX2",30f);Set(profile,"maxSpeedX3",45f);Set(profile,"maxSpeedX4",60f);
            AssetDatabase.CreateAsset(profile,folder+"/Movement.asset");
            var d=new RunnerBalanceDraft();Vector4 caps=d.caps;
            Near(RunnerBalanceMath.Decay(40,MomentumTier.Maximum,caps,4),4f/9,"Gentle deficit");
            Near(RunnerBalanceMath.Decay(30,MomentumTier.Maximum,caps,4),4,"One band behind");
            Near(RunnerBalanceMath.Decay(15,MomentumTier.Maximum,caps,4),16,"Two bands behind");
            Near(RunnerBalanceMath.Decay(45,MomentumTier.Maximum,caps,4),0,"Boundary maintains");
            Near(RunnerBalanceMath.Decay(90,MomentumTier.Maximum,caps,4),0,"Overspeed maintains");
            Near(RunnerBalanceMath.Decay(0,MomentumTier.Low,caps,4),0,"x1 floor");
            Check(!RunnerBalanceMath.ValidBands(Vector4.one),"Equal caps rejected");
            Near(RunnerBalanceMath.BlendCap(15,60,0.35f,0.2f),RunnerBalanceMath.BlendCap(RunnerBalanceMath.BlendCap(15,60,0.35f,0.1f),60,0.35f,0.1f),"Frame independent blend");
            Near(RunnerBalanceMath.BlendCap(15,60,0.35f,0),15,"Paused blend");
            Check(RunnerBalanceMath.ResolveTier(80,MomentumTier.High,d.thresholds,2)==MomentumTier.Maximum,"Promotion");
            Check(RunnerBalanceMath.ResolveTier(79,MomentumTier.Maximum,d.thresholds,2)==MomentumTier.Maximum,"Hysteresis retains tier");
            Check(RunnerBalanceMath.ResolveTier(77.9f,MomentumTier.Maximum,d.thresholds,2)==MomentumTier.High,"Demotion");
            d.Probability(0,0.8f);Near(d.probabilities.x+d.probabilities.y+d.probabilities.z,1,"Normalized mix");Near(d.probabilities.y/d.probabilities.z,2.5f,"Preserved other ratio");
            var invalid=new RunnerBalanceDraft {probabilities=new Vector3(float.NaN,0,1)};
            Check(invalid.Validate()!=null,"Non-finite probabilities rejected");
            Check(RunnerBalanceMath.ResolveTier(19.6f,MomentumTier.Medium,new Vector3(20,21,80),2)==MomentumTier.Medium,"Hysteresis bounded by narrow threshold gap");
            d=new RunnerBalanceDraft();d.bad.Clear();var clone=d.Copy();clone.bad.Add(new(MushroomSpeedMode.Set,1));Check(d.bad.Count==0,"Detached list copy");d=new RunnerBalanceDraft();
            d.smoothDifficulty=false;
            var simulation=new RunnerBalanceSimulation(d,profile,new Vector3(0,1,1));
            var bad=simulation.Run(0);Check(Array.TrueForAll(bad.reached,n=>n<0),"Bad streak unreachable");Check(simulation.OrderedRewards(),"Default rules ordered");
            var mixedA=new RunnerBalanceMixed();var mixedB=new RunnerBalanceMixed();var randomA=new System.Random(1337);var randomB=new System.Random(1337);
            for(int i=0;i<32;i++){mixedA.Add(simulation.Run(0,randomA,false));mixedB.Add(simulation.Run(0,randomB,false));}
            Near(mixedA.mean[100].score,mixedB.mean[100].score,"Seeded mixed score");Near(mixedA.mean[100].momentum,mixedB.mean[100].momentum,"Seeded mixed momentum");
            Check(mixedA.Summary(0,100).Contains("conditional")||mixedA.Summary(0,100).Contains("Not reached"),"Honest conditional statistics");
            for(int i=32;i<2048;i++) mixedA.Add(simulation.Run(0,randomA,false));
            Check(mixedA.completed==2048 && !float.IsNaN(mixedA.mean[100].score),"Full default mixed batch");
            for(int i=1;i<mixedA.mean.Count;i++) Check(mixedA.mean[i].score>=mixedA.mean[i-1].score,"Mixed earned score never decreases");
            for(int preset=0;preset<3;preset++)
            {
                var request=new RunnerBalanceDraft();request.Preset(preset);request.smoothDifficulty=false;request.targets=new Vector3Int(3,7,12);var fitter=new RunnerBalanceFitter(request,profile,new Vector3(0,1,1));
                var iterator=fitter.Fit();while(iterator.MoveNext()){}
                Check(fitter.Result.Validate()==null,"Fitted settings valid");
                Check(fitter.Result.caps==request.caps && fitter.Result.targets==request.targets,"Solver preserves targets and caps");
                Check(new RunnerBalanceSimulation(fitter.Result,profile,new Vector3(0,1,1)).OrderedRewards(),"Fit quality ordering");
                reports.Add(request.preset+": "+fitter.Report);
                Check(fitter.MeetsTargets,"Default preset should fit: "+fitter.Report);
            }
            var smooth=new RunnerBalanceDraft();
            Check(smooth.targets==new Vector3Int(3,9,21),"Exponential stage costs default to 3/6/12");
            float last=-1;
            for(int m=0;m<=100;m++)
            {
                float v=RunnerBalanceMath.SmoothCurve(m,smooth.upkeep);
                Check(v>=last && v>=smooth.upkeep.x && v<=smooth.upkeep.w,"Monotone bounded smooth upkeep");last=v;
            }
            foreach(float boundary in new[]{100f/3,200f/3,smooth.thresholds.x,smooth.thresholds.y,smooth.thresholds.z})
                Near(smooth.Loss(boundary-.0001f,20,MomentumTier.Low),smooth.Loss(boundary+.0001f,20,MomentumTier.Maximum),"Continuous loss across knots and tiers",.001f);
            Near(smooth.Loss(100,60,MomentumTier.Maximum),smooth.upkeep.w,"Sufficient speed still pays upkeep");
            Check(smooth.Loss(100,15,MomentumTier.Maximum)>smooth.Loss(100,30,MomentumTier.Maximum),"Greater deficit costs more");
            Check(smooth.Loss(0,0,MomentumTier.Low)==0,"No starting upkeep trap");
            var smoothSim=new RunnerBalanceSimulation(smooth,profile,new Vector3(0,1,1));
            Check(smoothSim.DropTime(60)>0 && smoothSim.DropTime(60)<120,"x4 eventually drops without landings");
            Check(smoothSim.BadTolerance(60)>0,"Bad streak loses x4");
            var smoothTrace=smoothSim.Run(2);
            float totalSeconds=0;foreach(float seconds in smoothTrace.tierSeconds)totalSeconds+=seconds;
            Near(totalSeconds,smooth.horizon*smooth.cadence,"Tier occupancy covers full run",.05f);
            for(int preset=0;preset<3;preset++)
            {
                var request=new RunnerBalanceDraft();request.Preset(preset);
                var fit=new RunnerBalanceFitter(request,profile,new Vector3(0,1,1));var iterator=fit.Fit();while(iterator.MoveNext()){}
                Check(fit.Result.Validate()==null && fit.Result.targets==request.targets,"Smooth fit preserves valid target request");
                var resultSim=new RunnerBalanceSimulation(fit.Result,profile,new Vector3(0,1,1));var resultTrace=resultSim.Run(2);
                if(fit.MeetsTargets)for(int t=0;t<3;t++)Check(resultTrace.reached[t]==request.targets[t],"Smooth fit reports exact actual crossings");
                else Check(fit.Report.Contains("NOT met"),"Smooth infeasibility remains visible");
                reports.Add("Smooth "+request.preset+": "+fit.Report);
            }
            foreach(float requestedTime in new[]{2f,15f,60f})
            {
                var source=new RunnerBalanceDraft();string original=JsonUtility.ToJson(source);
                var inverse=new RunnerSurvivalFitter(source,profile,new Vector3(0,1,1),45,requestedTime);
                var fit=inverse.Fit();while(fit.MoveNext()){}
                Check(inverse.Met,"Survival fit reaches requested time: "+inverse.Report);
                Near(new RunnerBalanceSimulation(inverse.Result,profile,new Vector3(0,1,1)).DropTime(45),requestedTime,"Survival result matches simulation",.05f);
                Check(JsonUtility.ToJson(source)==original,"Survival fit leaves source draft detached");
                Check(inverse.Result.caps==source.caps && inverse.Result.thresholds==source.thresholds && inverse.Result.rewards==source.rewards,"Survival fit preserves rewards and tier settings");
                Near(inverse.Result.upkeep.w/source.upkeep.w,inverse.Result.decay/source.decay,"Survival preserves relative loss curve strength",.001f);
            }
            var noLoss=new RunnerBalanceDraft{upkeep=Vector4.zero,decay=0};
            var unreachableSurvival=new RunnerSurvivalFitter(noLoss,profile,Vector3.forward,45,10);
            var inverseSteps=unreachableSurvival.Fit();while(inverseSteps.MoveNext()){}
            Check(!unreachableSurvival.Met && unreachableSurvival.Result.upkeep==Vector4.zero && unreachableSurvival.Result.decay==0,"Infeasible survival target leaves draft unchanged");
            var impossible=new RunnerBalanceDraft {probabilities=new Vector3(1,0,0),targetMomentumDrift=10};
            var failed=new RunnerBalanceFitter(impossible,profile,Vector3.forward);var failIterator=failed.Fit();while(failIterator.MoveNext()){}
            Check(!failed.MeetsTargets && failed.Report.Contains("NOT met"),"Infeasible target reported honestly");

            var mg=new GameObject("Momentum");var momentum=mg.AddComponent<MomentumSystem>();
            var player=new GameObject("Motor");player.AddComponent<Rigidbody>();var motor=player.AddComponent<RunnerMovementMotor>();
            motor.SetTuningProfile(profile);motor.SetMomentumSystem(momentum);motor.ResetMotion(Vector3.zero,Quaternion.identity);
            Set(momentum,"useTierSpeedDecay",true);Set(momentum,"mediumThreshold",0.2f);Set(momentum,"highThreshold",0.46f);Set(momentum,"maximumThreshold",0.8f);
            momentum.BeginRun();momentum.CurrentMomentum=85;momentum.CurrentTier=MomentumTier.Maximum;Call(motor,"HandleMomentumTierChanged",MomentumTier.Maximum);
            Set(motor,"blendedMaxSpeed",60f);Set(motor,"hasBlendedMaxSpeed",true);
            var state=new RunnerBalanceSimulation.State {velocity=new Vector3(2,4,22),momentum=85,tier=MomentumTier.Maximum,cap=60};
            motor.rigidBody.linearVelocity=state.velocity;float oldStep=Time.fixedDeltaTime;Time.fixedDeltaTime=0.02f;
            simulation.Tick(ref state,0.02f);Call(motor,"FixedUpdate");
            Near(momentum.CurrentMomentum,state.momentum,"Runtime/preview decay parity");Near(motor.CurrentMaxSpeed,state.cap,"Runtime/preview cap parity");
            Near(motor.Velocity.x,state.velocity.x,"Runtime/preview X parity");Near(motor.Velocity.y,state.velocity.y,"Runtime/preview Y parity");Near(motor.Velocity.z,state.velocity.z,"Runtime/preview Z parity");
            momentum.SetPaused(true);float frozen=momentum.CurrentMomentum,cap=motor.CurrentMaxSpeed;Call(motor,"FixedUpdate");
            Near(momentum.CurrentMomentum,frozen,"Pause freezes decay");Near(motor.CurrentMaxSpeed,cap,"Pause freezes cap");
            momentum.ResetMomentum();Near(momentum.CurrentMomentum,0,"Reset while paused");Near(motor.CurrentMaxSpeed,15,"Reset snaps x1 cap");
            momentum.SetPaused(false);momentum.CurrentMomentum=80;momentum.CurrentTier=MomentumTier.Maximum;motor.SetMomentumSystem(null);Near(motor.CurrentMaxSpeed,15,"Missing source x1 fallback");motor.SetMomentumSystem(momentum);
            var switched=Object.Instantiate(profile);Set(switched,"maxSpeedX3",50f);Set(switched,"maxSpeedX4",70f);motor.SetTuningProfile(switched);
            float before=momentum.CurrentMomentum;momentum.Tick(45,0.1f);Near(before-momentum.CurrentMomentum,0.025f,"Profile switching refreshes speed bands");motor.SetTuningProfile(profile);Object.DestroyImmediate(switched);
            Set(momentum,"useSmoothMomentumDifficulty",true);momentum.CurrentMomentum=90;momentum.CurrentTier=MomentumTier.Maximum;
            var smoothState=new RunnerBalanceSimulation.State{momentum=90,velocity=Vector3.forward*60,tier=MomentumTier.Maximum,cap=60};
            smoothSim.Tick(ref smoothState,.02f);momentum.Tick(60,.02f);Near(momentum.CurrentMomentum,smoothState.momentum,"Smooth runtime/preview loss parity");
            momentum.SetPaused(true);float pausedSmooth=momentum.CurrentMomentum;momentum.Tick(0,1);Near(momentum.CurrentMomentum,pausedSmooth,"Pause freezes upkeep");momentum.SetPaused(false);
            Set(momentum,"useSmoothMomentumDifficulty",false);
            var score=new GameObject("Score").AddComponent<DistanceScoreManager>();Set(score,"momentumSystem",momentum);Call(score,"OnEnable");
            int notifications=0;Action<float> listener=_=>notifications++;GameplayEvents.OnMultiplierChanged+=listener;
            momentum.ResetMomentum();notifications=0;momentum.OnMushroomLanded(LandingQuality.Perfect);momentum.OnMushroomLanded(LandingQuality.Perfect);
            Check(notifications==1,"One multiplier notification per tier transition");GameplayEvents.OnMultiplierChanged-=listener;
            score.AddBonusScore(25);int earned=score.CurrentScore;momentum.Tick(0,5);Check(score.CurrentScore==earned,"Decay preserves earned score");
            Call(score,"OnDisable");Time.fixedDeltaTime=oldStep;
            Set(momentum,"useTierSpeedDecay",false);momentum.CurrentMomentum=20;momentum.CurrentTier=MomentumTier.Medium;momentum.OnTierChanged.Invoke(MomentumTier.Medium);
            momentum.Tick(20,1);Near(momentum.CurrentMomentum,22,"Legacy passive gain preserved");
            float legacyMomentum=momentum.CurrentMomentum;Call(motor,"FixedUpdate");Near(momentum.CurrentMomentum,legacyMomentum,"Legacy motor does not duplicate manager momentum tick");
            Near(motor.CurrentMaxSpeed,30,"Legacy cap remains instantaneous");

            var manager=new GameObject("Manager").AddComponent<GameplayManager>();
            var mushroom=new GameObject("Test mushroom");var normal=mushroom.AddComponent<BoxCollider>();normal.isTrigger=true;var launcher=mushroom.AddComponent<MushroomLauncher>();
            Transform arrow=new GameObject("Arrow").transform;arrow.SetParent(mushroom.transform);arrow.rotation=Quaternion.Euler(-45,0,0);
            var good=new GameObject("Good").AddComponent<BoxCollider>();good.transform.SetParent(mushroom.transform);good.isTrigger=true;
            var perfectZone=new GameObject("Perfect").AddComponent<BoxCollider>();perfectZone.transform.SetParent(mushroom.transform);perfectZone.isTrigger=true;
            Set(launcher,"normalTrigger",normal);Set(launcher,"goodTrigger",good);Set(launcher,"perfectTrigger",perfectZone);Set(launcher,"launchDirection",arrow);
            var prefab=PrefabUtility.SaveAsPrefabAsset(mushroom,folder+"/Mushroom.prefab");Object.DestroyImmediate(mushroom);
            var savedLauncher=prefab.GetComponent<MushroomLauncher>();var initialRules=RunnerBalanceBindings.Rules(savedLauncher,"normalRules");
            string initialFile=File.ReadAllText(folder+"/Mushroom.prefab");float movement=profile.MoveAcceleration;Quaternion rotation=RunnerBalanceBindings.Reference<Transform>(savedLauncher,"launchDirection").localRotation;
            var applyDraft=new RunnerBalanceDraft {caps=new Vector4(17,31,46,62)};applyDraft.bad[0]=new(MushroomSpeedMode.Add,-8.25f);
            var edits=RunnerBalanceBindings.Edits(applyDraft,momentum,score,motor,manager,profile,new List<GameObject>{prefab});
            foreach(var e in edits){e.Before();e.After();}
            Near(profile.MaxSpeed,15,"Review is non-mutating");Check(File.ReadAllText(folder+"/Mushroom.prefab")==initialFile,"Review preserves prefab file");
            RunnerBalanceBindings.Apply(edits);Near(profile.MaxSpeed,17,"Apply profile");Near(profile.MoveAcceleration,movement,"Apply preserves movement tuning");
            Check(RunnerBalanceBindings.Reference<Transform>(savedLauncher,"launchDirection").localRotation==rotation,"Apply preserves arrow");
            Near(RunnerBalanceBindings.Rules(savedLauncher,"normalRules")[0].value,-8.25f,"Apply prefab rules");
            Check(File.ReadAllText(folder+"/Mushroom.prefab").Contains("-8.25"),"Prefab changes saved to disk");
            Undo.PerformUndo();Near(profile.MaxSpeed,15,"Undo profile");Near(RunnerBalanceBindings.Rules(savedLauncher,"normalRules")[0].value,initialRules[0].value,"Undo prefab");
            Undo.PerformRedo();Near(profile.MaxSpeed,17,"Redo profile");Near(RunnerBalanceBindings.Rules(savedLauncher,"normalRules")[0].value,-8.25f,"Redo prefab");
            var loaded=RunnerBalanceBindings.Load(momentum,score,profile,savedLauncher);Near(loaded.caps.x,17,"Reload applied settings");Check(loaded.smoothDifficulty && loaded.upkeep==applyDraft.upkeep,"Smooth settings Apply/reload");
            var window=ScriptableObject.CreateInstance<RunnerBalanceWindow>();
            Set(window,"momentum",momentum);Set(window,"scoring",score);Set(window,"motor",motor);Set(window,"manager",manager);Set(window,"profile",profile);Set(window,"prefabs",new List<GameObject>{prefab});Set(window,"draft",applyDraft.Copy());
            Call(window,"CaptureTargets");Call(window,"Rebuild");Check(Call(window,"TargetError")==null,"Window accepts valid selected targets");Call(window,"StopJobs");
            var beforeMixed=(RunnerBalanceMixed)window.GetType().GetField("mixed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
            int completedBefore=beforeMixed.completed;Call(window,"UpdateJobs");Check(beforeMixed.completed==completedBefore,"Cancel stops mixed calculation");
            Object.DestroyImmediate(window);
            var custom=new RunnerBalanceDraft {preset="Custom",cadence=3.25f};RunnerBalanceLabStorage.instance.Store(custom);
            var presetDraft=new RunnerBalanceDraft();presetDraft.Preset(2);RunnerBalanceLabStorage.instance.Store(presetDraft);
            Near(RunnerBalanceLabStorage.instance.custom.cadence,3.25f,"Preset selection preserves last Custom draft");
            Check(File.Exists("UserSettings/RunnerBalanceLab.asset"),"Custom draft persisted to disk");
            reports.Add("PASS: "+checks+" checks");
        }
        catch(Exception e){reports.Add("FAIL after "+checks+" checks: "+e);Debug.LogException(e);code=1;}
        finally
        {
            foreach(var root in scene.GetRootGameObjects())Object.DestroyImmediate(root);if(oldScene.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(oldScene);
            if(!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets/BalanceVerification_"))AssetDatabase.DeleteAsset(folder);
            File.WriteAllText(output,string.Join("\n\n",reports));
        }
        EditorApplication.Exit(code);
    }
}
