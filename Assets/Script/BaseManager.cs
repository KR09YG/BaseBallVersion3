using UnityEngine;

public class BaseManager : MonoBehaviour
{
    [Tooltip("ベースのポジション設定")]
    [SerializeField] private Transform _homeBase;
    [SerializeField] private Transform _firstBase;
    [SerializeField] private Transform _secondBase;
    [SerializeField] private Transform _thirdBase;

    public Transform HomeBase => _homeBase;
    public Transform FirstBase => _firstBase;
    public Transform SecondBase => _secondBase;
    public Transform ThirdBase => _thirdBase;

    private void Start()
    {
        ServiceLocator.Register(this);
    }

}
