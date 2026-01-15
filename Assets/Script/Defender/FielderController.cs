using UnityEngine;

public class FielderController : MonoBehaviour
{
    [SerializeField] private FielderData _data;
    public FielderData Data => _data;

    public void MoveTo(Vector3 targetPos, float timeLimit)
    {
        // NavMesh / ’¼üˆÚ“® / Tween “™
    }

    public void MoveToCutoff()
    {
        // ’†ŒpˆÊ’u‚ÖˆÚ“®
    }

    public void CatchBall()
    {
    }

    public void ThrowBall(Vector3 target)
    {
    }
}
