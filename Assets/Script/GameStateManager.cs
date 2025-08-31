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

    private GameState _currentState;

    private void Start()
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
