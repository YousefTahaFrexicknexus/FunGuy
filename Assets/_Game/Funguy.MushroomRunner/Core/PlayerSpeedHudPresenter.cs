using UnityEngine;
using UnityEngine.UI;

namespace Funguy.MushroomRunner
{
    [DisallowMultipleComponent]
    public sealed class PlayerSpeedHudPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("Movement motor sampled for the current player speed.")]
        private RunnerMovementMotor movementMotor;
        [SerializeField, Tooltip("Filled image used as the speed meter bar.")]
        private Image fillImage;
        [SerializeField, Tooltip("Static label text, usually SPEED.")]
        private Text labelText;
        [SerializeField, Tooltip("Numeric text showing the current speed.")]
        private Text valueText;
        [SerializeField, Tooltip("If enabled, vertical velocity is ignored and only planar speed is displayed.")]
        private bool usePlanarSpeed = true;
        [SerializeField, Tooltip("Extra multiplier applied before showing speed in the HUD.")]
        private float speedDisplayMultiplier = 1f;
        [SerializeField, Tooltip("Reference speed used to normalize the meter when no tuning profile is available.")]
        private float maxDisplaySpeed = 48f;
        [SerializeField, Tooltip("How quickly the displayed speed catches up to the live speed.")]
        private float smoothing = 10f;
        [SerializeField, Tooltip("Label shown next to the speed value.")]
        private string speedLabel = "SPEED";
        [SerializeField, Tooltip("Suffix appended to the numeric speed value.")]
        private string unitsSuffix = " u/s";
        [SerializeField, Tooltip("Meter color at low speed.")]
        private Color lowSpeedColor = new(0.33f, 0.81f, 0.65f, 0.95f);
        [SerializeField, Tooltip("Meter color at high speed.")]
        private Color highSpeedColor = new(0.98f, 0.56f, 0.24f, 0.98f);

        private float displayedSpeed;

        private void Reset()
        {
            if (movementMotor == null)
            {
                movementMotor = GetComponent<RunnerMovementMotor>();
            }
        }

        private void OnEnable()
        {
            displayedSpeed = MeasureTargetSpeed();
            RefreshUi();
        }

        private void OnValidate()
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

        private void Update()
        {
            float targetSpeed = MeasureTargetSpeed();
            float blendFactor = smoothing <= 0f
                ? 1f
                : 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);

            displayedSpeed = Mathf.Lerp(displayedSpeed, targetSpeed, blendFactor);
            RefreshUi();
        }

        public void Configure(RunnerMovementMotor motor, Image fill, Text label, Text value)
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

        private float MeasureTargetSpeed()
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

        private float ResolveReferenceSpeed()
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

            float profileReferenceSpeed = usePlanarSpeed
                ? tuningProfile.MaxSpeed
                : tuningProfile.MaxSpeed + tuningProfile.DashForce;

            return Mathf.Max(referenceSpeed, profileReferenceSpeed * speedDisplayMultiplier);
        }

        private void RefreshUi()
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
}
