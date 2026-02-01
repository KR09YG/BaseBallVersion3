using TMPro;
using UnityEngine;

public class ScoreboardViewController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI[] _topScoreTexts;
    [SerializeField] private TextMeshProUGUI[] _bottomScoreTexts;
    [SerializeField] private GameObject _scoreboardPanel;
    private void Awake()
    {
        this.gameObject.SetActive(true);

        foreach (var text in _topScoreTexts)
        {
            text.text = "";
        }

        foreach (var text in _bottomScoreTexts)
        {
            text.text = "";
        }
    }

    public void UpdateScore(bool isTop, int inning, int score)
    {
        Debug.Log($"ScoreboardViewController: Updating score. isTop={isTop}, inning={inning}, score={score}");
        var texts = isTop ? _topScoreTexts : _bottomScoreTexts;
        texts[inning - 1].text = score.ToString();
        Debug.Log(texts[inning - 1].text);
    }

    // “¾“_”Â‚ð”ñ•\Ž¦‚É‚·‚é
    public void HideScoreboard()
    {
        _scoreboardPanel.SetActive(false);
    }

    public void ShowScoreboard()
    {
        _scoreboardPanel.SetActive(true);
    }
}
