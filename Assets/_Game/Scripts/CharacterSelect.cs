using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Character carousel with a collision transition. Only one 3D character is on stage at a time.
/// Pressing Left / Right makes the next character CHARGE in from that side, SMASH into the one
/// standing there (a short freeze-frame + squash), and knock it flying out the other side while the
/// newcomer plows to the centre and settles on a spring. The Select button locks the choice in.
///
/// The collision is simulated by the script (momentum transfer, knockback arc, tumble), so the characters
/// do NOT need Rigidbodies or Colliders.
///
/// Setup: put this on an empty "Stage" object placed where the selected character should stand.
/// The characters slide along the Stage's RIGHT axis (the red arrow), so point the camera along its forward.
/// Drag your characters into the list (they can be anywhere in the scene, this script moves them).
/// </summary>
[DisallowMultipleComponent]
public class CharacterSelect : MonoBehaviour
{
    /// <summary>PlayerPrefs key the confirmed index is saved under (read it from any scene).</summary>
    public const string PrefsKey = "SelectedCharacter";

    [Header("Characters")]
    [Tooltip("The characters, in order. Their rotation/scale in the scene is their 'rest' pose.")]
    [SerializeField] private Transform[] characters;
    [SerializeField] private int startIndex = 0;
    [Tooltip("Left on the first goes to the last, and the other way round.")]
    [SerializeField] private bool wrapAround = true;
    [Tooltip("Pressing Left / Right during a hit finishes it instantly and starts the next one. Off = ignore presses until it settles.")]
    [SerializeField] private bool allowInterrupt = true;

    [Header("UI buttons (optional)")]
    [Tooltip("Assign here OR wire the buttons' OnClick to Previous / Next / Confirm yourself, not both.")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button selectButton;
    [SerializeField] TextMeshProUGUI charNameText;

    [Header("Charge")]
    [Tooltip("How far from the centre the new character starts (world units). Make it wider than the camera view so it enters from off-screen. Knocked-out characters are removed at 1.5x this distance.")]
    [SerializeField] private float spacing = 5f;
    [Tooltip("Seconds the new character takes to run in and hit. It accelerates the whole way, so it hits at full speed.")]
    [SerializeField] private float chargeTime = 0.28f;
    [Tooltip("Centre-to-centre distance at the moment of contact. Roughly the width of a character.")]
    [SerializeField] private float contactDistance = 1.1f;
    [Tooltip("Degrees the charging character leans into the hit.")]
    [SerializeField] private float chargeLean = 12f;

    [Header("Impact")]
    [Tooltip("Seconds both characters freeze on contact. This is what makes a hit feel heavy. 0.04 - 0.09 is good.")]
    [SerializeField] private float hitStopTime = 0.06f;
    [Tooltip("How far (world units) both characters tremble during the freeze.")]
    [SerializeField] private float hitShake = 0.04f;
    [Tooltip("Squash on contact (0 = none). Characters flatten, then spring back.")]
    [SerializeField, Range(0f, 0.6f)] private float squashAmount = 0.3f;
    [Tooltip("How fast the squash recovers.")]
    [SerializeField] private float squashRecover = 10f;

    [Header("Knockback (the character being hit)")]
    [Tooltip("Knockback speed as a fraction of the hit speed. Higher = it flies out harder.")]
    [SerializeField] private float knockTransfer = 0.9f;
    [Tooltip("Upward speed given to the knocked character, for a little arc.")]
    [SerializeField] private float knockUp = 4f;
    [Tooltip("Gravity on the knocked character (units/s squared).")]
    [SerializeField] private float knockGravity = 12f;
    [Tooltip("Spin around the up axis while flying (degrees per second).")]
    [SerializeField] private float knockSpin = 540f;
    [Tooltip("Tumble in the direction of travel while flying (degrees per second).")]
    [SerializeField] private float knockTumble = 360f;

    [Header("Newcomer after the hit")]
    [Tooltip("Fraction of the hit speed the new character keeps, so it plows toward the centre.")]
    [SerializeField, Range(0f, 1f)] private float momentumKept = 0.4f;
    [Tooltip("How hard the spring pulls it to the centre.")]
    [SerializeField] private float settleStiffness = 260f;
    [Tooltip("0 = bounces a lot, 1 = no overshoot. 0.5 - 0.6 gives a small bounce past the centre.")]
    [SerializeField, Range(0.1f, 1.5f)] private float settleDamping = 0.55f;

    [Header("Idle & confirm")]
    [Tooltip("Slow turntable spin of the character on stage (degrees per second). 0 = off.")]
    [SerializeField] private float idleSpinSpeed = 25f;
    [Tooltip("How much the character pops (as a fraction of its size) when confirmed. 0 = off.")]
    [SerializeField] private float confirmPunch = 0.15f;
    [SerializeField] private float confirmPunchTime = 0.3f;
    [Tooltip("After Confirm, ignore Left / Right / Select until Unlock() is called.")]
    [SerializeField] private bool lockAfterConfirm = true;
    [Tooltip("Save the confirmed index so the next scene can read it with CharacterSelect.SavedIndex.")]
    [SerializeField] private bool saveToPlayerPrefs = true;

    [Header("Impact feedback (optional)")]
    [Tooltip("Played at the point of contact.")]
    [SerializeField] private ParticleSystem impactParticles;

    [Header("Events")]
    [Tooltip("Fired when the on-stage character changes (as the switch STARTS). Use it to update name / stats UI.")]
    public UnityEvent<int> onCharacterChanged;
    [Tooltip("Fired at the exact moment of the hit. Hook camera shake, a sound, a flash...")]
    public UnityEvent onImpact;
    [Tooltip("Fired when the Select button is pressed, with the chosen index.")]
    public UnityEvent<int> onCharacterConfirmed;

    /// <summary>Same as onCharacterConfirmed, with the chosen character object too.</summary>
    public event Action<int, Transform> Confirmed;
    /// <summary>Raised at the moment of the hit, with the world position of the contact point.</summary>
    public event Action<Vector3> Impacted;

    /// <summary>Index of the character currently on stage (and the one that will be confirmed).</summary>
    public int SelectedIndex { get; private set; }
    /// <summary>The character currently on stage.</summary>
    public Transform SelectedCharacter => characters != null && characters.Length > 0 ? characters[SelectedIndex] : null;
    /// <summary>True once Select has been pressed (until Unlock).</summary>
    public bool IsConfirmed { get; private set; }
    /// <summary>True while a hit / switch is playing.</summary>
    public bool IsMoving => phase != Phase.None;
    /// <summary>The index saved by the last confirmed selection (0 if none). Works from any scene.</summary>
    public static int SavedIndex => PlayerPrefs.GetInt(PrefsKey, 0);

    private enum Phase { None, Charging, HitStop, Settling }

    private Quaternion[] baseRotation;
    private Vector3[] baseScale;

    private Phase phase = Phase.None;
    private int incoming, outgoing;
    private int direction;            // +1 = newcomer arrives from the right, -1 = from the left

    private float chargeElapsed;
    private float hitTimer;
    private float settleElapsed;

    // Newcomer: distance from the centre along its arrival side (positive = still on that side).
    private float inOffset, inVel, inSquash, inRoll;

    // Knocked-out character: simple ballistic body.
    private Vector3 outPosition, outVelocity;
    private float outSquash, outRoll, outYaw, outFlightTime;
    private bool outGone;

    private float idleYaw;
    private float punchTimer;

    private bool IsLocked => IsConfirmed && lockAfterConfirm;
    private Vector3 Center => transform.position;
    private float ContactDistance => Mathf.Min(contactDistance, spacing * 0.9f);

    private void Awake()
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogWarning("CharacterSelect: add your characters to the list.", this);
            enabled = false;
            return;
        }

        baseRotation = new Quaternion[characters.Length];
        baseScale = new Vector3[characters.Length];

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] == null)
            {
                Debug.LogWarning("CharacterSelect: the character list has an empty slot.", this);
                enabled = false;
                return;
            }

            baseRotation[i] = characters[i].rotation;
            baseScale[i] = characters[i].localScale;
            characters[i].gameObject.SetActive(false);
        }

        SelectedIndex = Mathf.Clamp(startIndex, 0, characters.Length - 1);
        characters[SelectedIndex].gameObject.SetActive(true);
        charNameText.text = characters[SelectedIndex].name;
        Pose(SelectedIndex, Center, Vector3.one, 0f, 0f);
        RefreshButtons();
    }

    private void OnEnable()
    {
        if (leftButton != null) leftButton.onClick.AddListener(Previous);
        if (rightButton != null) rightButton.onClick.AddListener(Next);
        if (selectButton != null) selectButton.onClick.AddListener(Confirm);

        ResetCharacterSelectControls();
    }

    private void OnDisable()
    {
        if (leftButton != null) leftButton.onClick.RemoveListener(Previous);
        if (rightButton != null) rightButton.onClick.RemoveListener(Next);
        if (selectButton != null) selectButton.onClick.RemoveListener(Confirm);
    }

    private void Update()
    {
        // Unscaled time so the menu animates even if the game is paused (timeScale = 0).
        float rawDt = Time.unscaledDeltaTime;
        idleYaw = (idleYaw + idleSpinSpeed * rawDt) % 360f;

        // Clamp the step so a frame hitch can't make the spring or the knockback explode.
        float dt = Mathf.Min(rawDt, 0.033f);

        switch (phase)
        {
            case Phase.Charging: TickCharge(dt); break;
            case Phase.HitStop: TickHitStop(dt); break;
            case Phase.Settling: TickSettle(dt); break;
            default: TickIdle(rawDt); break;
        }
    }

    // ---------- Public API (hook these to buttons, keys, gamepad...) ----------

    /// <summary>Right button: the next character charges in from the right and knocks the current one out to the left.</summary>
    public void Next() => Step(+1);

    /// <summary>Left button: the previous character charges in from the left and knocks the current one out to the right.</summary>
    public void Previous() => Step(-1);

    /// <summary>Select button: locks in the character that is on stage right now.</summary>
    public void Confirm()
    {
        if (IsLocked) return;

        if (phase != Phase.None) FinishTransition();   // never confirm mid-hit: settle first

        IsConfirmed = true;
        punchTimer = confirmPunch > 0f ? confirmPunchTime : 0f;

        if (saveToPlayerPrefs)
        {
            PlayerPrefs.SetInt(PrefsKey, SelectedIndex);
            PlayerPrefs.Save();
        }

        RefreshButtons();

        onCharacterConfirmed?.Invoke(SelectedIndex);
        Confirmed?.Invoke(SelectedIndex, SelectedCharacter);

        GameplayEvents.OnCharacterSelected?.Invoke(SelectedIndex);
    }

    /// <summary>Lets the player choose again (e.g. from a Back button after Confirm).</summary>
    public void Unlock()
    {
        IsConfirmed = false;
        RefreshButtons();
    }

    // ---------- Transition ----------

    private void Step(int dir)
    {
        if (IsLocked) return;

        if (phase != Phase.None)
        {
            if (!allowInterrupt) return;
            FinishTransition();   // tapping fast: settle the running hit instantly
        }

        int target = SelectedIndex + dir;
        if (target < 0 || target >= characters.Length)
        {
            if (!wrapAround) return;
            target = (target + characters.Length) % characters.Length;
        }

        if (target == SelectedIndex) return;

        outgoing = SelectedIndex;
        incoming = target;
        direction = dir;

        chargeElapsed = 0f;
        settleElapsed = 0f;
        inOffset = spacing;
        inVel = 0f;
        inSquash = 0f;
        inRoll = 0f;

        outPosition = Center;
        outVelocity = Vector3.zero;
        outSquash = 0f;
        outRoll = 0f;
        outYaw = 0f;
        outFlightTime = 0f;
        outGone = false;

        SelectedIndex = target;
        characters[incoming].gameObject.SetActive(true);
        phase = Phase.Charging;
        PoseBoth(0f);   // place it off-stage now, so it never flashes at the centre for a frame
        charNameText.text = characters[SelectedIndex].name;

        RefreshButtons();
        onCharacterChanged?.Invoke(SelectedIndex);
    }

    private void TickCharge(float dt)
    {
        chargeElapsed += dt;
        float p = chargeElapsed / Mathf.Max(0.05f, chargeTime);

        if (p >= 1f)
        {
            Impact();
            return;
        }

        // Constant acceleration from rest: distance covered grows with p squared, so it hits at full speed.
        float contact = ContactDistance;
        inOffset = spacing - (spacing - contact) * p * p;
        inRoll = direction * chargeLean * p;

        PoseBoth(0f);
    }

    private void Impact()
    {
        float contact = ContactDistance;

        // Speed at the end of the charge: derivative of the p^2 curve at p = 1.
        float impactSpeed = 2f * (spacing - contact) / Mathf.Max(0.05f, chargeTime);

        inOffset = contact;
        inVel = -impactSpeed * momentumKept;      // the newcomer keeps some speed toward the centre

        Vector3 away = transform.right * -direction;
        outVelocity = away * (impactSpeed * knockTransfer) + Vector3.up * knockUp;

        inSquash = squashAmount;
        outSquash = squashAmount;

        phase = Phase.HitStop;
        hitTimer = hitStopTime;

        Vector3 point = Center + transform.right * (direction * contact * 0.5f);
        if (impactParticles != null)
        {
            impactParticles.transform.position = point;
            impactParticles.Play();
        }

        onImpact?.Invoke();
        Impacted?.Invoke(point);

        PoseBoth(hitShake);
    }

    private void TickHitStop(float dt)
    {
        hitTimer -= dt;
        PoseBoth(hitShake);   // frozen, trembling

        if (hitTimer <= 0f) phase = Phase.Settling;
    }

    private void TickSettle(float dt)
    {
        settleElapsed += dt;

        // Newcomer: damped spring toward the centre. It can slide a little past it and bounce back.
        float damping = 2f * settleDamping * Mathf.Sqrt(settleStiffness);
        inVel += (-settleStiffness * inOffset - damping * inVel) * dt;
        inOffset += inVel * dt;

        // Knocked-out character: simple ballistic flight with spin and tumble.
        if (!outGone)
        {
            outVelocity += Vector3.down * knockGravity * dt;
            outPosition += outVelocity * dt;
            outFlightTime += dt;
            outRoll += direction * knockTumble * dt;
            outYaw += direction * knockSpin * dt;

            float along = Vector3.Dot(outPosition - Center, transform.right * -direction);
            if (along > spacing * 1.5f || outFlightTime > 1.5f)
            {
                outGone = true;
                characters[outgoing].gameObject.SetActive(false);
            }
        }

        float recover = Mathf.Exp(-squashRecover * dt);
        inSquash *= recover;
        outSquash *= recover;
        inRoll *= Mathf.Exp(-12f * dt);

        PoseBoth(0f);

        bool settled = Mathf.Abs(inOffset) < 0.004f && Mathf.Abs(inVel) < 0.05f;
        if ((outGone && settled) || settleElapsed > 2.5f) FinishTransition();
    }

    private void TickIdle(float dt)
    {
        float punch = 0f;
        if (punchTimer > 0f)
        {
            punchTimer -= dt;
            float progress = 1f - Mathf.Clamp01(punchTimer / Mathf.Max(0.01f, confirmPunchTime));
            punch = confirmPunch * Mathf.Sin(Mathf.PI * progress);
        }

        Pose(SelectedIndex, Center, Vector3.one * (1f + punch), 0f, 0f);
    }

    private void FinishTransition()
    {
        if (phase == Phase.None) return;
        phase = Phase.None;

        // Leave both in a clean pose: the knocked-out one is hidden but ready for its next entrance.
        characters[outgoing].gameObject.SetActive(false);
        Pose(outgoing, Center, Vector3.one, 0f, 0f);
        Pose(incoming, Center, Vector3.one, 0f, 0f);

        inOffset = 0f;
        inVel = 0f;
        inSquash = 0f;
        inRoll = 0f;
        outSquash = 0f;
        outRoll = 0f;
        outYaw = 0f;
    }

    // ---------- Posing ----------

    private void PoseBoth(float shake)
    {
        Vector3 inPos = Center + transform.right * (direction * inOffset);
        Vector3 outPos = outPosition;

        if (shake > 0f)
        {
            inPos += UnityEngine.Random.insideUnitSphere * shake;
            outPos += UnityEngine.Random.insideUnitSphere * shake;
        }

        Pose(incoming, inPos, Squash(inSquash), 0f, inRoll);
        if (!outGone) Pose(outgoing, outPos, Squash(outSquash), outYaw, outRoll);
    }

    /// <summary>Flatten and widen: 'amount' 0 = normal. Works on local axes, so it stays correct as the character spins.</summary>
    private static Vector3 Squash(float amount)
    {
        amount = Mathf.Clamp(amount, -0.6f, 0.6f);
        return new Vector3(1f + amount * 0.5f, 1f - amount, 1f + amount * 0.5f);
    }

    private void Pose(int index, Vector3 position, Vector3 scaleMultiplier, float extraYaw, float rollDegrees)
    {
        Transform c = characters[index];

        Quaternion lean = Quaternion.AngleAxis(rollDegrees, transform.forward);   // tilt toward / away from the hit

        c.SetPositionAndRotation(position, lean);
        c.localScale = Vector3.Scale(baseScale[index], scaleMultiplier);
    }

    /// <summary>Call this when the character select opens again: makes Left / Right / Select usable again.</summary>
    public void ResetCharacterSelectControls()
    {
        if (phase != Phase.None) FinishTransition();   // settle any hit still playing
        IsConfirmed = false;                           // lifts the lock set by Confirm()
        punchTimer = 0f;                               // cancel the leftover confirm pop
        RefreshButtons();                              // Left / Right / Select become interactable again
    }

    private void RefreshButtons()
    {
        bool canMove = !IsLocked && characters.Length > 1;

        if (leftButton != null) leftButton.interactable = canMove && (wrapAround || SelectedIndex > 0);
        if (rightButton != null) rightButton.interactable = canMove && (wrapAround || SelectedIndex < characters.Length - 1);
        if (selectButton != null) selectButton.interactable = !IsLocked;
    }
}

public enum Characters
{
    Froggy,
    ChirpyChick,
    NotSoFunGi,
}