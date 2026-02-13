using UnityEngine;

[RequireComponent(typeof(Animator))]
public class FielderAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator _animator;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLogs = true;

    private static readonly int StateParam = Animator.StringToHash("State");
    private static readonly int WaitingHash = Animator.StringToHash("Waiting");
    private static readonly int MovingToHash = Animator.StringToHash("MovingTo");
    private static readonly int CatchingBallHash = Animator.StringToHash("CatchingBall");
    private static readonly int ThrowingBallHash = Animator.StringToHash("ThrowingBall");

    private FielderState _currentState = FielderState.Waiting;

    private void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }
    }

    public void PlayAnimation(FielderState state)
    {
        Debug.Log($"Playing animation for state: {state}");
        _animator.applyRootMotion = (state != FielderState.MovingTo);
        _currentState = state;
        int hash = GetHashForState(state);

        _animator.Play(hash);
    }

    private int GetHashForState(FielderState state)
    {
        return state switch
        {
            FielderState.Waiting => WaitingHash,
            FielderState.MovingTo => MovingToHash,
            FielderState.CatchingBall => CatchingBallHash,
            FielderState.ThrowingBall => ThrowingBallHash,
            _ => WaitingHash,
        };
    }

    public FielderState GetCurrentState()
    {
        return _currentState;
    }
}