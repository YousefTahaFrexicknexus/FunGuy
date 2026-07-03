using System;

namespace Funguy.MushroomRunner
{
    public readonly struct RunScoreSnapshot : IEquatable<RunScoreSnapshot>
    {
        public RunScoreSnapshot(
            int score,
            int comboHits,
            float multiplier,
            bool hasActiveCombo,
            bool isComboBreakPending,
            float comboBreakTimeRemainingSeconds,
            float comboBreakDelaySeconds,
            bool isAirborne,
            float currentAirtimeSeconds,
            float rewardedAirtimeSeconds,
            bool hasQualifiedAirtime,
            bool hasQualifiedAirtimeMultiplier)
        {
            Score = score;
            ComboHits = comboHits;
            Multiplier = multiplier;
            HasActiveCombo = hasActiveCombo;
            IsComboBreakPending = isComboBreakPending;
            ComboBreakTimeRemainingSeconds = comboBreakTimeRemainingSeconds;
            ComboBreakDelaySeconds = comboBreakDelaySeconds;
            IsAirborne = isAirborne;
            CurrentAirtimeSeconds = currentAirtimeSeconds;
            RewardedAirtimeSeconds = rewardedAirtimeSeconds;
            HasQualifiedAirtime = hasQualifiedAirtime;
            HasQualifiedAirtimeMultiplier = hasQualifiedAirtimeMultiplier;
        }

        public int Score { get; }
        public int ComboHits { get; }
        public float Multiplier { get; }
        public bool HasActiveCombo { get; }
        public bool IsComboBreakPending { get; }
        public float ComboBreakTimeRemainingSeconds { get; }
        public float ComboBreakDelaySeconds { get; }
        public bool IsAirborne { get; }
        public float CurrentAirtimeSeconds { get; }
        public float RewardedAirtimeSeconds { get; }
        public bool HasQualifiedAirtime { get; }
        public bool HasQualifiedAirtimeMultiplier { get; }

        public bool Equals(RunScoreSnapshot other)
        {
            return Score == other.Score
                && ComboHits == other.ComboHits
                && Multiplier.Equals(other.Multiplier)
                && HasActiveCombo == other.HasActiveCombo
                && IsComboBreakPending == other.IsComboBreakPending
                && ComboBreakTimeRemainingSeconds.Equals(other.ComboBreakTimeRemainingSeconds)
                && ComboBreakDelaySeconds.Equals(other.ComboBreakDelaySeconds)
                && IsAirborne == other.IsAirborne
                && CurrentAirtimeSeconds.Equals(other.CurrentAirtimeSeconds)
                && RewardedAirtimeSeconds.Equals(other.RewardedAirtimeSeconds)
                && HasQualifiedAirtime == other.HasQualifiedAirtime
                && HasQualifiedAirtimeMultiplier == other.HasQualifiedAirtimeMultiplier;
        }

        public override bool Equals(object obj)
        {
            return obj is RunScoreSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(Score);
            hash.Add(ComboHits);
            hash.Add(Multiplier);
            hash.Add(HasActiveCombo);
            hash.Add(IsComboBreakPending);
            hash.Add(ComboBreakTimeRemainingSeconds);
            hash.Add(ComboBreakDelaySeconds);
            hash.Add(IsAirborne);
            hash.Add(CurrentAirtimeSeconds);
            hash.Add(RewardedAirtimeSeconds);
            hash.Add(HasQualifiedAirtime);
            hash.Add(HasQualifiedAirtimeMultiplier);
            return hash.ToHashCode();
        }
    }
}
