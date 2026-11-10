using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu with two buttons. Each one loads a different scene, by name, when clicked.
/// Put this on any object in the menu scene, assign the two buttons and type the two scene names.
/// Both scenes must be added in File > Build Settings > Scenes In Build.
/// </summary>
[DisallowMultipleComponent]
public class MainMenu : MonoBehaviour
{
    [Header("Button 1")]
    [SerializeField] private UIButtonAnimator firstButton;
    [Tooltip("Exact name of the scene to load (no .unity extension).")]
    [SerializeField] private string firstSceneName = "Game";

    [Header("Button 2")]
    [SerializeField] private UIButtonAnimator secondButton;
    [Tooltip("Exact name of the scene to load (no .unity extension).")]
    [SerializeField] private string secondSceneName = "CharacterSelect";

    [Header("Menu Performance")]
    [SerializeField, Range(30, 240)]
    [Tooltip("Caps only the menu while VSync is disabled; gameplay restores the previous frame rate.")]
    private int maxMenuFrameRate = 120;

    [Header("Loading Transition")]
    [SerializeField, Tooltip("Optional FunGuy logo displayed during the menu-to-gameplay loading transition.")]
    private Sprite loadingLogo;

    private bool loading;
    private bool menuRateApplied;
    private int previousTargetFrameRate;

    private void OnEnable()
    {
        loading = false;

        // Menu-only CPU/GPU frame cap: visual quality and gameplay settings stay unchanged.
        if (QualitySettings.vSyncCount == 0)
        {
            previousTargetFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = Mathf.Clamp(maxMenuFrameRate, 30, 240);
            menuRateApplied = true;
        }

        // if (firstButton != null) firstButton.GetButton().onClick.AddListener(LoadFirstScene);
        // if (secondButton != null) secondButton.GetButton().onClick.AddListener(LoadSecondScene);
    }

    private void OnDisable()
    {
        if (menuRateApplied)
        {
            // Preserve any later frame-rate decision made by another system.
            if (Application.targetFrameRate == Mathf.Clamp(maxMenuFrameRate, 30, 240))
                Application.targetFrameRate = previousTargetFrameRate;
            menuRateApplied = false;
        }

        // if (firstButton != null) firstButton.GetButton().onClick.RemoveListener(LoadFirstScene);
        // if (secondButton != null) secondButton.GetButton().onClick.RemoveListener(LoadSecondScene);
    }

    /// <summary>Loads the scene named in 'Button 1'.</summary>
    public void LoadFirstScene() => Load(firstSceneName);

    /// <summary>Loads the scene named in 'Button 2'.</summary>
    public void LoadSecondScene() => Load(secondSceneName);

    private void Load(string sceneName)
    {
        // Ignore extra taps while the scene is already loading (double-tap on mobile).
        if (loading) return;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("MainMenu: the scene name is empty.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"MainMenu: scene '{sceneName}' can't be loaded. Check the spelling and add it to " +
                           "File > Build Settings > Scenes In Build.", this);
            return;
        }

        loading = true;
        Sprite logo = loadingLogo;
        if (logo == null)
        {
            // Reuse a menu logo without changing the authored scene.
            var images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var image in images)
            {
                if (image.gameObject.scene == gameObject.scene &&
                    image.name == "Logo" && image.sprite != null)
                {
                    logo = image.sprite;
                    break;
                }
            }
        }

        if (!FunGuySceneLoadingTransition.Begin(sceneName, logo))
        {
            loading = false;
            Debug.LogError("[FunGuy MainMenu] Failed to begin async loading: " + sceneName, this);
        }
    }
}
