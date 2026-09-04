using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerSpeedHudPresenter : MonoBehaviour
{
    [SerializeField, Tooltip("Movement motor sampled for the current player speed.")]
    RunnerMovementMotor movementMotor;
    [SerializeField, Tooltip("Filled image used as the speed meter bar.")]
    Image fillImage;
    [SerializeField, Tooltip("Static label text, usually SPEED.")]
    TextMeshProUGUI labelText;
    [SerializeField, Tooltip("Numeric text showing the current speed.")]
    TextMeshProUGUI valueText;
    [SerializeField, Tooltip("If enabled, vertical velocity is ignored and only planar speed is displayed.")]
    bool usePlanarSpeed = true;
    [SerializeField, Tooltip("Extra multiplier applied before showing speed in the HUD.")]
    float speedDisplayMultiplier = 1f;
    [SerializeField, Tooltip("Reference speed used to normalize the meter when no tuning profile is available.")]
    float maxDisplaySpeed = 48f;
    [SerializeField, Tooltip("How quickly the displayed speed catches up to the live speed.")]
    float smoothing = 10f;
    [SerializeField, Tooltip("Label shown next to the speed value.")]
    string speedLabel = "SPEED";
    [SerializeField, Tooltip("Suffix appended to the numeric speed value.")]
    string unitsSuffix = " KM/H";
    [SerializeField, Tooltip("Meter color at low speed.")]
    Color lowSpeedColor = new(0.33f, 0.81f, 0.65f, 0.95f);
    [SerializeField, Tooltip("Meter color at high speed.")]
    Color highSpeedColor = new(0.98f, 0.56f, 0.24f, 0.98f);

    float displayedSpeed;

    void Reset()
    {
        if (movementMotor == null)
        {
            movementMotor = GetComponent<RunnerMovementMotor>();
        }
    }

    void OnEnable()
    {
        displayedSpeed = MeasureTargetSpeed();
        RefreshUi();
    }

    void OnValidate()
    {
        speedDisplayMultiplier = Mathf.Max(0.01f, speedDisplayMultiplier);
        maxDisplaySpeed = Mathf.Max(0.1f, maxDisplaySpeed);
        smoothing = Mathf.Max(0f, smoothing);

        if (string.IsNullOrWhiteSpace(speedLabel))
        {
            speedLabel = "SPEED";
        }

        if (labelText != null)
        {
            labelText.text = speedLabel;
        }
    }

    void Update()
    {
        float targetSpeed = MeasureTargetSpeed();
        float blendFactor = smoothing <= 0f ? 1f : 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);

        displayedSpeed = Mathf.Lerp(displayedSpeed, targetSpeed, blendFactor);
        RefreshUi();
    }

    public void Configure(RunnerMovementMotor motor, Image fill, TextMeshProUGUI label, TextMeshProUGUI value)
    {
        movementMotor = motor;
        fillImage = fill;
        labelText = label;
        valueText = value;
        RefreshUi();
    }

    public void SetMovementMotor(RunnerMovementMotor motor)
    {
        movementMotor = motor;
    }

    float MeasureTargetSpeed()
    {
        if (movementMotor == null)
        {
            return 0f;
        }

        Vector3 velocity = movementMotor.Velocity;

        if (usePlanarSpeed)
        {
            velocity = Vector3.ProjectOnPlane(velocity, movementMotor.UpDirection);
        }

        return velocity.magnitude * speedDisplayMultiplier;
    }

    float ResolveReferenceSpeed()
    {
        float referenceSpeed = Mathf.Max(0.1f, maxDisplaySpeed) * speedDisplayMultiplier;

        if (movementMotor == null)
        {
            return referenceSpeed;
        }

        MovementTuningProfile tuningProfile = movementMotor.TuningProfile;

        if (tuningProfile == null)
        {
            return referenceSpeed;
        }

        float profileReferenceSpeed = usePlanarSpeed ? tuningProfile.MaxSpeed : tuningProfile.MaxSpeed + tuningProfile.DashForce;

        return Mathf.Max(referenceSpeed, profileReferenceSpeed * speedDisplayMultiplier);
    }

    void RefreshUi()
    {
        if (fillImage == null || valueText == null || labelText == null)
        {
            return;
        }

        float normalizedSpeed = Mathf.Clamp01(displayedSpeed / ResolveReferenceSpeed());
        fillImage.fillAmount = normalizedSpeed;
        fillImage.color = Color.Lerp(lowSpeedColor, highSpeedColor, normalizedSpeed);
        labelText.text = speedLabel;
        valueText.text = $"{displayedSpeed:0.0}{unitsSuffix}";
    }
}