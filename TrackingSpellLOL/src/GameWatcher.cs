using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class GameWatcher : IDisposable
{
    private readonly Action<bool> _visibilityCallback;
    private readonly System.Timers.Timer _pollTimer = new(1000);
    private bool _wasInGame = false;

    public GameWatcher(Action<bool> visibilityCallback)
    {
        _visibilityCallback = visibilityCallback;
        _pollTimer.Elapsed += (s, e) => CheckGameWindow();
        _pollTimer.Start();
    }

    private void CheckGameWindow()
    {
        bool inGame = false;
        foreach (var p in Process.GetProcessesByName("League of Legends"))
        {
            if (p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle))
            {
                inGame = true;
                break;
            }
        }

        if (inGame != _wasInGame)
        {
            _wasInGame = inGame;
            _visibilityCallback(inGame);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    public void Dispose() => _pollTimer.Dispose();
}