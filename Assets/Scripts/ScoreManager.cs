using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager instance;

    [SerializeField]
    int CurrentScore = 0;

    [SerializeField]
    TextMeshProUGUI ScoreText;


    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        ScoreText.text = "Score : " + CurrentScore.ToString();
    }


    public void AddScore(int scoreToAdd)
    {
        CurrentScore += scoreToAdd;
        UpdateUI();
    }
}
