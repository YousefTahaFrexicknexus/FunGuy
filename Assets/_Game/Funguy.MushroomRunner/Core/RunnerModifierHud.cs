using UnityEngine;

/// <summary>Two independently visible radial countdowns. Empty icon slots can be assigned later.</summary>
public sealed class RunnerModifierHud : MonoBehaviour
{
    [System.Serializable]
    public sealed class Slot
    {
        public GameObject root;
        public UnityEngine.UI.Image ring;
        public UnityEngine.UI.Image icon;
        public Sprite iconOverride;
    }

    [SerializeField] RunnerModifierController source;
    [SerializeField] Slot chilling = new();
    [SerializeField] Slot perfect = new();

    void OnEnable()
    {
        MushroomRunnerEvents.PlayerRegistered += OnPlayerRegistered;
        if (source == null) source = FindFirstObjectByType<RunnerModifierController>();
        Refresh();
    }

    void OnDisable() => MushroomRunnerEvents.PlayerRegistered -= OnPlayerRegistered;
    void Update() => Refresh();
    void OnPlayerRegistered(PlayerRegisteredEvent data)
    {
        source = data.MovementMotor != null ? data.MovementMotor.Modifiers : null;
        Refresh();
    }

    public void Bind(RunnerModifierController controller) { source = controller; Refresh(); }
    public void Refresh()
    {
        RefreshSlot(chilling, MushroomModifierType.Chilling);
        RefreshSlot(perfect, MushroomModifierType.PerfectLandings);
    }

    void RefreshSlot(Slot slot, MushroomModifierType type)
    {
        if (slot == null || slot.root == null) return;
        bool active = source != null && source.IsActive(type);
        if (slot.root.activeSelf != active) slot.root.SetActive(active);
        if (!active) return;
        if (slot.ring != null) slot.ring.fillAmount = source.GetNormalizedRemaining(type);
        if (slot.icon != null)
        {
            MushroomModifierDefinition definition = source.GetDefinition(type);
            slot.icon.sprite = slot.iconOverride != null ? slot.iconOverride : definition != null ? definition.Icon : null;
            slot.icon.enabled = slot.icon.sprite != null;
        }
    }
}
