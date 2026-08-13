using UnityEngine;
using UnityEngine.SceneManagement;

using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.VisualScripting;

public class GlobalManager : Singleton<GlobalManager>
{
	[Header("Splash screen")]
	[SerializeField] SplashScreen splashScreen;

    [Header("Loading screen")]
    [SerializeField] LoadingScreen loadingScreen_firstLaunch;
	[SerializeField] UIPanelAnimator loadingPanel_UIPanelAnimator;
	[SerializeField] float currentStep;
	[SerializeField] float totalSteps;

	[Header("Player Prefab")]
	public GameObject PlayerPrefab;

	[Header("Extra Properties")]
	public bool isLoadingScene;
	public bool isFirstLaunch = true;
	[SerializeField] SceneNames currentScene;

	[Header("Global Settings")]
	Coroutine gameLaunchSequenceCoroutine;

	public enum SceneNames
	{	
        Loader = 0,
		SplashScreen = 1,
		Main = 2,
	}

	void Awake()
	{
		GlobalEvents.OnAppUpdateStarted += OnAppUpdateStarted;
		GlobalEvents.OnAppUpdateDeclined += OnAppUpdateDeclined;
	}

	IEnumerator Start()
	{
        GetDeviceID();
        InitApplicationSettings();

		yield return splashScreen.StartSplashScreenAnimation();

		if(Get_ActiveScene_Name() == "SplashScreen")
		{
			yield return StartCoroutine(WaitForAsync(() => LoadScene(SceneNames.Loader)));
		}

		gameLaunchSequenceCoroutine = StartCoroutine(FirstLaunch_LoadingSequence());
	}

	void InitApplicationSettings()
	{
        Set_MultiTouch(false);
		Set_FrameRate(60);
		KeepScreenAwake();
	}

	public IEnumerator FirstLaunch_LoadingSequence()
	{
		yield return StartLoadingScreen();

		// --- Put all the steps here ---		
		// Step | Try login to PlayFab
        yield return StartCoroutine(WaitForAsync(() => PlayFabManager.Instance.TryLogin()));
		UpdateLoadingProgress();
		yield return new WaitForSeconds(0.5f);

		// Step | Fetch Title Data / Remote Config
        yield return StartCoroutine(WaitForAsync(() => PlayFabManager.Instance.TryFetchTitleData()));
		UpdateLoadingProgress();
		yield return new WaitForSeconds(0.5f);

		// Step | FCM
        // yield return FetchFCMToken();

		// Step | Check for updates
        yield return StartCoroutine(WaitForAsync(() => AppUpdateManager.Instance.CheckUpdate()));
		UpdateLoadingProgress();

		if(isAppUpdating == true)
		{
			yield break;
		}

		// Step | Check if update need to show update popup
		yield return CheckIfUpdateRequired();

		// Step | Fetch Leaderboard Data
		yield return StartCoroutine(WaitForAsync(() => PlayFabManager.Instance.TryFetchLeaderboardData()));
		UpdateLoadingProgress();

		// Step | Fetch game settings
        // yield return StartCoroutine(WaitForAsync(() => Write async function to fetch game settings));

		// Step | Initialize IAP products after game settings fetching
        // yield return StartCoroutine(WaitForAsync(() => Write async function to fetch IAP data));

		// Step | Fetch player data
        // yield return StartCoroutine(WaitForAsync(() => Write async function to fetch Player data));

		yield return new WaitForSeconds(0.5f);

		// Step | Load homescreen
		// yield return StartCoroutine(WaitForAsync(() => LoadScene(SceneNames.Main)));
		// UpdateLoadingProgress();

		isFirstLaunch = false;
    }

	void UpdateLoadingProgress(string _status = "")
	{
		currentStep += 1;

		loadingScreen_firstLaunch.UpdateProgress(currentStep/totalSteps);

		if (_status != "")
        {
            loadingScreen_firstLaunch.SetProgressText(_status);
        }
	}

	public int GetTimeNow()
	{
		return System.DateTime.Now.Hour;
	}

    private void Set_MultiTouch(bool _state)
	{
		Input.multiTouchEnabled = _state;
	}

    private void KeepScreenAwake()
	{
		Screen.sleepTimeout = SleepTimeout.NeverSleep;
	}

	private void Set_FrameRate(int _frameValue)
	{
		Application.targetFrameRate = _frameValue;
	}

	public async Task LoadScene(SceneNames _sceneName, bool _isAdditive = false)
	{
		if(!_isAdditive)
			await LoadScene_Normal(_sceneName);
		else
			await LoadScene_Additive(_sceneName);
	}

	public void UnLoadScene(SceneNames _sceneName)
	{
		SceneManager.UnloadSceneAsync(_sceneName.ToString());
	}

    public async Task LoadScene_Normal(SceneNames _sceneName)
	{
		if(isLoadingScene)
			return;
			
		isLoadingScene = true;

		if(!isFirstLaunch)
		{
			loadingPanel_UIPanelAnimator.OnClick_ForceOpen();
		}

		AsyncOperation scene = SceneManager.LoadSceneAsync(_sceneName.ToString());
		scene.allowSceneActivation = false;

		while(scene.progress < 0.9f)
		{
			// Debug.Log($"scene.progress: {scene.progress}");

            // loadingScreen.UpdateProgress(scene.progress);
			await Task.Delay(100);
		}

		UpdateLoadingProgress();

		await Task.Delay(1000);

		scene.allowSceneActivation = true;

		await Task.Delay(1000);

		currentScene = _sceneName;

		if(!isFirstLaunch)
		{
			loadingPanel_UIPanelAnimator.OnClick_ForceClose();
		}
		else
		{
			if(currentScene != SceneNames.Loader)
				loadingScreen_firstLaunch.FadeOut();
		}			

		isLoadingScene = false;
	}

	public async Task LoadScene_Additive(SceneNames _sceneName)
	{
		if(isLoadingScene)
			return;
			
		isLoadingScene = true;

		if(isFirstLaunch)
		{
			loadingScreen_firstLaunch.gameObject.SetActive(true);
        	// loadingScreen.FadeIn();
		}
		else
		{
			loadingPanel_UIPanelAnimator.OnClick_ForceOpen();
		}

		AsyncOperation scene = SceneManager.LoadSceneAsync(_sceneName.ToString(), LoadSceneMode.Additive);
		scene.allowSceneActivation = false;

		while(scene.progress < 0.9f)
		{
			// Debug.Log($"scene.progress: {scene.progress}");

            // loadingScreen.UpdateProgress(scene.progress);
			await Task.Delay(100);
		}

		scene.allowSceneActivation = true;

		await Task.Delay(1000);

		currentScene = _sceneName;

		if(!isFirstLaunch)
		{
			loadingPanel_UIPanelAnimator.OnClick_ForceClose();
		}
		else
		{
			if(currentScene != SceneNames.Loader)
				loadingScreen_firstLaunch.FadeOut();
		}

		isLoadingScene = false;
	}

	public string Get_ActiveScene_Name()
	{
		return SceneManager.GetActiveScene().name;
	}

	public string GetDeviceID()
	{
		return SystemInfo.deviceUniqueIdentifier;
	}

	public void Set_SplashScreenOff()
	{
		splashScreen.gameObject.SetActive(false);
	}

	IEnumerator StartLoadingScreen()
	{
		Invoke(nameof(Set_SplashScreenOff), 0.5f);
		loadingScreen_firstLaunch.gameObject.SetActive(true);
		loadingScreen_firstLaunch.UpdateProgress(0);

		splashScreen.gameObject.SetActive(true);
		yield return new WaitForSeconds(2f);
		splashScreen.gameObject.SetActive(false);
	}

    IEnumerator FetchRemoteConfig()
    {
        // TODO:
        // while(FirebaseRemoteConfigManager.isConfigFetched == false)
		// {
		// 	Debug.Log("Firebase_RemoteConfigManager.isConfigDataFetched");
		// 	yield return new WaitForSeconds(0.25f);
		// }

        UpdateLoadingProgress();

		yield return new WaitForSeconds(0.25f);
    }

    IEnumerator FetchFCMToken()
    {
        // TODO:
        // while(FirebaseMessagingManager.IsFCMFetched == false)
		// {
		// 	Debug.Log("Firebase_Messaging.isFCMFetched");
		// 	yield return new WaitForSeconds(0.25f);
		// }

        UpdateLoadingProgress();

		yield return new WaitForSeconds(0.25f);
    }

    IEnumerator FetchGameSettings()
    {
        // TODO:
        // DataManager.Instance.InitGameSettingsFetching();
		// while (DataManager.isGameSettingsLoadingCompleted == false)
		// {
		// 	Debug.Log("DataManager.isGameSettingsLoadingCompleted");
		// 	yield return new WaitForSeconds(0.25f);
		// }

        UpdateLoadingProgress();

        yield return new WaitForSeconds(0.25f);
    }

    IEnumerator InitializeInAppPurchases()
    {
        // TODO:
        // IAPManager_Store.Instance.InitializeIAP();

		UpdateLoadingProgress();

        yield return new WaitForSeconds(0.25f);
    }

    IEnumerator CheckIfUpdateRequired()
    {
        if(AppUpdateManager.Instance.IsVersionUpToDate() == false)
		{
			AppUpdateManager.Instance.ShowUpdatePopup();
		}

		yield return new WaitUntil(() => UIManager.Instance.IsPopupOpen(UIType.appUpdate_Popup) == false);

        yield return new WaitForSeconds(0.25f);
    }

	IEnumerator WaitForAsync(Func<Task> asyncFunction)
    {
        // Call the async function and wait for it to complete
        Task task = asyncFunction();

        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.Exception != null)
        {
            Debug.LogError($"Async function threw an exception: {task.Exception}");
        }
        else
        {
            Debug.Log("Async function completed successfully.");
        }
    }

	[Header("Debugging"), Space]
	[Header("Connectivity (Internet) related")]
	public bool makeOffline = false;
	
	[Header("Place static token")]
	public bool isForTesting;
	public string playerToken = "";

	#region App Update
	bool isAppUpdating = false;
	bool isAppUpdateDeclined = false;

	void OnAppUpdateStarted()
	{
		if(gameLaunchSequenceCoroutine != null)
		{
			StopCoroutine(gameLaunchSequenceCoroutine);
			gameLaunchSequenceCoroutine = null;
		}

		isAppUpdating = true;
	}

	void OnAppUpdateDeclined()
	{
		isAppUpdateDeclined = true;
	}
	#endregion --- App Update ---
}	
