using UnityEngine;
using UnityEngine.UI;

public class BallCountDisplay : MonoBehaviour
{
    private BallCountManager _ballCountManager;
    [SerializeField, Header("ストライクカウントのイメージ")] Image[] _strikeImages;
    [SerializeField, Header("ボールカウントのイメージ")] Image[] _ballImages;
    [SerializeField, Header("アウトカウントのイメージ")] Image[] _outImages;
    
    public void BallCountDisplayUpdate()
    {
        for (int i = 0; i < _strikeImages.Length; i++)
        {
            if (i + 1 <= _ballCountManager.StrikeCount)            
                _strikeImages[i].color = Color.yellow;
            else _strikeImages[i].color = Color.white;
        }

        for (int i = 0; i < _ballCountManager.BallCount; i++)
        {
            if (i + 1 <= _ballCountManager.BallCount)
                _ballImages[i].color = Color.green;
            else
                _ballImages[i].color = Color.white;
        }

        for (int i = 0; i < _ballCountManager.OutCount; i++)
        {
            if (i + 1 <= _ballCountManager.OutCount)
                _outImages[i].color = Color.red;
            else
                _outImages[i].color = Color.white;
        }
    }
}
