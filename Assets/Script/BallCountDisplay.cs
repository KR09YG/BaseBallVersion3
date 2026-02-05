using UnityEngine;
using UnityEngine.UI;

public class BallCountDisplay : MonoBehaviour
{
    [SerializeField] private Image[] _ballCounts;
    [SerializeField] private Image[] _strikeCounts;
    [SerializeField] private Image[] _outCounts;

    public void UpdateBallCount(Count count)
    {
        for (int i = 0; i < _ballCounts.Length; i++)
        {
            _ballCounts[i].color = i < count.Balls ? Color.green : Color.white;
        }
        for (int i = 0; i < _strikeCounts.Length; i++)
        {
            _strikeCounts[i].color = i < count.Strikes ? Color.yellow : Color.white;
        }
        for (int i = 0; i < _outCounts.Length; i++)
        {
            _outCounts[i].color = i < count.Outs ? Color.red : Color.white;
        }
    }
}
