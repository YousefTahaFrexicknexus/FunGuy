using System;
using System.Collections;
using UnityEngine;

/// <summary>Deterministic bounded coordinate search; failures remain visible rather than changing targets.</summary>
public sealed class RunnerBalanceFitter
{
    public RunnerBalanceDraft Result { get; private set; }
    public int Evaluations { get; private set; }
    public string Report { get; private set; }
    public bool MeetsTargets { get; private set; }
    readonly MovementTuningProfile profile;
    readonly Vector3 arrow;
    readonly RunnerBalanceDraft request;
    float[] seed;
    public RunnerBalanceFitter(RunnerBalanceDraft draft,MovementTuningProfile movement,Vector3 direction)
    { request=draft.Copy(); profile=movement; arrow=direction; }
    RunnerBalanceDraft Decode(float[] p)
    {
        var d=request.Copy();d.rewards=new Vector3(p[0],p[1],p[2]);d.thresholds=new Vector3(p[3],p[4],p[5]);
        for(int q=0;q<3;q++) {var rules=d.Rules(q);rules.Clear();rules.Add(new(MushroomSpeedMode.Add,p[6+q*2]));rules.Add(new(MushroomSpeedMode.Multiply,p[7+q*2]));}
        d.decay=p[12]; return d;
    }
    float Score(float[] p)
    {
        Evaluations++;var d=Decode(p);if(d.Validate()!=null) return 1e9f;
        var sim=new RunnerBalanceSimulation(d,profile,arrow);if(!sim.OrderedRewards()) return 1e9f;
        RunnerBalanceTrace trace;
        Vector2 drift;
        try
        {
            trace=sim.Run(2,null,false,Mathf.Min(d.horizon,d.targets.z+5));
            drift=sim.Drift(1);
        }
        catch(InvalidOperationException) { return 1e9f; }
        float cost=0;
        for(int t=0;t<3;t++)
        {
            float actual=trace.reached[t]<0?d.targets.z+10:trace.reached[t];
            cost+=25*Mathf.Pow((actual-d.targets[t])/Mathf.Max(1,d.targets[t]),2);
            float low=trace.points[d.targets[t]-1].momentum,high=trace.points[d.targets[t]].momentum;
            cost+=0.05f*Mathf.Pow((d.thresholds[t]-(low+high)*0.5f)/Mathf.Max(1,d.rewards.z),2);
        }
        cost+=Mathf.Pow(drift.x-d.targetMomentumDrift,2)+Mathf.Pow((drift.y-d.targetSpeedDrift)*100,2);
        if(d.smoothDifficulty)
        {
            // At a common maintained speed, average play must lose momentum in x3,
            // Good must lose it in x4, and Perfect must be able to hold x4.
            float x3=(d.thresholds.y+d.thresholds.z)*.5f,x4=(d.thresholds.z+100)*.5f;
            cost+=2*Mathf.Pow(Mathf.Max(0,sim.NetMomentum(x3,d.caps.z,3)),2);
            cost+=2*Mathf.Pow(Mathf.Max(0,sim.NetMomentum(x4,d.caps.w,1)),2);
            cost+=2*Mathf.Pow(Mathf.Min(0,sim.NetMomentum(x4,d.caps.w,2)),2);
        }
        for(int i=0;i<p.Length;i++) cost+=0.0001f*Mathf.Pow((p[i]-seed[i])/Mathf.Max(1,Mathf.Abs(seed[i])),2);
        return float.IsNaN(cost)||float.IsInfinity(cost)?1e9f:cost;
    }
    public IEnumerator Fit()
    {
        float perfect=80f/request.targets.z+(request.smoothDifficulty?request.upkeep.z*request.cadence:0),good=perfect*0.5f;
        float bad=request.probabilities.x>0.001f
            ? (request.targetMomentumDrift-request.probabilities.y*good-request.probabilities.z*perfect)/request.probabilities.x : -perfect;
        seed=new[]{Mathf.Clamp(bad,-100,0),good,perfect,
            80f*(request.targets.x-0.5f)/request.targets.z,80f*(request.targets.y-0.5f)/request.targets.z,80f*(request.targets.z-0.5f)/request.targets.z,
            -2f,0.85f,1f,1.02f,5f,1.2f,request.decay};
        float[] p=(float[])seed.Clone();
        float[] steps={5,3,3,5,5,5,request.caps.w*0.1f,0.08f,request.caps.w*0.1f,0.08f,request.caps.w*0.1f,0.08f,2};
        float[] min={-100,0,0.1f,1,2,3,-request.caps.w,0,-request.caps.w,0,-request.caps.w,0,0};
        float[] max={0,100,100,97,98,99,request.caps.w,2,request.caps.w,2,request.caps.w,2,20};
        float best=Score(p);yield return null;
        float overallBest=best;float[] overall=(float[])p.Clone();float[] originalSteps=(float[])steps.Clone();
        for(int restart=0;restart<(request.smoothDifficulty?3:1);restart++)
        {
        if(restart>0)
        {
            p=(float[])overall.Clone();steps=(float[])originalSteps.Clone();
            if(restart==1){p[3]=request.thresholds.x;p[4]=request.thresholds.y;p[5]=request.thresholds.z;}
            else
            {
                var proposal=Decode(p);var proposalTrace=new RunnerBalanceSimulation(proposal,profile,arrow).Run(2,null,false);
                for(int t=0;t<3;t++)p[3+t]=Mathf.Clamp((proposalTrace.points[request.targets[t]-1].momentum+proposalTrace.points[request.targets[t]].momentum)*.5f,min[3+t],max[3+t]);
            }
            best=Score(p);
        }
        for(int level=0;level<6;level++)
        {
            for(int sweep=0;sweep<5;sweep++)
            {
                bool improved=false;
                for(int i=0;i<p.Length;i++)
                {
                    for(int direction=-1;direction<=1;direction+=2)
                    {
                        float old=p[i];p[i]=Mathf.Clamp(old+steps[i]*direction,min[i],max[i]);
                        float cost=Score(p);
                        if(cost<best) {best=cost;improved=true;} else p[i]=old;
                        yield return null;
                    }
                }
                if(!improved) break;
            }
            for(int i=0;i<steps.Length;i++) steps[i]*=0.4f;
        }
        if(best<overallBest){overallBest=best;overall=(float[])p.Clone();}
        }
        p=overall;
        Result=Decode(p);var sim=new RunnerBalanceSimulation(Result,profile,arrow);
        var trace=sim.Run(2,null,false);Vector2 driftResult=sim.Drift(1);
        MeetsTargets=Result.Validate()==null && sim.OrderedRewards();
        string jumps="";
        for(int t=0;t<3;t++) {MeetsTargets &= trace.reached[t]==Result.targets[t]; jumps+=$"x{t+2}: {(trace.reached[t]<0?"not reached":trace.reached[t].ToString())} (target {Result.targets[t]})  ";}
        MeetsTargets &= Mathf.Abs(driftResult.x-Result.targetMomentumDrift)<=0.25f && Mathf.Abs(driftResult.y-Result.targetSpeedDrift)<=0.005f;
        string sustain="";
        if(Result.smoothDifficulty)
        {
            float mix=sim.NetMomentum((Result.thresholds.y+Result.thresholds.z)*.5f,Result.caps.z,3);
            float goodNet=sim.NetMomentum((Result.thresholds.z+100)*.5f,Result.caps.w,1);
            float perfectNet=sim.NetMomentum((Result.thresholds.z+100)*.5f,Result.caps.w,2);
            MeetsTargets &= mix<=.05f && goodNet<=.05f && perfectNet>=-.05f;
            sustain=$"\nSustain checks (net points/interval): mixed at x3 {mix:+0.##;-0.##;0} (<=0), Good at x4 {goodNet:+0.##;-0.##;0} (<=0), Perfect at x4 {perfectNet:+0.##;-0.##;0} (>=0).";
        }
        Report=(MeetsTargets?"Targets met within calibration tolerance. ":"Targets NOT met within the search budget; this is an approximate proposal, not a balanced result. ")
            +jumps+sustain+$"\nCalibration: momentum {driftResult.x:+0.###;-0.###;0} vs {Result.targetMomentumDrift:+0.###;-0.###;0}; speed {driftResult.y:P2} vs {Result.targetSpeedDrift:P2}."
            +"\nTolerance: 0.25 momentum points and 0.5 percentage points of speed per landing interval. Boundaries and requested targets were not changed.";
        if(!MeetsTargets && request.probabilities.x>=0.9999f && request.targetMomentumDrift>0)
            Report+="\nConflicting constraints: a Bad-only mix cannot gain momentum while Bad rewards are nonpositive and passive gain is disabled.";
    }
}

/// <summary>Fits the no-landing diagnostic without changing rewards, speed caps or tier thresholds.</summary>
public sealed class RunnerSurvivalFitter
{
    public RunnerBalanceDraft Result { get; private set; }
    public float Achieved { get; private set; }
    public bool Met { get; private set; }
    public string Report { get; private set; }
    readonly RunnerBalanceDraft source;
    readonly MovementTuningProfile profile;
    readonly Vector3 arrow;
    readonly float speed,target;
    float bestError=float.PositiveInfinity,bestScale=1;
    public RunnerSurvivalFitter(RunnerBalanceDraft draft,MovementTuningProfile movement,Vector3 direction,float startSpeed,float seconds)
    {source=draft.Copy();profile=movement;arrow=direction;speed=startSpeed;target=seconds;Result=source.Copy();}
    float Evaluate(float scale)
    {
        var proposal=source.Copy();proposal.upkeep=source.upkeep*scale;proposal.decay=source.decay*scale;
        float time=new RunnerBalanceSimulation(proposal,profile,arrow).DropTime(speed);
        float error=time<0?float.PositiveInfinity:Mathf.Abs(time-target);
        if(error<bestError){bestError=error;bestScale=scale;Result=proposal;Achieved=time;}
        return time<0?120.02f:time;
    }
    public IEnumerator Fit()
    {
        if(!source.smoothDifficulty || !float.IsFinite(target) || target<.1f || target>119)
        {Report="Use smooth difficulty and a target from 0.1 to 119 seconds.";yield break;}
        // Scan before refining: actual drag and tier-cap transitions are simulated, not approximated by a ratio.
        float priorScale=0,priorTime=Evaluate(0),lo=0,hi=0;
        Evaluate(1);yield return null;
        for(int i=0;i<=40;i++)
        {
            float scale=Mathf.Pow(10,-3+i*.15f);float time=Evaluate(scale);
            if(hi==0 && priorTime>=target && time<=target){lo=priorScale;hi=scale;}
            priorTime=time;priorScale=scale;yield return null;
        }
        if(hi>0)for(int i=0;i<24;i++)
        {
            float mid=(lo+hi)*.5f;float time=Evaluate(mid);
            if(time>target)lo=mid;else hi=mid;
            yield return null;
        }
        Met=bestError<=.05f;
        if(float.IsPositiveInfinity(bestError))
        {Achieved=-1;Result=source.Copy();Report="Target not reached: the current loss curve cannot cause a demotion within 120 seconds. Add nonzero upkeep or a speed penalty first.";yield break;}
        Result.preset="Custom";
        Report=$"{(Met?"Survival target met":"Survival target NOT met within fitting limits")}: requested {target:0.00}s, achieved {Achieved:0.00}s at starting speed {speed:0.##}. "
            +$"Upkeep and speed-penalty strength scaled by {bestScale:0.###}. Rewards, caps, thresholds and launch rules stayed fixed. Progression has been recalculated; its targets may now differ.";
    }
}
