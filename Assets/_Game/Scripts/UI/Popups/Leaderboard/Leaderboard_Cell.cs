using UnityEngine;
using UnityEngine.UI;

using TMPro;

public class Leaderboard_Cell : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] TMP_Text rankText;
    [SerializeField] TMP_Text playerNameText;
    [SerializeField] TMP_Text scoreText;

    [Header("Images")]
    [SerializeField] Image profileImage;
    [SerializeField] Image medalImage;

    [Header("Medals")]
    [SerializeField] Sprite goldMedal;
    [SerializeField] Sprite silverMedal;
    [SerializeField] Sprite bronzeMedal;

    public void Setup(int rank, string playerName, int score, Sprite profileSprite)
    {
        playerNameText.text = playerName;
        scoreText.text = score.ToString("N0");

        profileImage.sprite = profileSprite;

        SetupRank(rank);
    }

    void SetupRank(int rank)
    {
        medalImage.gameObject.SetActive(false);

        switch(rank)
        {
            case 1:
            {
                rankText.transform.parent.gameObject.SetActive(false);
                medalImage.gameObject.SetActive(true);
                medalImage.sprite = goldMedal;
                break;
            }

            case 2:
            {
                rankText.transform.parent.gameObject.SetActive(false);
                medalImage.gameObject.SetActive(true);
                medalImage.sprite = silverMedal;
                break;
            }

            case 3:
            {
                rankText.transform.parent.gameObject.SetActive(false);
                medalImage.gameObject.SetActive(true);
                medalImage.sprite = bronzeMedal;
                break;
            }

            default:
            {
                rankText.transform.parent.gameObject.SetActive(true);
                rankText.text = rank.ToString();
                break;
            }
        }
    }
}