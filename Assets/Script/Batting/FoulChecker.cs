using DG.Tweening.Core.Easing;
using UnityEngine;

public class FoulChecker : MonoBehaviour
{
    [SerializeField] BaseManager _baseManager;
    public bool IsFoul(Vector3 landingpoint)
    {
        landingpoint -= _baseManager.HomeBase.position;
        Vector3 firstDir = (_baseManager.FirstBase.position - _baseManager.HomeBase.position).normalized;
        Vector3 thirdDir = (_baseManager.ThirdBase.position - _baseManager.HomeBase.position).normalized;

        float angleToFirst = Vector3.Angle(firstDir, landingpoint);
        float angleToThird = Vector3.Angle(thirdDir, landingpoint);
        float fieldAngle = Vector3.Angle(firstDir, thirdDir);

        // �t�F�A�F�����̊p�x���t�B�[���h�p�x�ȉ�
        bool isFair = angleToFirst <= fieldAngle && angleToThird <= fieldAngle;
        return !isFair;
    }
}
