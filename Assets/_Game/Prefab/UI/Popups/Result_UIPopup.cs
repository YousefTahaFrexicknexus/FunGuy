using UnityEngine;
using TMPro;

public class Result_UIPopup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TextMeshProUGUI score_TMP;
    [SerializeField] TextMeshProUGUI highscore_TMP;
    [SerializeField] TextMeshProUGUI distanceReached_TMP;
    [SerializeField] TextMeshProUGUI coinsCollected_TMP;

    [Header("Results")]
    EndGameResults endGameResults = new EndGameResults();

    void OnEnable()
    {
        endGameResults.ResetResults();

        // TODO: Init results first
    }

    void ResetResults()
    {
        score_TMP.text = "0";
        highscore_TMP.text = "0";
        distanceReached_TMP.text = "0";
        coinsCollected_TMP.text = "0";

        endGameResults.ResetResults();
    }

    void InitResults(EndGameResults _endGameResults)
    {
        endGameResults = _endGameResults;
    }

    void ShowResultsSequence()
    {
        
    }

    public void OnClick_TryAgain()
    {
        UIManager.Instance.Close_PopupsAndPanels(UIType.Results);

        // TODO: Call game restart
    }

    public void OnClick_MainMenu()
    {
        UIManager.Instance.Close_PopupsAndPanels(UIType.Results);

        // TODO: Return to MainMenu
    }
}

public class EndGameResults
{
    public int Score = 0;
    public int Highscore = 0;
    public int DistanceReached = 0;
    public int CoinsCollected = 0;

    public void SetResults(EndGameResults _endGameResults)
    {
        Score = _endGameResults.Score;
        Highscore = _endGameResults.Highscore;
        DistanceReached = _endGameResults.DistanceReached;
        CoinsCollected = _endGameResults.CoinsCollected;
    }

    public void ResetResults()
    {
        Score = 0;
        Highscore = 0;
        DistanceReached = 0;
        CoinsCollected = 0;
    }
}