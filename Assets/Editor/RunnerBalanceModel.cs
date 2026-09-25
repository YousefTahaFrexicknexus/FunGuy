using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

[Serializable]
public sealed class RunnerBalanceDraft
{
    public string preset = "Custom";
    public Vector4 caps = new(15,30,45,60);
    public Vector3 thresholds = new(20,46,80);
    public Vector3 rewards = new(-8,4,8);
    public Vector3 probabilities = new(0.3f,0.5f,0.2f);
    public Vector3Int targets = new(3,9,21);
    public bool smoothDifficulty = true;
    public Vector4 upkeep = new(0,0.4f,1.6f,3.2f);
    public float highPressure = 4, firstTierLandings = 3, difficultyGrowth = 2;
    public void UpdateTargets()
    {
        int a=Mathf.Max(1,Mathf.RoundToInt(firstTierLandings));
        int b=Mathf.Max(1,Mathf.RoundToInt(firstTierLandings*difficultyGrowth));
        int c=Mathf.Max(1,Mathf.RoundToInt(firstTierLandings*difficultyGrowth*difficultyGrowth));
        targets=new Vector3Int(a,a+b,a+b+c);
    }
    public float Loss(float momentum,float speed,MomentumTier tier) => smoothDifficulty
        ? RunnerBalanceMath.SmoothLoss(momentum,speed,caps,upkeep,decay,highPressure)
        : RunnerBalanceMath.Decay(speed,tier,caps,decay);
    public float decay = 4, hysteresis = 2, transition = 0.35f, cadence = 2;
    public float initialSpeed, initialMomentum, pointsPerUnit = 10;
    public float targetMomentumDrift, targetSpeedDrift;
    public int horizon = 100, trials = 2048, seed = 1337;
    public Vector2 input;
    public List<MushroomSpeedRule> bad = new() { new(MushroomSpeedMode.Add,-2), new(MushroomSpeedMode.Multiply,0.9f) };
    public List<MushroomSpeedRule> good = new() { new(MushroomSpeedMode.Add,1), new(MushroomSpeedMode.Multiply,1.02f) };
    public List<MushroomSpeedRule> perfect = new() { new(MushroomSpeedMode.Add,5), new(MushroomSpeedMode.Multiply,1.15f) };
    public List<MushroomSpeedRule> Rules(int quality) => quality == 0 ? bad : quality == 1 ? good : perfect;
    public RunnerBalanceDraft Copy() => JsonUtility.FromJson<RunnerBalanceDraft>(JsonUtility.ToJson(this));
    public void Preset(int index)
    {
        smoothDifficulty=true;upkeep=new Vector4(0,.4f,1.6f,3.2f);highPressure=4;
        firstTierLandings=3;difficultyGrowth=2;UpdateTargets();
        preset = new[] {"Negative Sum","Zero Sum","Positive Sum"}[index];
        targetMomentumDrift = (index - 1) * 2f;
        targetSpeedDrift = (index - 1) * 0.02f;
        caps = new Vector4(15,30,45,60);
        thresholds = new Vector3(20,46,80);
        rewards = new Vector3(-8 + (index-1)*2,4,8);
        decay = 4; hysteresis = 2; transition = 0.35f;
        bad = new() { new(MushroomSpeedMode.Add,-2), new(MushroomSpeedMode.Multiply,0.9f+(index-1)*0.02f) };
        good = new() { new(MushroomSpeedMode.Add,1), new(MushroomSpeedMode.Multiply,1.02f) };
        perfect = new() { new(MushroomSpeedMode.Add,5), new(MushroomSpeedMode.Multiply,1.15f) };
    }
    public void Probability(int index, float value)
    {
        value = Mathf.Clamp01(value);
        int a = (index+1)%3, b = (index+2)%3;
        float remainder = probabilities[a]+probabilities[b];
        float share = remainder > 0 ? probabilities[a]/remainder : 0.5f;
        probabilities[index]=value; probabilities[a]=(1-value)*share; probabilities[b]=(1-value)*(1-share);
    }
    public string Validate()
    {
        if(smoothDifficulty)
        {
            for(int i=0;i<4;i++)if(float.IsNaN(upkeep[i])||float.IsInfinity(upkeep[i])||upkeep[i]<0||(i>0&&upkeep[i]<upkeep[i-1]))return "Upkeep points must be finite, nonnegative and nondecreasing.";
            if(float.IsNaN(highPressure)||float.IsInfinity(highPressure)||highPressure<1)return "High-momentum pressure must be finite and at least 1.";
        }
        foreach(float v in new[]{rewards.x,rewards.y,rewards.z,probabilities.x,probabilities.y,probabilities.z,input.x,input.y})
            if(float.IsNaN(v)||float.IsInfinity(v)) return "Rewards, probabilities and preview input must be finite.";
        if (!RunnerBalanceMath.ValidBands(caps)) return "Speed caps must be finite, positive, and strictly increasing. Imported equal caps must be edited first.";
        if (!(thresholds.x>0 && thresholds.y>thresholds.x && thresholds.z>thresholds.y && thresholds.z<100)) return "Thresholds must increase strictly between 0 and 100.";
        if (!(rewards.x<=0 && rewards.y>=0 && rewards.z>0 && rewards.z>=rewards.y)) return "Bad reward must be nonpositive; Perfect >= Good >= 0, with Perfect positive.";
        foreach(float v in new[]{decay,hysteresis,transition,initialSpeed,initialMomentum,pointsPerUnit})
            if(float.IsNaN(v)||float.IsInfinity(v)||v<0) return "Settings must be finite and nonnegative.";
        if(initialMomentum>100 || cadence<0.05f || cadence>30 || float.IsNaN(cadence)) return "Momentum must be <=100; landing cadence must be 0.05?30 seconds.";
        if(float.IsNaN(targetMomentumDrift)||float.IsInfinity(targetMomentumDrift)||float.IsNaN(targetSpeedDrift)||float.IsInfinity(targetSpeedDrift)) return "Calibration targets must be finite.";
        if(horizon<1 || horizon>500 || trials<1 || trials>8192) return "Use 1?500 landings and 1?8192 mixed trials.";
        if(targets.x<1 || targets.y<=targets.x || targets.z<=targets.y || targets.z>horizon) return "Target jumps must increase and fit inside the simulation horizon.";
        if(probabilities.x<0 || probabilities.y<0 || probabilities.z<0 || Mathf.Abs(probabilities.x+probabilities.y+probabilities.z-1)>0.001f) return "Landing probabilities must total 100%.";
        foreach(var rules in new[]{bad,good,perfect}) foreach(var rule in rules)
            if(float.IsNaN(rule.value)||float.IsInfinity(rule.value)) return "Speed rule values must be finite.";
        return null;
    }
}

[FilePath("UserSettings/RunnerBalanceLab.asset", FilePathAttribute.Location.ProjectFolder)]
public sealed class RunnerBalanceLabStorage : ScriptableSingleton<RunnerBalanceLabStorage>
{
    public RunnerBalanceDraft custom = new();
    public RunnerBalanceDraft current = new();
    public void Store(RunnerBalanceDraft draft)
    {
        current=draft.Copy();
        if(draft.preset=="Custom") custom=draft.Copy();
        Save(true);
    }
}

public struct RunnerBalancePoint
{
    public float jumps,time,momentum,speed,score,cap;
    public int tier;
}
public sealed class RunnerBalanceTrace
{
    public readonly List<RunnerBalancePoint> points = new();
    public readonly float[] tierSeconds = new float[4];
    public readonly int[] reached = {-1,-1,-1};
}

/// <summary>Cadence-based preview; it does not model collision success or generated course reachability.</summary>
public sealed class RunnerBalanceSimulation
{
    public const float StepTime = 0.02f;
    readonly RunnerBalanceDraft d;
    readonly MovementTuningProfile profile;
    readonly Vector3 arrow;
    readonly MovementInputFrame input;
    public struct State
    {
        public Vector3 velocity;
        public float momentum,cap,time,score;
        public MomentumTier tier;
    }
    public RunnerBalanceSimulation(RunnerBalanceDraft draft, MovementTuningProfile movement, Vector3 direction)
    {
        d=draft; profile=movement; arrow=direction.normalized;
        Vector2 stick=Vector2.ClampMagnitude(d.input,1);
        input=new MovementInputFrame(stick,new Vector3(stick.x,0,stick.y).normalized,Vector3.forward,stick.magnitude,false);
    }
    public State Initial() => new() {
        velocity=Vector3.forward*d.initialSpeed, momentum=d.initialMomentum,
        tier=RunnerBalanceMath.ResolveTier(d.initialMomentum,MomentumTier.Low,d.thresholds,d.hysteresis),
        cap=d.caps[(int)RunnerBalanceMath.ResolveTier(d.initialMomentum,MomentumTier.Low,d.thresholds,d.hysteresis)] };
    public void Tick(ref State s,float dt)
    {
        s.momentum=Mathf.Clamp(s.momentum-d.Loss(s.momentum,s.velocity.z,s.tier)*dt,0,100);
        s.tier=RunnerBalanceMath.ResolveTier(s.momentum,s.tier,d.thresholds,d.hysteresis);
        s.cap=RunnerBalanceMath.BlendCap(s.cap,d.caps[(int)s.tier],d.transition,dt);
        BounceMovementMath.ApplyShapedGravity(ref s.velocity,profile,Vector3.up,dt);
        BounceMovementMath.ApplyAirAcceleration(ref s.velocity,profile,input,Vector3.up,false,false,dt,s.cap);
        BounceMovementMath.ApplyPlanarDrag(ref s.velocity,Vector3.up,profile.AirDrag,dt);
        BounceMovementMath.ApplySoftSpeedLimit(ref s.velocity,profile,Vector3.up,dt,s.cap);
        s.score+=Mathf.Max(0,s.velocity.z)*dt*d.pointsPerUnit*((int)s.tier+1);
        s.time+=dt;
    }
    public void Land(ref State s,int quality)
    {
        if(!MushroomLauncher.TryCalculateLaunch(s.velocity,profile.MushroomBounceEffectiveness,d.Rules(quality),arrow,out var velocity))
            throw new InvalidOperationException("The representative mushroom has invalid launch settings or a rule overflowed.");
        s.velocity=velocity;
        s.momentum=Mathf.Clamp(s.momentum+d.rewards[quality],0,100);
        s.tier=RunnerBalanceMath.ResolveTier(s.momentum,s.tier,d.thresholds,d.hysteresis);
    }
    static RunnerBalancePoint Point(State s,float jumps) => new() {
        jumps=jumps,time=s.time,momentum=s.momentum,speed=s.velocity.z,score=s.score,cap=s.cap,tier=(int)s.tier };
    public RunnerBalanceTrace Run(int quality, System.Random random=null, bool detail=true, int? horizon=null)
    {
        var trace=new RunnerBalanceTrace(); State state=Initial(); trace.points.Add(Point(state,0));
        for(int t=1;t<=3;t++) if((int)state.tier>=t) trace.reached[t-1]=0;
        int count=horizon??d.horizon;
        for(int jump=1;jump<=count;jump++)
        {
            float elapsed=0;
            while(elapsed<d.cadence-0.000001f)
            {
                float step=Mathf.Min(StepTime,d.cadence-elapsed); trace.tierSeconds[(int)state.tier]+=step; Tick(ref state,step); elapsed+=step;
                if(detail) trace.points.Add(Point(state,jump-1+elapsed/d.cadence));
            }
            int q=quality;
            if(random!=null) { double r=random.NextDouble(); q=r<d.probabilities.x?0:r<d.probabilities.x+d.probabilities.y?1:2; }
            Land(ref state,q); trace.points.Add(Point(state,jump));
            for(int t=1;t<=3;t++) if(trace.reached[t-1]<0 && (int)state.tier>=t) trace.reached[t-1]=jump;
        }
        return trace;
    }
    public Vector2 Drift(int tier)
    {
        float low=tier==0?0:d.caps[tier-1];
        float speed=(low+d.caps[tier])*0.5f;
        float bottom=tier==0?0:d.thresholds[tier-1], top=tier==3?100:d.thresholds[tier];
        Vector2 drift=Vector2.zero;
        for(int q=0;q<3;q++)
        {
            State s=new() { velocity=Vector3.forward*speed, momentum=(bottom+top)*0.5f,tier=(MomentumTier)tier,cap=d.caps[tier] };
            float m=s.momentum; Land(ref s,q);
            for(float elapsed=0;elapsed<d.cadence-0.000001f;)
            { float step=Mathf.Min(StepTime,d.cadence-elapsed); Tick(ref s,step); elapsed+=step; }
            drift+=d.probabilities[q]*new Vector2(s.momentum-m,(s.velocity.z-speed)/Mathf.Max(speed,0.001f));
        }
        return drift;
    }
    public float NetMomentum(float momentum,float speed,int quality)
    {
        if(quality==3){float sum=0;for(int q=0;q<3;q++)sum+=d.probabilities[q]*NetMomentum(momentum,speed,q);return sum;}
        var tier=RunnerBalanceMath.ResolveTier(momentum,MomentumTier.Low,d.thresholds,d.hysteresis);
        State s=new(){momentum=momentum,velocity=Vector3.forward*speed,tier=tier,cap=d.caps[(int)tier]};
        Land(ref s,quality);
        for(float time=0;time<d.cadence-.000001f;){float dt=Mathf.Min(StepTime,d.cadence-time);Tick(ref s,dt);time+=dt;}
        return s.momentum-momentum;
    }
    public float DropTime(float speed,float horizon=120)
    {
        State s=new(){momentum=100,velocity=Vector3.forward*speed,tier=MomentumTier.Maximum,cap=d.caps.w};
        while(s.time<horizon && s.tier==MomentumTier.Maximum)Tick(ref s,StepTime);
        return s.tier==MomentumTier.Maximum?-1:s.time;
    }
    public int BadTolerance(float speed)
    {
        State s=new(){momentum=100,velocity=Vector3.forward*speed,tier=MomentumTier.Maximum,cap=d.caps.w};
        for(int n=1;n<=d.horizon;n++)
        {
            Land(ref s,0);
            for(float time=0;time<d.cadence-.000001f;){float dt=Mathf.Min(StepTime,d.cadence-time);Tick(ref s,dt);time+=dt;}
            if(s.tier!=MomentumTier.Maximum)return n;
        }
        return -1;
    }
    public bool OrderedRewards()
    {
        // Each Add/Multiply/Set chain is affine until the final clamp. Check endpoints and all clamp breakpoints.
        var speeds=new List<float>{0,d.caps.w};
        for(int q=0;q<3;q++)
        {
            float a=profile.MushroomBounceEffectiveness,b=0;
            foreach(var rule in d.Rules(q))
            {
                if(rule.mode==MushroomSpeedMode.Set) {a=0;b=rule.value;}
                else if(rule.mode==MushroomSpeedMode.Multiply) {a*=rule.value;b*=rule.value;}
                else b+=rule.value;
            }
            if(a!=0 && -b/a>0 && -b/a<d.caps.w) speeds.Add(-b/a);
        }
        foreach(float speed in speeds)
        {
            float previous=-1;
            for(int q=0;q<3;q++)
            {
                if(!MushroomLauncher.TryCalculateLaunch(Vector3.forward*speed,profile.MushroomBounceEffectiveness,d.Rules(q),arrow,out var v)
                    || v.z+0.0001f<previous) return false;
                previous=v.z;
            }
        }
        return true;
    }
}

public sealed class RunnerBalanceMixed
{
    public int completed;
    public readonly double[] tierSeconds=new double[4];
    public readonly List<int>[] arrivals={new(),new(),new()};
    public readonly List<RunnerBalancePoint> mean=new();
    public void Add(RunnerBalanceTrace trace)
    {
        completed++;
        for(int t=0;t<4;t++)tierSeconds[t]+=trace.tierSeconds[t];
        for(int t=0;t<3;t++) if(trace.reached[t]>=0) arrivals[t].Add(trace.reached[t]);
        for(int i=0;i<trace.points.Count;i++)
        {
            if(mean.Count<=i) mean.Add(trace.points[i]);
            else { var a=mean[i];var b=trace.points[i]; float f=1f/completed;
                a.momentum=Mathf.Lerp(a.momentum,b.momentum,f);a.speed=Mathf.Lerp(a.speed,b.speed,f);
                a.score=Mathf.Lerp(a.score,b.score,f);a.cap=Mathf.Lerp(a.cap,b.cap,f);mean[i]=a; }
        }
    }
    public string Summary(int tier,int horizon)
    {
        var a=arrivals[tier]; if(a.Count==0) return $"0% ? Not reached within {horizon} landings";
        var sorted=a.OrderBy(x=>x).ToArray(); float avg=(float)a.Average();
        float median=(sorted[(sorted.Length-1)/2]+sorted[sorted.Length/2])*0.5f;
        double sd=Math.Sqrt(a.Select(x=>(x-avg)*(x-avg)).Average());
        return $"{100f*a.Count/completed:0.#}% reached | conditional mean {avg:0.#}, median {median:0.#}, SD {sd:0.#}";
    }
}
