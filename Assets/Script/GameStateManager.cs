using UnityEngine;
public enum GameState
{
    Batting,
    Pitching,
    Fielding,
    Paused,
    Replay,
    Result
}

public class GameStateManager : MonoBehaviour
{

    public GameState CurrentState { get; private set; } = GameState.Batting;

    /// <summary>
    /// GameState‚ğİ’è‚·‚é
    /// </summary>
    /// <param name="state"></param>
    public void SetState(GameState state)
    {
        CurrentState = state;
    }

}
