using UnityEngine;
using UnityEngine.UI;

public class InGameSettings_Popup : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] Slider musicVolumeSlider;
    [SerializeField] Slider sfxVolumeSlider;

    void Start()
    {
        SetupSliders();
    }

    void OnDestroy()
    {
        musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
    }

    void SetupSliders()
    {
        musicVolumeSlider.minValue = 0f;
        musicVolumeSlider.maxValue = 1f;

        sfxVolumeSlider.minValue = 0f;
        sfxVolumeSlider.maxValue = 1f;

        // SetValueWithoutNotify prevents triggering the callbacks while loading.
        musicVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.SFXVolume);

        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
    }

    void OnMusicVolumeChanged(float _volume)
    {
        AudioManager.Instance.SetMusicVolume(_volume);
    }

    void OnSFXVolumeChanged(float _volume)
    {
        AudioManager.Instance.SetSFXVolume(_volume);
    }

    public void ClosePopup()
    {
        AudioManager.Instance.SaveVolumeSettings();
        gameObject.SetActive(false);
    }
}
