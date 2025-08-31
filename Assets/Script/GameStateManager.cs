using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public enum GameState
    {
        Batting,
        Pitching,
        Fielding,
        Paused,
        Replay,
        Result
    }

    private GameState _currentState = GameState.Batting;

    private void Awake()
    {
        ServiceLocator.Register(this);
    }

    /// <summary>
    /// GameState‚ğİ’è‚·‚é
    /// </summary>
    /// <param name="state"></param>
    public void SetState(GameState state)
    {
        _currentState = state;
    }

    public GameState GetCurrentState()
    {
        return _currentState;
    }

}
