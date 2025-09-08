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
    [Header("�ړ��Ԋu�i���b���ƂɎ��̃|�C���g�ɐi�ނ̂��j"), SerializeField]
    private float _moveInterval = 0.1f; // �{�[���̈ړ��Ԋu
    [SerializeField] private HomeRunChecker _isHomeRun;

    private void Awake()
    {
        ServiceLocator.Register(this);
    }

    

    public async UniTaskVoid BattingBallMoved()
    {
        _gameStateManager.SetState(GameState.Fielding);

        int index = 0;
        
    }

    /// <summary>
    /// �o�b�e�B���O�̃{�[���ړ�����
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
        else if (isRePlay && isHomerun)
        {
            ServiceLocator.Get<RePlayManager>().ResetPriority(ServiceLocator.Get<RePlayManager>().BatterCamera);
            ServiceLocator.Get<RePlayManager>().SetPriority(ServiceLocator.Get<RePlayManager>().BallLookCamera);
        }

            Debug.Log(isHomerun ? "�z�[�������I" : "�z�[�������ł͂Ȃ�");
        while (index < trajectryData.Count)
        {
            // �{�[���̈ʒu���X�V
            _ballTransform.DOMove(trajectryData[index], _moveInterval).SetEase(Ease.Linear);
            yield return new WaitForSeconds(_moveInterval);

            if (isHomerun && trajectryData[index] == landingPos && !isRePlay)
            {
                Debug.Log("�z�[�������̈ʒu�ɓ��B���܂����B�C�x���g�𔭉΂��܂��B");
                _isHomeRun.OnHomeRun?.Invoke();
                break;
            }
            
            if (trajectryData[index] == landingPos && isRePlay)
            {
                Debug.Log("���v���C���I�����܂��B�C�x���g�𔭉΂��܂��B");
                ServiceLocator.Get<RePlayManager>().RePlayFin?.Invoke();
                ServiceLocator.Get<BattingInputManager>()._endReplay?.Invoke();
                _gameStateManager.SetState(GameState.Batting);
                break;
            }

            index++;
        }

        _gameStateManager.SetState(GameState.Batting);

        if (!isHomerun)
            ServiceLocator.Get<BattingInputManager>().ResetData();
    }

    public void StopMovie()
    {
        _hitMovie.Stop();
        _homerunMovie.Stop();
    }
}
