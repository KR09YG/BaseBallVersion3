using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.Playables;
using UnityEngine.Rendering;

public class BattingBallMove : MonoBehaviour
{
    [SerializeField] private Transform _ballTransform;
    [SerializeField] private IsHomeRun _isHomeRun;
    [SerializeField] private PlayableDirector _homerunMovie;
    [SerializeField] private PlayableDirector _hitMovie;
    [Header("�ړ��Ԋu�i���b���ƂɎ��̃|�C���g�ɐi�ނ̂��j"), SerializeField]
    private float _moveInterval = 0.1f; // �{�[���̈ړ��Ԋu

    private void Start()
    {
        ServiceLocator.Register(this);
    }

    /// <summary>
    /// �o�b�e�B���O�̃{�[���ړ�����
    /// </summary>
    public IEnumerator BattingMove(List<Vector3> trajectryData, Vector3 landingPos, bool isRePlay)
    {       
        ServiceLocator.Get<BallControl>().SetBallState(true);
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
                break;
            }

            index++;
        }
        ServiceLocator.Get<BallControl>().SetBallState(false);
    }

    public void StopMovie()
    {
        _hitMovie.Stop();
        _homerunMovie.Stop();
    }
}
