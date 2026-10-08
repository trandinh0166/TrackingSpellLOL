using System;
using System.Diagnostics;

public class SpellTimer
{
    private readonly Stopwatch _watch = new();
    public int CooldownSeconds { get; set; } = 300; // Default 5 min

    public bool IsRunning => _watch.IsRunning;
    public double ElapsedSeconds => _watch.Elapsed.TotalSeconds;
    public TimeSpan Remaining => TimeSpan.FromSeconds(CooldownSeconds) - _watch.Elapsed;
    public bool IsNegative => Remaining.TotalSeconds <= 0;

    public void Start(int durationSeconds)
    {
        CooldownSeconds = durationSeconds;
        _watch.Restart();
    }

    public void Cancel()
    {
        _watch.Stop();
        _watch.Reset();
    }

    public event Action? Completed;

    public void Tick()
    {
        if (!IsRunning) return;
        // Auto reset if negative countdown exceeds -30 seconds
        if (Remaining.TotalSeconds <= -30.0)
        {
            Cancel();
            Completed?.Invoke();
        }
    }
}
