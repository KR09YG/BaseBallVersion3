using UnityEngine;

public class ServiceLocator : MonoBehaviour
{
    [SerializeField] private BattingInputManager _battingInputManager;
    [SerializeField] private BallControl _ballControl;
    [SerializeField] private CursorController _cursorController;
    [SerializeField] private BattingResultCalculator _battingResultCalculator;
    [SerializeField] private BattingBallMove _battingBallMove;
    [SerializeField] private RunnerCalculation _runnerCalculation;
    [SerializeField] private BaseManager _baseManager;
    [SerializeField] private BallJudge _ballJudge;
    [SerializeField] private AdvancedTrajectoryCalculator _advancedTrajectoryCalculator;
    [SerializeField] private BallCountManager _ballCountManager;
    [SerializeField] private RePlayManager _rePlayManager;
    [SerializeField] private BatterAnimationControl _batterAnimationControl;
    [SerializeField] private PitchManager _pitchManager;

    public static ServiceLocator ServiceLocatorInstance { get; private set; }
    public static BattingInputManager BattingInputManagerInstance { get; private set; }
    public static BallControl BallControlInstance { get; private set; }
    public static CursorController CursorControllerInstance { get; private set; }
    public static BattingResultCalculator BattingResultCalculatorInstance { get; private set; }
    public static BattingBallMove BattingBallMoveInstance { get; private set; }
    public static RunnerCalculation RunnerCalculationInstance { get; private set; }
    public static BaseManager BaseManagerInstance { get; private set; }
    public static BallJudge BallJudgeInstance { get; private set; }
    public static AdvancedTrajectoryCalculator AdvancedTrajectoryCalculatorInstance { get; private set; }
    public static BallCountManager BallCountManagerInstance { get; private set; }
    public static RePlayManager RePlayManagerInstance { get; private set; }
    public static BatterAnimationControl BatterAnimationControlInstance { get; private set; }
    public static PitchManager PitchManagerInstance { get; private set; }

    private void Awake()
    {
        if (ServiceLocatorInstance != null && ServiceLocatorInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        BattingInputManagerInstance = _battingInputManager;
        BallControlInstance = _ballControl;
        CursorControllerInstance = _cursorController;
        BattingResultCalculatorInstance = _battingResultCalculator;
        BattingBallMoveInstance = _battingBallMove;
        RunnerCalculationInstance = _runnerCalculation;
        BaseManagerInstance = _baseManager;
        BallJudgeInstance = _ballJudge;
        AdvancedTrajectoryCalculatorInstance = _advancedTrajectoryCalculator;
        BallCountManagerInstance = _ballCountManager;
        RePlayManagerInstance = _rePlayManager;
        BatterAnimationControlInstance = _batterAnimationControl;
        PitchManagerInstance = _pitchManager;

        _battingInputManager = null;
        _ballControl = null;
        _cursorController = null;
        _battingResultCalculator = null;
        _battingBallMove = null;
        _runnerCalculation = null;
        _baseManager = null;
        _ballJudge = null;
        _advancedTrajectoryCalculator = null;
        _ballCountManager = null;
        _rePlayManager = null;
        _batterAnimationControl = null;
        _pitchManager = null;

        ServiceLocatorInstance = this;
    }
}
