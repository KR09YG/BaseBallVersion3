using System.Collections;
using UnityEngine;

public class RunnerCalculation : MonoBehaviour
{
    [SerializeField] private Transform _runnerTransform;
    private WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
    /// <summary>
    /// �����i�[�̑��یv�Z
    /// </summary>
    /// <param name="arrivaltime">���̗ۂɓ��B����܂ł̎���</param>
    /// <param name="startPoint">�ǂꂭ�炢���[�h���Ă��邩�i�o�b�^�[�̓[���j</param>
    /// <returns></returns>
    public IEnumerator RunningCalculate(float arrivaltime, float startPoint, Transform nextBase, Transform currentBase)
    {
        float t = startPoint;
        while (t < 1)
        {
            //Debug.Log(t);
            t += Time.deltaTime / arrivaltime;
            _runnerTransform.position = Vector3.Lerp(nextBase.position, currentBase.position, t);
            yield return _waitForEndOfFrame;
        }
    }
}
