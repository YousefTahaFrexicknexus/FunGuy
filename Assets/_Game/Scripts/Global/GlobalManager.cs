using UnityEngine;
using UnityEngine.SceneManagement;

using System;
using System.Collections;
using System.Threading.Tasks;

public class GlobalManager : Singleton<GlobalManager>
{
	[Header("Splash screen")]
	[SerializeField] SplashScreen splashScreen;

    [Header("Loading screen")]
    [SerializeField] LoadingScreen loadingScreen_firstLaunch;
	[SerializeField] UIPanelAnimator loadingPanel_UIPanelAnimator;
	[SerializeField] float currentStep;
	[SerializeField] float totalSteps;

	[Header("Player Prefab")] public GameObject PlayerPrefab;
	[Header("Extra Properties")]
	public bool isLoadingScene;
	public bool isFirstLaunch = true;
	[SerializeField] SceneNames currentScene;

	public enum SceneNames
	{	
        Loader = 0,
		SplashScreen = 1,
		Main = 2,
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

		StartCoroutine(FirstLaunch_LoadingSequence());
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
		// Step | Fetch Firebase Remote Config
        yield return FetchRemoteConfig();

		// Step | FCM
        yield return FetchFCMToken();

		// Step | Fetch game settings
        yield return FetchGameSettings();

		// Step | Initialize IAP products after game settings fetching
        yield return InitializeInAppPurchases();

		// Step | Fetch player data
        yield return CheckForUpdate();

		// Step | Load homescreen
		yield return StartCoroutine(WaitForAsync(() => LoadScene(SceneNames.Main)));

		UpdateLoadingProgress();

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

    IEnumerator CheckForUpdate()
    {
        // TODO:
        // if(AppUpdateManager.isShowPopupOnLoginScreen)
		// {
		// 	AppUpdateManager.Instance.CheckUpdate();
		// 	AppUpdateManager.isShowPopupOnLoginScreen = false;
		// }

		UpdateLoadingProgress();

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
}	
