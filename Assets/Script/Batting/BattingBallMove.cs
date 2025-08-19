using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.Playables;

public class BattingBallMove : MonoBehaviour
{
    [SerializeField] private Transform _ballTransform;
    [SerializeField] private IsHomeRun _isHomeRun;
    [SerializeField] private PlayableDirector _homerunMovie;
    [SerializeField] private PlayableDirector _hitMovie;
    [Header("移動間隔（何秒ごとに次のポイントに進むのか）"),SerializeField]
    private float _moveInterval = 0.1f; // ボールの移動間隔

    /// <summary>
    /// バッティングのボール移動処理
    /// </summary>
    public IEnumerator BattingMove(List<Vector3> trajectryData,Vector3 landingPos)
    {       
        int index = 0;
        bool isHomerun = _isHomeRun.HomeRunCheck(landingPos);
        if (isHomerun)
        {
            _homerunMovie.Play();
        }
        else
        {
            _hitMovie.Play();
        }

            Debug.Log(isHomerun ? "ホームラン！" : "ホームランではない");
        while (index < trajectryData.Count)
        {
            // ボールの位置を更新
            _ballTransform.DOMove(trajectryData[index], _moveInterval).SetEase(Ease.Linear);
            yield return new WaitForSeconds(_moveInterval);
            if (isHomerun && trajectryData[index] == landingPos)
            {
                Debug.Log("ホームランの位置に到達しました。イベントを発火します。");
                _isHomeRun.OnHomeRun?.Invoke();
            }
            index++;
        }
    }
}
