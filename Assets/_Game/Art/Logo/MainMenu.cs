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

    private bool loading;

    private void OnEnable()
    {
        loading = false;

        // if (firstButton != null) firstButton.GetButton().onClick.AddListener(LoadFirstScene);
        // if (secondButton != null) secondButton.GetButton().onClick.AddListener(LoadSecondScene);
    }

    private void OnDisable()
    {
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
        SceneManager.LoadScene(sceneName);
    }
}
