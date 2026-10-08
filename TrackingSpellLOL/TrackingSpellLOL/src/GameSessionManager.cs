using System;

public enum GameSessionState
{
    NOT_IN_GAME,
    CONNECTING,
    SYNCING,
    IN_GAME,
    DISCONNECTED
}

public class GameSessionManager
{
    public GameSessionState State { get; private set; } = GameSessionState.NOT_IN_GAME;
    public event Action<GameSessionState>? StateChanged;
    public event Action? NewGameStarted;

    private string _lastGameId = "";

    public void UpdateSession(bool isApiAvailable, string currentGameId)
    {
        var prevState = State;

        if (!isApiAvailable)
        {
            State = GameSessionState.NOT_IN_GAME;
        }
        else
        {
            if (State == GameSessionState.NOT_IN_GAME)
            {
                State = GameSessionState.CONNECTING;
            }

            if (!string.IsNullOrEmpty(currentGameId) && currentGameId != _lastGameId)
            {
                _lastGameId = currentGameId;
                NewGameStarted?.Invoke();
                State = GameSessionState.IN_GAME;
            }
            else
            {
                State = GameSessionState.IN_GAME;
            }
        }

        if (prevState != State)
        {
            StateChanged?.Invoke(State);
        }
    }

    public void ResetSession()
    {
        _lastGameId = "";
        State = GameSessionState.NOT_IN_GAME;
        NewGameStarted?.Invoke();
    }
}
