using UnityEngine;

public abstract class DefenderBase : MonoBehaviour
{
    [Header("パラメータ設定")]
    [SerializeField] private float _moveSpeed;
    [SerializeField] private float _dashSpeed;
    [SerializeField] private float _chachRange;
    [SerializeField] private float _chachHeight;
    [SerializeField] private float _reactionSpeed;

    [Header("投球能力設定")]
    [SerializeField] private float _throwSpeed;
    [SerializeField] private Transform _throwPoint;

    protected Rigidbody _ballRb;
    protected DefenseState _currentState = DefenseState.Ready;
    protected GameObject _ball;
    protected Vector3 _targetPosition;
    protected bool _isChasing = false;
    protected float _reactionTimer = 0f;
    protected BaseManager _baseManager;

    protected enum DefenseState
    {
        Ready, Chasing, Catching, Throwing, Returning
    }

    protected virtual void Start()
    {
        _currentState = DefenseState.Ready;
    }

    protected virtual void Update()
    {
        switch (_currentState)
        {
            case DefenseState.Ready:
                HandleReadyState();
                break;
            case DefenseState.Chasing:
                HandleChasingState();
                break;
            case DefenseState.Catching:
                HandleCatchingState();
                break;
            case DefenseState.Throwing:
                HandleThrowingState();
                break;
            case DefenseState.Returning:
                HandleReturningState();
                break;
        }
    }

    protected virtual void HandleReadyState()
    {
        
    }
    protected virtual void HandleChasingState()
    {

    }

    protected virtual void HandleCatchingState()
    {
    
    }

    protected virtual void HandleThrowingState()
    {

    }

    protected virtual void HandleReturningState()
    {
        
    }
    protected abstract bool IsChaseable();

}
