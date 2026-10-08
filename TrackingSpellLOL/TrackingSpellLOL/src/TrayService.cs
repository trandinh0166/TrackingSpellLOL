using System;
using System.Drawing;
using System.Windows.Forms;

public class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly OverlayForm _overlay;
    private readonly Settings _settings;

    public TrayService(OverlayForm overlay, Settings settings)
    {
        _overlay = overlay;
        _settings = settings;
        _icon = new NotifyIcon
        {
            Icon = GetAppTrayIcon(),
            Text = "TrackingSpellLOL",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _icon.DoubleClick += (s, e) => ToggleOverlay();
    }

    private static Icon GetAppTrayIcon()
    {
        try
        {
            string localPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "stopwatchcolor.png");
            string absPath   = @"D:\Downloads\TrackingSpellLOL\src\assets\stopwatchcolor.png";
            string fallback  = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "logo.png");

            string target = System.IO.File.Exists(localPath) ? localPath :
                            System.IO.File.Exists(absPath)   ? absPath   : fallback;

            if (System.IO.File.Exists(target))
            {
                using var bmp = new Bitmap(target);
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
        catch { }
        return SystemIcons.Application;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show Overlay", null, (s, e) => ShowOverlay());
        var hideItem = new ToolStripMenuItem("Hide Overlay", null, (s, e) => HideOverlay());

        var teamItem = new ToolStripMenuItem("Display Team");
        foreach (string t in new[] { "Enemy Team", "My Team", "Both" })
        {
            var item = new ToolStripMenuItem(t, null, (s, e) => SetTeam(t)) { Checked = _settings.DisplayTeam == t };
            teamItem.DropDownItems.Add(item);
        }

        var scaleItem = new ToolStripMenuItem("HUD Scale (Kích thước)");
        foreach (float scale in new[] { 0.8f, 1.0f, 1.2f, 1.4f, 1.6f })
        {
            int pct = (int)(scale * 100);
            var item = new ToolStripMenuItem($"{pct}%", null, (s, e) => SetScale(scale))
            {
                Checked = Math.Abs(_settings.UIScale - scale) < 0.05f
            };
            scaleItem.DropDownItems.Add(item);
        }

        var ctItem = new ToolStripMenuItem("Lock Position (Khoá vị trí HUD)", null,
            (s, e) => ToggleClickThrough()) { Checked = _settings.IsLocked || _settings.ClickThrough };

        var opacityItem = new ToolStripMenuItem("Opacity…");
        foreach (int p in new[] {20,30,40,50,60,70,80,90,100})
        {
            var op = new ToolStripMenuItem($"{p}%", null,
                (s, e) => SetOpacity(p)) { Checked = _settings.Opacity == p };
            opacityItem.DropDownItems.Add(op);
        }

        var topQuestItem = new ToolStripMenuItem("Unlock Top Quest Teleport", null,
            (s, e) => _overlay.Invoke(new Action(_overlay.ToggleTopRoleQuestTP)));

        var resetItem = new ToolStripMenuItem("Reset All Timers", null,
            (s, e) => _overlay.Invoke(new Action(_overlay.ResetAllTimers)));

        var reloadDataItem = new ToolStripMenuItem("Reload Game Data", null,
            (s, e) => _overlay.Invoke(new Action(() => _overlay.Invalidate())));

        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => Application.Exit());

        menu.Items.AddRange(new ToolStripItem[]
        {
            showItem, hideItem, new ToolStripSeparator(),
            teamItem, scaleItem, topQuestItem, ctItem, opacityItem,
            new ToolStripSeparator(), resetItem, reloadDataItem, new ToolStripSeparator(), exitItem
        });

        return menu;
    }

    private void ShowOverlay() => _overlay?.Show();
    private void HideOverlay() => _overlay?.Hide();
    private void ToggleOverlay()
    {
        if (_overlay.Visible) HideOverlay();
        else ShowOverlay();
    }

    private void SetTeam(string team)
    {
        _settings.DisplayTeam = team;
        Settings.Save(_settings);
        _overlay.Invalidate();
    }

    private void SetScale(float scale)
    {
        _settings.UIScale = scale;
        Settings.Save(_settings);
        _overlay.ApplyScaleAndRecalculateLayout();
    }

    private void ToggleClickThrough()
    {
        bool newState = !(_settings.IsLocked || _settings.ClickThrough);
        _settings.IsLocked = newState;
        _settings.ClickThrough = newState;
        _overlay.UpdateClickThrough();
        Settings.Save(_settings);
    }

    private void SetOpacity(int pct)
    {
        _settings.Opacity = pct;
        _overlay.Opacity = pct / 100.0;
        Settings.Save(_settings);
    }

    public void Dispose() => _icon.Dispose();
}