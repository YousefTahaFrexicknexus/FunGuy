
using UnityEngine;
using UnityEngine.Rendering;

public class PostProcessingPlatformController : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] Volume globalVolume;

    [Header("Profiles")]
    [SerializeField] VolumeProfile mobileProfile;
    [SerializeField] VolumeProfile pcProfile;

    [Header("Fallback")]
    [SerializeField] VolumeProfile defaultProfile;

    void Awake()
    {
        ApplyPlatformProfile();
    }

    void ApplyPlatformProfile()
    {
        if (globalVolume == null)
        {
            Debug.LogWarning("Post Processing Volume is not assigned.", this);
            return;
        }

        VolumeProfile selectedProfile = GetPlatformProfile();

        if (selectedProfile == null)
        {
            Debug.LogWarning("No Post Processing Profile assigned for this platform.", this);
            return;
        }

        globalVolume.sharedProfile = selectedProfile;
    }

    VolumeProfile GetPlatformProfile()
    {
        if (Application.isMobilePlatform)
            return mobileProfile != null ? mobileProfile : defaultProfile;

        if (Application.isConsolePlatform)
            return pcProfile != null ? pcProfile : defaultProfile;

        if (Application.isEditor || Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.LinuxPlayer)
            return pcProfile != null ? pcProfile : defaultProfile;

        return defaultProfile;
    }
}
