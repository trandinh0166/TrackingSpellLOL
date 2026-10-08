using System.Windows.Forms;

public class TimerManager
{
    public LaneTimer[] Lanes { get; } = new LaneTimer[5];

    public TimerManager()
    {
        for (int i = 0; i < 5; i++)
        {
            Lanes[i] = new LaneTimer();
            int idx = i; // capture for closure
            Lanes[i].Completed += () => OnLaneCompleted(idx);
        }

        var uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        uiTimer.Tick += (s, e) => TickAll();
        uiTimer.Start();
    }

    private void TickAll()
    {
        foreach (var lane in Lanes) lane.Tick();
    }

    private void OnLaneCompleted(int laneIdx)
    {
        // Optional sound could be played here if the user enabled it.
    }

    public void ResetAll()
    {
        foreach (var lane in Lanes) lane.Cancel();
    }

    public void ResetLane(int idx)
    {
        if (idx >= 0 && idx < 5) Lanes[idx].Cancel();
    }
}