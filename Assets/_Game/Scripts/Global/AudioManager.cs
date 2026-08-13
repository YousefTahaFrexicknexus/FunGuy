using UnityEngine;

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    #region Instance | Singleton
    private static AudioManager _instance;
    public static AudioManager Instance
	{
		get
		{
			if (!_instance)
				_instance = FindAnyObjectByType<AudioManager>();

			return _instance;
		}
	}

    private void DontDestroy() 
    { 
        if (Instance != null && Instance != this) 
        { 
            Destroy(this); 
        } 
        else 
        { 
            _instance = this; 
            DontDestroyOnLoad(this.gameObject);
        } 
    }
    #endregion --- Instance | Singleton ---

#region Consts
    public static string SOUND_SETTINGS_KEY = "soundsSettings";
    public static string MUSIC_SETTINGS_KEY = "musicSettings";

    const string MusicVolumeKey = "MusicVolume";
    const string SFXVolumeKey = "SFXVolume";

    const string MusicMixerParameter = "MusicVolume";
    const string SFXMixerParameter = "SFXVolume";
#endregion --- Consts ---

#region Vars
    [TabGroup("General"), Header("Main Components"), Space]
    [TabGroup("General"), SerializeField] AudioSource BGM_AudioSource;
    [TabGroup("General"), SerializeField] AudioSource SFX_AudioSource;

    [TabGroup("General"), Header("Settings"), Space]
    [TabGroup("General"), SerializeField] bool isSoundActive;
    [TabGroup("General"), SerializeField] bool isMusicActive;

    [TabGroup("SFX"), Header("SFX Parameters")]
    [TabGroup("SFX"), SerializeField] List<AudioElement> SFX_AudioElements;

    [TabGroup("BGM"), Header("BGM Parameters")]
    [TabGroup("BGM"), SerializeField] bool isBGMTransitioning;
    [TabGroup("BGM"), SerializeField] float transitionDuration = 1;
    [TabGroup("BGM"), SerializeField] float maxVolume = 1;
    [TabGroup("BGM"), SerializeField]  List<AudioElement> BGM_AudioElements;

    [Header("Audio Mixer")]
    [SerializeField] AudioMixer audioMixer;

    public float MusicVolume { get; private set; } = 1f;
    public float SFXVolume { get; private set; } = 1f;
#endregion --- Vars ---

    void Awake()
    {
        DontDestroy();
    }

    void Start()
    {
        InitSavedSettings();
        LoadVolumeSettings();
        // PlayMusic(BGM_AudioElements.FirstOrDefault().audioName);
    }

    public AudioElement GetSFX_ByName(string name)
    {
        if(SFX_AudioElements.Count == 0)
            return null;

        return SFX_AudioElements.FirstOrDefault(audioElement => audioElement.audioName == name);
    }

    public AudioElement GetBGM_ByName(string name)
    {
        if(BGM_AudioElements.Count == 0)
            return null;

        return BGM_AudioElements.FirstOrDefault(audioElement => audioElement.audioName == name);
    }

    public void PlaySFX(string _name, float _volume = 1.0f, bool _isOneShot = true)
    {
        // Debug.Log("SFX name: " + _name);

        if(!isSoundActive)
        {
            return;
        }

        AudioElement audioElement = GetSFX_ByName(_name);

        if (audioElement == null)
            return;

        if(_isOneShot)
        {
            SFX_AudioSource.PlayOneShot(audioElement.audioClip, _volume);
        }
        else
        {
            SFX_AudioSource.clip = audioElement.audioClip;
            SFX_AudioSource.volume = _volume;
            SFX_AudioSource.Play();
        }
    }

    public void PlayMusic(string _trackName, bool _forceTransition = false)
    {
        Debug.Log($"PlayMusic called, Track name: {_trackName}");

        if(!isMusicActive)
        {
            return;
        }

        if(_forceTransition)
            isBGMTransitioning = false;

        if(isBGMTransitioning)
            return;
        
        isBGMTransitioning = true;

        AudioElement newTrack = GetBGM_ByName(_trackName);

        Debug.Log($"newTrack null?: {newTrack == null}");

        if(newTrack == null)
        {
            Debug.LogWarning("AudioElement not found or is invalid.");
            isBGMTransitioning = false;
            return;
        }

        if(BGM_AudioSource.isPlaying)
        {
            StartCoroutine(TransitionToNewTrack(newTrack, transitionDuration));
        }
        else
        {
            BGM_AudioSource.clip = newTrack.audioClip;
            BGM_AudioSource.volume = 0;
            BGM_AudioSource.Play();
            BGM_AudioSource.DOFade(maxVolume, transitionDuration).OnComplete(()=>
            {
                isBGMTransitioning = false;
            });
        }
    }

    public void FadeInMusic()
    {
        BGM_AudioSource.DOFade(maxVolume, transitionDuration);
    }

    public void FadeOutMusic()
    {
        BGM_AudioSource.DOFade(0, transitionDuration);
    }

    private IEnumerator TransitionToNewTrack(AudioElement newTrack, float fadeDuration)
    {
        yield return BGM_AudioSource.DOFade(0, fadeDuration / 2).WaitForCompletion();
        
        BGM_AudioSource.clip = newTrack.audioClip;
        BGM_AudioSource.Play();

        yield return BGM_AudioSource.DOFade(maxVolume, fadeDuration / 2).WaitForCompletion();

        isBGMTransitioning = false;
    }

    void InitSavedSettings()
    {
        isSoundActive = Get_SoundSettings();
        isMusicActive = Get_MusicSettings();

        Set_SoundSettings(Get_SoundSettings());
        Set_MusicSettings(Get_MusicSettings());
    }

    public void Set_SoundSettings(bool _state)
    {
        PlayerPrefs.SetInt(SOUND_SETTINGS_KEY, _state ? 1 : 0);
        isSoundActive = _state;
    }

    public void Set_MusicSettings(bool _state)
    {
        PlayerPrefs.SetInt(MUSIC_SETTINGS_KEY, _state ? 1 : 0);
        isMusicActive = _state;

        BGM_AudioSource.volume = isMusicActive ? maxVolume : 0;
    }

    public bool Get_SoundSettings()
    {
        return PlayerPrefs.GetInt(SOUND_SETTINGS_KEY, 1) == 1 ? true : false;
    }

    public bool Get_MusicSettings()
    {
        return PlayerPrefs.GetInt(MUSIC_SETTINGS_KEY, 1) == 1 ? true : false;
    }

    public void SaveVolumeSettings()
    {
        PlayerPrefs.Save();
    }

    #region Volume
    public void SetMusicVolume(float _volume)
    {
        MusicVolume = Mathf.Clamp01(_volume);

        SetMixerVolume(MusicMixerParameter, MusicVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
    }

    public void SetSFXVolume(float _volume)
    {
        SFXVolume = Mathf.Clamp01(_volume);

        SetMixerVolume(SFXMixerParameter, SFXVolume);
        PlayerPrefs.SetFloat(SFXVolumeKey, SFXVolume);
    }

    void SetMixerVolume(string _parameterName, float _volume)
    {
        // Prevent Log10(0).
        float decibels = _volume <= 0.0001f ? -80f : Mathf.Log10(_volume) * 20f;

        audioMixer.SetFloat(_parameterName, decibels);
    }

    void LoadVolumeSettings()
    {
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        SFXVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);

        SetMixerVolume(MusicMixerParameter, MusicVolume);
        SetMixerVolume(SFXMixerParameter, SFXVolume);
    }
    #endregion --- Volume ---

    [Serializable]
    public class AudioElement
    {
        public string audioName;
        public AudioClip audioClip;
    }

// #if (UNITY_EDITOR)   
//     [CustomEditor(typeof(AudioManager))]
//     public class CustomInspector : Editor
//     {
//         public override void OnInspectorGUI()
//         {
//             DrawDefaultInspector();
    
//             AudioManager audioManager = (AudioManager) target;
    
//             if(GUILayout.Button("music_1"))
//             {
//                 audioManager.PlayMusic("1");
//             }

//             if(GUILayout.Button("music_2"))
//             {
//                 audioManager.PlayMusic("2");
//             }
//         }
//     }
// #endif
}