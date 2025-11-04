using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.Playables;
using Cysharp.Threading.Tasks;

public class BattingBallMove : MonoBehaviour
{
    [SerializeField] private Transform _ballTransform;
    [SerializeField] private PlayableDirector _homerunMovie;
    [SerializeField] private BallControl _ballControl;
    [SerializeField] private PlayableDirector _hitMovie;
    [SerializeField] private GameStateManager _gameStateManager;
    [Header("移動間隔（何秒ごとに次のポイントに進むのか）"), SerializeField]
    private float _moveInterval = 0.1f; // ボールの移動間隔
    [SerializeField] private HomeRunChecker _isHomeRun;


    public async UniTaskVoid BattingBallMoved()
    {
        _gameStateManager.SetState(GameState.Fielding);

        int index = 0;
        
    }

    /// <summary>
    /// バッティングのボール移動処理
    /// </summary>
    public IEnumerator BattingMove(List<Vector3> trajectryData, Vector3 landingPos, bool isRePlay)
    {       
        _gameStateManager.SetState(GameState.Fielding);

        int index = 0;
        bool isHomerun = _isHomeRun.HomeRunCheck(landingPos);
        if (isHomerun && !isRePlay)
        {
            _homerunMovie.Play();
        }
        else if (!isHomerun && !isRePlay)
        {
            _hitMovie.Play();
        }

            Debug.Log(isHomerun ? "ホームラン！" : "ホームランではない");
        while (index < trajectryData.Count)
        {
            // ボールの位置を更新
            _ballTransform.DOMove(trajectryData[index], _moveInterval).SetEase(Ease.Linear);
            yield return new WaitForSeconds(_moveInterval);

            if (isHomerun && trajectryData[index] == landingPos && !isRePlay)
            {
                Debug.Log("ホームランの位置に到達しました。イベントを発火します。");
                _isHomeRun.OnHomeRun?.Invoke();
                break;
            }
            
            if (trajectryData[index] == landingPos && isRePlay)
            {
                Debug.Log("リプレイを終了します。イベントを発火します。");
                _gameStateManager.SetState(GameState.Batting);
                break;
            }

            index++;
        }

        _gameStateManager.SetState(GameState.Batting);        
    }

    public void StopMovie()
    {
        _hitMovie.Stop();
        _homerunMovie.Stop();
    }
}
