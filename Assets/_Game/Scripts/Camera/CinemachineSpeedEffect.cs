using UnityEngine;
using Unity.Cinemachine;
using Funguy.MushroomRunner;

public class CinemachineSpeedEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CinemachineCamera cinemachineCamera;
    [SerializeField] CinemachineThirdPersonFollow thirdPersonFollow;
    [SerializeField] CinemachineBasicMultiChannelPerlin cameraNoise;
    [SerializeField] Rigidbody rb;
    [SerializeField] CinemachineSpeedEffect speedEffect;

    [Header("Speed Range")]
    [SerializeField] float minimumSpeed = 5f;
    [SerializeField] float maximumSpeed = 25f;

    [Header("Field of View")]
    [SerializeField] float normalFOV = 60f;
    [SerializeField] float maximumFOV = 78f;

    [Header("Camera Distance")]
    [SerializeField] float normalDistance = 6f;
    [SerializeField] float maximumDistance = 8f;

    [Header("Camera Noise")]
    [SerializeField] float maximumNoiseAmplitude = 0.35f;
    [SerializeField] float maximumNoiseFrequency = 1.5f;

    [Header("Smoothing")]
    [SerializeField] float enterSpeed = 4f;
    [SerializeField] float exitSpeed = 2f;

    [SerializeField] float currentIntensity;

    void Awake()
    {
        SubscribeToGameplayEvents();
    }

    void Update()
    {
        float travelSpeed = Mathf.Max(0f, rb.linearVelocity.z);
        speedEffect.SetSpeed(travelSpeed);
    }

    void SubscribeToGameplayEvents()
    {
        GameplayEvents.GameplayReset += ResetCinemachine;
    }

    /// <summary>
    /// Call continuously using the player's current movement speed.
    /// </summary>
    public void SetSpeed(float _speed)
    {
        float targetIntensity = Mathf.InverseLerp( minimumSpeed, maximumSpeed, _speed);
        float smoothingSpeed = targetIntensity > currentIntensity ? enterSpeed : exitSpeed;

        currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, smoothingSpeed * Time.deltaTime );

        ApplyEffect(currentIntensity);
    }

    void ApplyEffect(float _intensity)
    {
        _intensity = Mathf.Clamp01(_intensity);

        // Gives the transition a stronger, more cinematic shape.
        float curvedIntensity = Mathf.SmoothStep(0f, 1f, _intensity);

        ApplyFOV(curvedIntensity);
        ApplyCameraDistance(curvedIntensity);
        ApplyNoise(curvedIntensity);
    }

    void ApplyFOV(float _intensity)
    {
        LensSettings lens = cinemachineCamera.Lens;

        lens.FieldOfView = Mathf.Lerp(normalFOV, maximumFOV, _intensity );

        cinemachineCamera.Lens = lens;
    }

    void ApplyCameraDistance(float _intensity)
    {
        if (thirdPersonFollow == null)
        {
            return;
        }

        thirdPersonFollow.CameraDistance = Mathf.Lerp(normalDistance, maximumDistance, _intensity );
    }

    void ApplyNoise(float _intensity)
    {
        if (cameraNoise == null)
        {
            return;
        }

        cameraNoise.AmplitudeGain = maximumNoiseAmplitude * _intensity;
        cameraNoise.FrequencyGain = Mathf.Lerp(0.5f, maximumNoiseFrequency, _intensity );
    }

    public void ResetImmediately()
    {
        currentIntensity = 0f;
        ApplyEffect(0f);
    }

    public void ResetCinemachine()
    {
        var brain = Camera.main.GetComponent<CinemachineBrain>();

        brain.enabled = false;
        brain.enabled = true;
    }
}