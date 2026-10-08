using System;
using System.Diagnostics;

public class LaneTimer
{
    private readonly Stopwatch _watch = new();
    private const int DurationSeconds = 5 * 60;

    public bool IsRunning => _watch.IsRunning;
    public TimeSpan Remaining => TimeSpan.FromSeconds(DurationSeconds) - _watch.Elapsed;

    public void Start()
    {
        if (IsRunning) return;
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
        if (Remaining <= TimeSpan.Zero)
        {
            Cancel();
            Completed?.Invoke();
        }
    }
}