using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RunnerBalanceBindings
{
    public static float Float(Object target,string field) => new SerializedObject(target).FindProperty(field).floatValue;
    public static T Reference<T>(Object target,string field) where T:Object => new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
    public static List<MushroomSpeedRule> Rules(MushroomLauncher launcher,string name)
    {
        var array=new SerializedObject(launcher).FindProperty(name);var rules=new List<MushroomSpeedRule>();
        for(int i=0;i<array.arraySize;i++) {var e=array.GetArrayElementAtIndex(i);rules.Add(new((MushroomSpeedMode)e.FindPropertyRelative("mode").enumValueIndex,e.FindPropertyRelative("value").floatValue));}
        return rules;
    }
    public static Vector3 Arrow(MushroomLauncher launcher)
    {
        var t=Reference<Transform>(launcher,"launchDirection");return t!=null?t.forward:Vector3.zero;
    }
    public static RunnerBalanceDraft Load(MomentumSystem momentum,DistanceScoreManager score,MovementTuningProfile profile,MushroomLauncher launcher)
    {
        float maximum=Float(momentum,"maximumMomentum");
        var d=new RunnerBalanceDraft {caps=RunnerBalanceMath.Caps(profile),
            thresholds=new Vector3(Float(momentum,"mediumThreshold"),Float(momentum,"highThreshold"),Float(momentum,"maximumThreshold"))*100,
            rewards=new Vector3(-Float(momentum,"badLandingPenalty"),Float(momentum,"goodLandingBonus"),Float(momentum,"perfectLandingBonus"))*(100/maximum),
            decay=Float(momentum,"tierDecayStrength"),hysteresis=Float(momentum,"tierHysteresis"),transition=Float(momentum,"tierSpeedTransitionTime"),
            pointsPerUnit=Float(score,"pointsPerUnit"),bad=Rules(launcher,"normalRules"),good=Rules(launcher,"goodRules"),perfect=Rules(launcher,"perfectRules")};
        var settings=new SerializedObject(momentum);
        d.smoothDifficulty=settings.FindProperty("useSmoothMomentumDifficulty").boolValue;
        d.upkeep=settings.FindProperty("momentumUpkeep").vector4Value;
        d.highPressure=Float(momentum,"highMomentumPressure");
        return d;
    }
    public sealed class Edit
    {
        public Object target;
        public string field;
        public float value;
        public Vector4 vector;
        public Object reference;
        public List<MushroomSpeedRule> rules;
        public int kind; // 0 float, 1 bool, 2 reference, 3 rule array
        public string Before()
        {
            var p=new SerializedObject(target).FindProperty(field);
            if(p==null) throw new InvalidOperationException("Missing serialized field: "+field);
            return kind switch {4=>p.vector4Value.ToString("G9"),1=>p.boolValue.ToString(),2=>p.objectReferenceValue!=null?p.objectReferenceValue.name:"None",
                3=>RuleText(Rules((MushroomLauncher)target,field)),_=>p.floatValue.ToString("G9")};
        }
        public string After() => kind switch {4=>vector.ToString("G9"),1=>(value!=0).ToString(),2=>reference!=null?reference.name:"None",3=>RuleText(rules),_=>value.ToString("G9")};
        public void Write(SerializedObject so)
        {
            var p=so.FindProperty(field);
            if(kind==4)p.vector4Value=vector;
            else if(kind==1) p.boolValue=value!=0;
            else if(kind==2) p.objectReferenceValue=reference;
            else if(kind==3)
            {
                p.arraySize=rules.Count;
                for(int i=0;i<rules.Count;i++) {var e=p.GetArrayElementAtIndex(i);e.FindPropertyRelative("mode").enumValueIndex=(int)rules[i].mode;e.FindPropertyRelative("value").floatValue=rules[i].value;}
            }
            else p.floatValue=value;
        }
    }
    static string RuleText(List<MushroomSpeedRule> rules) => rules.Count==0?"(empty)":string.Join(" ? ",rules.Select(r=>$"{r.mode} {r.value:0.###}"));
    public static List<Edit> Edits(RunnerBalanceDraft d,MomentumSystem momentum,DistanceScoreManager score,
        RunnerMovementMotor motor,GameplayManager manager,MovementTuningProfile profile,List<GameObject> prefabs)
    {
        var edits=new List<Edit>();
        void Number(Object target,string field,float value,int kind=0) => edits.Add(new Edit{target=target,field=field,value=value,kind=kind});
        void Ref(Object target,string field,Object value) => edits.Add(new Edit{target=target,field=field,reference=value,kind=2});
        Number(momentum,"useSmoothMomentumDifficulty",d.smoothDifficulty?1:0,1);
        Number(momentum,"highMomentumPressure",d.highPressure);
        edits.Add(new Edit{target=momentum,field="momentumUpkeep",kind=4,vector=d.upkeep});
        Number(momentum,"useTierSpeedDecay",1,1);Number(momentum,"maximumMomentum",100);Number(momentum,"startingMomentum",0);
        Number(momentum,"momentumGainPerSecond",0);Number(momentum,"tierDecayStrength",d.decay);Number(momentum,"tierHysteresis",d.hysteresis);Number(momentum,"tierSpeedTransitionTime",d.transition);
        Number(momentum,"mediumThreshold",d.thresholds.x/100);Number(momentum,"highThreshold",d.thresholds.y/100);Number(momentum,"maximumThreshold",d.thresholds.z/100);
        Number(momentum,"badLandingPenalty",-d.rewards.x);Number(momentum,"goodLandingBonus",d.rewards.y);Number(momentum,"perfectLandingBonus",d.rewards.z);
        Number(score,"pointsPerUnit",d.pointsPerUnit);
        for(int i=0;i<4;i++) {Number(score,$"stage{i+1}Multiplier",i+1);Number(profile,$"maxSpeedX{i+1}",d.caps[i]);}
        Ref(score,"momentumSystem",momentum);Ref(motor,"tuningProfile",profile);
        Ref(manager,"momentumSystem",momentum);Ref(manager,"runnerMovementMotor",motor);Ref(manager,"distanceScoreManager",score);Ref(manager,"defaultMovementTuningProfile",profile);
        foreach(var prefab in prefabs)
        {
            var launcher=prefab.GetComponentInChildren<MushroomLauncher>(true);
            for(int q=0;q<3;q++) edits.Add(new Edit{target=launcher,field=new[]{"normalRules","goodRules","perfectRules"}[q],kind=3,rules=new List<MushroomSpeedRule>(d.Rules(q))});
        }
        return edits;
    }
    public static void Apply(List<Edit> edits)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply is only available outside Play Mode.");
        Object[] targets=edits.Select(e=>e.target).Distinct().ToArray();
        foreach(var e in edits) e.Before(); // Validate every field before mutating any target.
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply Runner Balance");
        Undo.RecordObjects(targets,"Apply Runner Balance");
        try
        {
            foreach(var target in targets)
            {
                var so=new SerializedObject(target);foreach(var e in edits.Where(e=>e.target==target)) e.Write(so);
                so.ApplyModifiedProperties();EditorUtility.SetDirty(target);
                if(target is Component c && !EditorUtility.IsPersistent(c))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                    EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
                }
            }
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            foreach(var target in targets) if(EditorUtility.IsPersistent(target)) AssetDatabase.SaveAssetIfDirty(target);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            foreach(var target in targets) if(EditorUtility.IsPersistent(target)) {EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);}
            throw;
        }
    }
}
