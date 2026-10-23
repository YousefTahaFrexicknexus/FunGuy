using UnityEngine;

public enum MushroomModifierType { Chilling, PerfectLandings }
public enum MushroomModifierCategory { Buff, Debuff }

[CreateAssetMenu(menuName = "Mushroom Runner/Timed Modifier", fileName = "MushroomModifier")]
public sealed class MushroomModifierDefinition : ScriptableObject
{
    [SerializeField] MushroomModifierType effectType;
    [SerializeField] MushroomModifierCategory category;
    [SerializeField, Tooltip("While this modifier is active, reject new debuff pickups. Does not remove existing debuffs.")]
    bool immuneToDebuffs;
    [SerializeField, Tooltip("Remove active debuffs when this modifier is applied or refreshed. Does not prevent later debuffs.")]
    bool cleanseDebuffsOnPickup;
    [SerializeField, Min(0.01f)] float duration = 10f;
    [SerializeField, Range(0.01f, 1f), Tooltip("Chilling only. Applied once to final horizontal movement, including speed limits.")]
    float speedMultiplier = 0.7f;
    [SerializeField] Sprite icon;
    [SerializeField] GameObject playerVfxPrefab;
    [SerializeField] AudioClip activationSound;
    [SerializeField, Range(0f, 1f)] float soundVolume = 0.6f;

    public MushroomModifierType EffectType => effectType;
    public MushroomModifierCategory Category => category;
    public bool IsDebuff => category == MushroomModifierCategory.Debuff;
    public bool ImmuneToDebuffs => immuneToDebuffs;
    public bool CleanseDebuffsOnPickup => cleanseDebuffsOnPickup;
    public float Duration => duration;
    public float SpeedMultiplier => Mathf.Clamp(speedMultiplier, 0.01f, 1f);
    public Sprite Icon => icon;
    public GameObject PlayerVfxPrefab => playerVfxPrefab;
    public AudioClip ActivationSound => activationSound;
    public float SoundVolume => soundVolume;
    public bool IsValid => (effectType == MushroomModifierType.Chilling || effectType == MushroomModifierType.PerfectLandings)
        && duration > 0f && !float.IsNaN(duration) && !float.IsInfinity(duration)
        && !float.IsNaN(speedMultiplier) && !float.IsInfinity(speedMultiplier);
}
