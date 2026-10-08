using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public class OverlayForm : Form
{
    private const int WS_EX_TRANSPARENT  = 0x20;
    private const int WS_EX_LAYERED      = 0x80000;
    private const int WS_EX_NOACTIVATE   = 0x08000000;
    private const int GWL_EXSTYLE        = -20;
    private const int WM_MOUSEACTIVATE   = 0x0021;
    private const int MA_NOACTIVATE      = 3;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int n);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int n, int v);

    private readonly TimerManager       _timerMgr;
    private readonly Settings           _settings;
    private readonly GameSessionManager  _sessionMgr = new GameSessionManager();
    private List<EnemyPlayer>           _players = new List<EnemyPlayer>();

    private bool _isDragging = false;
    private bool _isResizing = false;
    private Point _dragCursorPoint;
    private Point _dragFormPoint;

    private readonly string[] _fallbackLanes = { "TOP", "JUNGLE", "MID", "AD", "SUP" };

    public OverlayForm(TimerManager mgr, Settings settings)
    {
        _timerMgr = mgr;
        _settings = settings;

        this.Text            = "TrackingSpellLOL HUD";
        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost         = true;
        this.BackColor       = Color.FromArgb(10, 10, 10);
        this.TransparencyKey = Color.FromArgb(10, 10, 10);
        this.ForeColor       = Color.White;
        this.DoubleBuffered  = true;
        this.StartPosition   = FormStartPosition.Manual;
        this.Location        = new Point(_settings.X, _settings.Y);
        this.Opacity         = Math.Clamp(_settings.Opacity / 100.0, 0.2, 1.0);

        int ex = GetWindowLong(this.Handle, GWL_EXSTYLE);
        SetWindowLong(this.Handle, GWL_EXSTYLE, ex | WS_EX_LAYERED | WS_EX_NOACTIVATE);

        _sessionMgr.NewGameStarted += ResetAllTimers;

        this.MouseWheel += OverlayForm_MouseWheel;

        var uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        uiTimer.Tick += (s, e) => Invalidate();
        uiTimer.Start();

        var apiTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        apiTimer.Tick += async (s, e) => await PollGameDataAsync();
        apiTimer.Start();

        SetupDefaultEnemies();
        ApplyScaleAndRecalculateLayout();

        var logoImg = GetAppLogo();
        if (logoImg != null)
        {
            try
            {
                using var bmp = new Bitmap(logoImg, new Size(32, 32));
                this.Icon = Icon.FromHandle(bmp.GetHicon());
            }
            catch { }
        }
    }

    private void OverlayForm_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (_settings.IsLocked) return;

        if ((ModifierKeys & Keys.Control) == Keys.Control || (ModifierKeys & Keys.Shift) == Keys.Shift)
        {
            float delta = (e.Delta > 0) ? 0.05f : -0.05f;
            _settings.UIScale = Math.Clamp(_settings.UIScale + delta, 0.6f, 2.2f);
            Settings.Save(_settings);
            ApplyScaleAndRecalculateLayout();
        }
    }

    public void ApplyScaleAndRecalculateLayout()
    {
        float scale = Math.Clamp(_settings.UIScale, 0.6f, 2.2f);

        int topBarH = (int)(22 * scale); // Compact top bar
        int champSize = (int)(32 * scale);
        int spellSize = (int)(30 * scale);
        int arrowW = (int)(6 * scale);
        int pad = (int)(4 * scale);
        int margin = (int)(6 * scale);

        bool isTopWithQuest = (_players.Count > 0 && _players[0].TopQuestUnlocked &&
                               _players[0].Spell1.Name != "SummonerTeleport" &&
                               _players[0].Spell2.Name != "SummonerTeleport");

        int numSpells = isTopWithQuest ? 3 : 2;

        int totalW = margin + champSize + pad + arrowW + pad + (numSpells * spellSize) + ((numSpells - 1) * pad) + margin;
        int rowH = Math.Max(champSize, spellSize) + (int)(6 * scale);
        int totalH = topBarH + (int)(4 * scale) + (rowH * 5) + margin;

        this.ClientSize = new Size(totalW, totalH);
        Invalidate();
    }

    private async void SetupDefaultEnemies()
    {
        _players.Clear();

        var defaults = new[]
        {
            new { Champ = "Malphite", Pos = "TOP",    Spell2Name = "SummonerHaste",  Spell2Display = "Ghost",   CD2 = 240 },
            new { Champ = "LeeSin",   Pos = "JUNGLE", Spell2Name = "SummonerSmite",  Spell2Display = "Smite",   CD2 = 90 },
            new { Champ = "Yasuo",    Pos = "MID",    Spell2Name = "SummonerDot",    Spell2Display = "Ignite",  CD2 = 180 },
            new { Champ = "Nilah",    Pos = "AD",     Spell2Name = "SummonerHeal",   Spell2Display = "Heal",    CD2 = 240 },
            new { Champ = "Rell",     Pos = "SUP",    Spell2Name = "SummonerExhaust",Spell2Display = "Exhaust", CD2 = 210 }
        };

        foreach (var item in defaults)
        {
            var e = new EnemyPlayer
            {
                ChampionName = item.Champ,
                Position = item.Pos,
                ChampionIcon = await LiveClientApi.GetChampionIconAsync(item.Champ)
            };

            e.Spell1.Name = "SummonerFlash";
            e.Spell1.DisplayName = "Flash";
            e.Spell1.BaseCooldown = 300;
            e.Spell1.Icon = await LiveClientApi.GetSpellIconAsync("SummonerFlash");

            e.Spell2.Name = item.Spell2Name;
            e.Spell2.DisplayName = item.Spell2Display;
            e.Spell2.BaseCooldown = item.CD2;
            e.Spell2.Icon = await LiveClientApi.GetSpellIconAsync(item.Spell2Name);

            e.QuestTeleport.Name = "SummonerTeleport";
            e.QuestTeleport.DisplayName = "Teleport";
            e.QuestTeleport.BaseCooldown = 390;
            e.QuestTeleport.Icon = await LiveClientApi.GetSpellIconAsync("SummonerTeleport");

            _players.Add(e);
        }

        ApplyScaleAndRecalculateLayout();
    }

    private async Task PollGameDataAsync()
    {
        var fetched = await LiveClientApi.GetTeamDataAsync(_settings.DisplayTeam);
        bool isApiAvailable = (fetched != null && fetched.Count > 0);

        _sessionMgr.UpdateSession(isApiAvailable, isApiAvailable ? "ACTIVE_GAME" : "");

        if (isApiAvailable && fetched != null)
        {
            for (int i = 0; i < Math.Min(fetched.Count, 5); i++)
            {
                if (i < _players.Count)
                {
                    // Preserve active running timers so they are NEVER reset mid-countdown
                    if (_players[i].Spell1.Timer.IsRunning) fetched[i].Spell1.Timer = _players[i].Spell1.Timer;
                    if (_players[i].Spell2.Timer.IsRunning) fetched[i].Spell2.Timer = _players[i].Spell2.Timer;
                    if (_players[i].QuestTeleport.Timer.IsRunning) fetched[i].QuestTeleport.Timer = _players[i].QuestTeleport.Timer;

                    fetched[i].TopQuestUnlocked = _players[i].TopQuestUnlocked;
                }

                // Ensure spell icons are loaded
                if (fetched[i].Spell1.Icon == null && !string.IsNullOrEmpty(fetched[i].Spell1.Name))
                    fetched[i].Spell1.Icon = await LiveClientApi.GetSpellIconAsync(fetched[i].Spell1.Name);
                if (fetched[i].Spell2.Icon == null && !string.IsNullOrEmpty(fetched[i].Spell2.Name))
                    fetched[i].Spell2.Icon = await LiveClientApi.GetSpellIconAsync(fetched[i].Spell2.Name);

                if (fetched[i].QuestTeleport.Icon == null)
                {
                    fetched[i].QuestTeleport.Name = "SummonerTeleport";
                    fetched[i].QuestTeleport.DisplayName = "Teleport";
                    fetched[i].QuestTeleport.BaseCooldown = 390;
                    fetched[i].QuestTeleport.Icon = await LiveClientApi.GetSpellIconAsync("SummonerTeleport");
                }
            }
            _players = fetched;
            ApplyScaleAndRecalculateLayout();
        }
    }

    public void ToggleTopRoleQuestTP()
    {
        if (_players.Count > 0)
        {
            _players[0].TopQuestUnlocked = !_players[0].TopQuestUnlocked;
            ApplyScaleAndRecalculateLayout();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        float scale = Math.Clamp(_settings.UIScale, 0.6f, 2.2f);
        int topBarH = (int)(22 * scale);
        int margin = (int)(6 * scale);

        // Smooth Dark Rounded Panel Container
        Rectangle containerBounds = new Rectangle(1, 1, ClientSize.Width - 2, ClientSize.Height - 2);
        int borderRadius = Math.Max(4, (int)(10 * scale));

        using (var bgPath = CreateRoundedRectanglePath(containerBounds, borderRadius))
        using (var bgBrush = new SolidBrush(Color.FromArgb(225, 16, 18, 24)))
        using (var borderPen = new Pen(Color.FromArgb(100, 220, 230, 245), 1.2f))
        {
            g.FillPath(bgBrush, bgPath);
            g.DrawPath(borderPen, bgPath);
        }

        // Draw Extended Top Bar Header (Padded & Balanced)
        int btnSize = Math.Max(12, (int)(13 * scale));
        int btnY = margin + (topBarH - btnSize) / 2 - (int)(2 * scale);

        // 1. Lock Button (Top Left)
        int lockX = margin + (int)(2 * scale);
        Rectangle lockBtnRect = new Rectangle(lockX, btnY, btnSize, btnSize);
        DrawLockIcon(g, lockBtnRect, _settings.IsLocked);

        // 2. 'X' Close Button (Top Right)
        Rectangle closeBtnRect = new Rectangle(ClientSize.Width - margin - (int)(2 * scale) - btnSize, btnY, btnSize, btnSize);
        DrawCloseIcon(g, closeBtnRect);

        // 3. "Made by trancongdinh" text in middle between Lock and Close X (Auto-fit font size)
        int creditLeft = lockX + btnSize + (int)(3 * scale);
        int creditRight = closeBtnRect.X - (int)(3 * scale);
        int creditW = creditRight - creditLeft;

        if (creditW > 15)
        {
            string creditText = "Made by trancongdinh";
            float creditFontSize = 5.8f * scale;

            while (creditFontSize > 3.2f)
            {
                using var fontTest = new Font("Segoe UI", creditFontSize, FontStyle.Bold);
                SizeF sz = g.MeasureString(creditText, fontTest);
                if (sz.Width <= creditW) break;
                creditFontSize -= 0.3f;
            }

            Rectangle creditRect = new Rectangle(creditLeft, margin - (int)(2 * scale), creditW, topBarH);
            using var creditFont = new Font("Segoe UI", creditFontSize, FontStyle.Bold);
            using var creditBrush = new SolidBrush(Color.FromArgb(95, 175, 190, 215));
            var creditSf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(creditText, creditFont, creditBrush, creditRect, creditSf);
        }

        // 4. Short Subtle Dim Centered Horizontal Divider Line
        int dividerY = topBarH + (int)(1 * scale);
        int lineW = (int)(ClientSize.Width * 0.45); // Short line centered in middle
        int lineX = (ClientSize.Width - lineW) / 2;
        using (var linePen = new Pen(Color.FromArgb(50, 255, 255, 255), 1.0f))
        {
            g.DrawLine(linePen, lineX, dividerY, lineX + lineW, dividerY);
        }

        int contentY = topBarH + (int)(4 * scale);
        int contentH = ClientSize.Height - contentY - margin;
        int rowH = contentH / 5;

        int champSize = (int)(32 * scale);
        int spellSize = (int)(30 * scale);
        int arrowW = (int)(6 * scale);
        int pad = (int)(4 * scale);

        for (int i = 0; i < 5; i++)
        {
            int rowY = contentY + (i * rowH);
            EnemyPlayer enemy = (i < _players.Count) ? _players[i] : new EnemyPlayer { ChampionName = _fallbackLanes[i] };
            bool isTopWithQuest = (i == 0 && enemy.TopQuestUnlocked && enemy.Spell1.Name != "SummonerTeleport" && enemy.Spell2.Name != "SummonerTeleport");

            // Tick timers
            enemy.Spell1.Timer.Tick();
            enemy.Spell2.Timer.Tick();
            if (isTopWithQuest) enemy.QuestTeleport.Timer.Tick();

            // Check if champion has ANY active cooldown
            bool isAnySpellActive = enemy.Spell1.Timer.IsRunning || enemy.Spell2.Timer.IsRunning || (isTopWithQuest && enemy.QuestTeleport.Timer.IsRunning);

            // Champion Portrait
            int champX = margin;
            int champY = rowY + (rowH - champSize) / 2;
            Rectangle champRect = new Rectangle(champX, champY, champSize, champSize);

            if (enemy.ChampionIcon != null)
            {
                using var path = new GraphicsPath();
                path.AddEllipse(champRect);
                g.SetClip(path);

                if (isAnySpellActive)
                {
                    g.DrawImage(enemy.ChampionIcon, champRect);
                }
                else
                {
                    using var ia = new ImageAttributes();
                    ColorMatrix cm = new ColorMatrix(new float[][]
                    {
                        new float[] {0.6f, 0, 0, 0, 0},
                        new float[] {0, 0.6f, 0, 0, 0},
                        new float[] {0, 0, 0.6f, 0, 0},
                        new float[] {0, 0, 0, 1.0f, 0},
                        new float[] {0, 0, 0, 0, 1.0f}
                    });
                    ia.SetColorMatrix(cm);
                    g.DrawImage(enemy.ChampionIcon, champRect, 0, 0, enemy.ChampionIcon.Width, enemy.ChampionIcon.Height, GraphicsUnit.Pixel, ia);
                }

                g.ResetClip();

                using var ringPen = new Pen(isAnySpellActive ? Color.FromArgb(240, 245, 255) : Color.FromArgb(120, 130, 140), 2.2f);
                g.DrawEllipse(ringPen, champRect);
            }
            else
            {
                using var bgBrush = new SolidBrush(Color.FromArgb(40, 45, 55));
                g.FillEllipse(bgBrush, champRect);
                using var ringPen = new Pen(Color.FromArgb(160, 170, 180), 2.2f);
                g.DrawEllipse(ringPen, champRect);
            }

            // Arrow (▶)
            int arrowX = champRect.Right + pad;
            int arrowCenterY = rowY + (rowH / 2);
            Point[] arrowPoints = new[]
            {
                new Point(arrowX, arrowCenterY - (int)(4 * scale)),
                new Point(arrowX + arrowW, arrowCenterY),
                new Point(arrowX, arrowCenterY + (int)(4 * scale))
            };
            using var arrowBrush = new SolidBrush(Color.FromArgb(180, 190, 200));
            g.FillPolygon(arrowBrush, arrowPoints);

            // Spells
            int spellStartX = arrowX + arrowW + pad;
            int spellY = rowY + (rowH - spellSize) / 2;

            Rectangle spell1Rect = new Rectangle(spellStartX, spellY, spellSize, spellSize);
            DrawClockwiseSpellIcon(g, enemy.Spell1, spell1Rect, scale);

            Rectangle spell2Rect = new Rectangle(spellStartX + spellSize + pad, spellY, spellSize, spellSize);
            DrawClockwiseSpellIcon(g, enemy.Spell2, spell2Rect, scale);

            if (isTopWithQuest)
            {
                Rectangle spell3Rect = new Rectangle(spellStartX + (spellSize * 2) + (pad * 2), spellY, spellSize, spellSize);
                DrawClockwiseSpellIcon(g, enemy.QuestTeleport, spell3Rect, scale);
            }
        }

        // Draw resize grip if NOT locked
        if (!_settings.IsLocked)
        {
            int rSize = (int)(8 * scale);
            Point[] rPoints = new[]
            {
                new Point(ClientSize.Width - rSize, ClientSize.Height - 2),
                new Point(ClientSize.Width - 2, ClientSize.Height - rSize),
                new Point(ClientSize.Width - 2, ClientSize.Height - 2)
            };
            using var resizeGripBrush = new SolidBrush(Color.FromArgb(120, 255, 255, 255));
            g.FillPolygon(resizeGripBrush, rPoints);
        }
    }

    private void DrawLockIcon(Graphics g, Rectangle rect, bool isLocked)
    {
        int bodyY = rect.Y + (rect.Height / 2) - 1;
        int bodyH = rect.Height - (rect.Height / 2) + 1;
        Rectangle bodyRect = new Rectangle(rect.X + 2, bodyY, rect.Width - 4, bodyH);

        Rectangle shackleRect = new Rectangle(rect.X + 4, rect.Y, rect.Width - 8, rect.Height / 2 + 2);

        if (isLocked)
        {
            // LOCKED: Solid White Fill
            using var whiteBrush = new SolidBrush(Color.White);
            using var penShackle = new Pen(Color.White, 2.0f);

            g.DrawArc(penShackle, shackleRect, 180, 180);
            g.FillRectangle(whiteBrush, bodyRect);
        }
        else
        {
            // UNLOCKED: Light grey stroke, empty fill
            using var outlinePen = new Pen(Color.FromArgb(180, 190, 200), 1.5f);

            g.DrawArc(outlinePen, shackleRect, 180, 180);
            g.DrawRectangle(outlinePen, bodyRect);
        }
    }

    private void DrawCloseIcon(Graphics g, Rectangle rect)
    {
        using var pen = new Pen(Color.FromArgb(180, 190, 200), 1.8f);
        g.DrawLine(pen, rect.X + 2, rect.Y + 2, rect.Right - 2, rect.Bottom - 2);
        g.DrawLine(pen, rect.Right - 2, rect.Y + 2, rect.X + 2, rect.Bottom - 2);
    }



    private void DrawClockwiseSpellIcon(Graphics g, SpellInfo spell, Rectangle rect, float scale)
    {
        bool isRunning = spell.Timer.IsRunning;
        int cornerRadius = (int)(5 * scale);

        using var path = CreateRoundedRectanglePath(rect, cornerRadius);

        g.SetClip(path);
        if (spell.Icon != null)
        {
            if (isRunning)
            {
                g.DrawImage(spell.Icon, rect);
            }
            else
            {
                using var ia = new ImageAttributes();
                ColorMatrix cm = new ColorMatrix(new float[][]
                {
                    new float[] {0.6f, 0, 0, 0, 0},
                    new float[] {0, 0.6f, 0, 0, 0},
                    new float[] {0, 0, 0.6f, 0, 0},
                    new float[] {0, 0, 0, 1.0f, 0},
                    new float[] {0, 0, 0, 0, 1.0f}
                });
                ia.SetColorMatrix(cm);
                g.DrawImage(spell.Icon, rect, 0, 0, spell.Icon.Width, spell.Icon.Height, GraphicsUnit.Pixel, ia);
            }
        }
        else
        {
            using var bg = new SolidBrush(Color.FromArgb(35, 40, 50));
            g.FillRectangle(bg, rect);
        }

        Color stateColor = Color.White;

        if (isRunning)
        {
            var ts = spell.Timer.Remaining;
            int totalSeconds = (int)Math.Ceiling(ts.TotalSeconds);
            int baseCd = spell.BaseCooldown > 0 ? spell.BaseCooldown : 300;

            if (totalSeconds > 30)
            {
                stateColor = Color.White;
            }
            else if (totalSeconds > 0)
            {
                stateColor = Color.FromArgb(245, 50, 50); // Red
            }
            else
            {
                stateColor = Color.FromArgb(255, 140, 0); // Orange
            }

            if (totalSeconds > 0)
            {
                double elapsed = spell.Timer.ElapsedSeconds;
                double progressRatio = Math.Clamp(elapsed / baseCd, 0.0, 1.0);
                float sweepAngle = (float)(progressRatio * 360.0);

                float remainingAngle = 360.0f - sweepAngle;
                float startAngle = -90.0f + sweepAngle;

                if (remainingAngle > 0.1f)
                {
                    Rectangle pieSquare = new Rectangle(
                        rect.X - rect.Width / 2,
                        rect.Y - rect.Height / 2,
                        rect.Width * 2,
                        rect.Height * 2);

                    using var overlayBrush = new SolidBrush(Color.FromArgb(175, 5, 5, 10));
                    g.FillPie(overlayBrush, pieSquare, startAngle, remainingAngle);
                }
            }
            else
            {
                using var overlayBrush = new SolidBrush(Color.FromArgb(175, 5, 5, 10));
                g.FillRectangle(overlayBrush, rect);
            }

            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };

            string text;
            Brush textBrush;

            if (totalSeconds > 30)
            {
                text = $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
                textBrush = Brushes.White;
            }
            else if (totalSeconds > 0)
            {
                text = totalSeconds.ToString();
                textBrush = new SolidBrush(Color.FromArgb(245, 50, 50));
            }
            else
            {
                text = totalSeconds.ToString();
                textBrush = new SolidBrush(Color.FromArgb(255, 140, 0));
            }

            float fontSize = (totalSeconds <= 30 && totalSeconds > -30) ? 11.5f * scale : 8.5f * scale;
            using var font = FindBestFittingFont(g, text, rect.Width - 2, fontSize);

            DrawTextWithBlackOutline(g, text, font, textBrush, rect, sf, scale);
        }

        g.ResetClip();

        // 1. Always draw dim background border
        using (var idlePen = new Pen(Color.FromArgb(80, 85, 100), 1.8f * scale))
        {
            g.DrawPath(idlePen, path);
        }

        // 2. If running, draw bright active border sweeping clockwise starting at 12 o'clock (-90 deg)
        if (isRunning)
        {
            var ts = spell.Timer.Remaining;
            int totalSeconds = (int)Math.Ceiling(ts.TotalSeconds);
            int baseCd = spell.BaseCooldown > 0 ? spell.BaseCooldown : 300;

            if (totalSeconds > 0)
            {
                double elapsed = spell.Timer.ElapsedSeconds;
                double progressRatio = Math.Clamp(elapsed / baseCd, 0.0, 1.0);
                float sweepAngle = (float)(progressRatio * 360.0);

                if (sweepAngle > 0.5f)
                {
                    Rectangle pieSquare = new Rectangle(
                        rect.X - rect.Width,
                        rect.Y - rect.Height,
                        rect.Width * 3,
                        rect.Height * 3);

                    using var clipPie = new GraphicsPath();
                    clipPie.AddPie(pieSquare, -90.0f, sweepAngle);

                    var saveState = g.Save();
                    g.SetClip(clipPie, CombineMode.Intersect);

                    using (var activePen = new Pen(stateColor, 3.0f * scale))
                    {
                        g.DrawPath(activePen, path);
                    }
                    g.Restore(saveState);
                }
            }
            else
            {
                using (var activePen = new Pen(stateColor, 3.0f * scale))
                {
                    g.DrawPath(activePen, path);
                }
            }
        }
    }

    private void DrawTextWithBlackOutline(Graphics g, string text, Font font, Brush textBrush, Rectangle rect, StringFormat sf, float scale)
    {
        try
        {
            using var textPath = new GraphicsPath();
            FontFamily fontFamily = font.FontFamily;
            int fontStyle = (int)font.Style;
            float emSize = font.SizeInPoints * g.DpiY / 72.0f;

            textPath.AddString(text, fontFamily, fontStyle, emSize, rect, sf);

            using var outlinePen = new Pen(Color.Black, Math.Max(2.5f, 3.0f * scale))
            {
                LineJoin = LineJoin.Round
            };
            g.DrawPath(outlinePen, textPath);
            g.FillPath(textBrush, textPath);
        }
        catch
        {
            g.DrawString(text, font, textBrush, rect, sf);
        }
    }

    private Font FindBestFittingFont(Graphics g, string text, int maxWidth, float startSize)
    {
        float size = startSize;
        while (size > 5.0f)
        {
            using var font = new Font("Segoe UI", size, FontStyle.Bold);
            SizeF sz = g.MeasureString(text, font);
            if (sz.Width <= maxWidth)
            {
                return new Font("Segoe UI", size, FontStyle.Bold);
            }
            size -= 0.5f;
        }
        return new Font("Segoe UI", 5.0f, FontStyle.Bold);
    }

    private GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int cornerRadius)
    {
        GraphicsPath path = new GraphicsPath();
        int diameter = Math.Max(2, cornerRadius * 2);
        Rectangle arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.X;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left)
        {
            float scale = Math.Clamp(_settings.UIScale, 0.6f, 2.2f);
            int topBarH = (int)(22 * scale);
            int margin = (int)(6 * scale);
            int btnSize = Math.Max(12, (int)(13 * scale));
            int btnY = margin + (topBarH - btnSize) / 2 - (int)(2 * scale);

            int lockX = margin + (int)(2 * scale);

            // Check Lock Button click (Top Left)
            Rectangle lockBtnRect = new Rectangle(lockX - 3, btnY - 3, btnSize + 6, btnSize + 6);
            if (lockBtnRect.Contains(e.Location))
            {
                _settings.IsLocked = !_settings.IsLocked;
                Settings.Save(_settings);
                Invalidate();
                return;
            }

            // Check 'X' Close Button click (Top Right)
            Rectangle closeBtnRect = new Rectangle(ClientSize.Width - margin - (int)(2 * scale) - btnSize - 6, btnY, btnSize + 6, btnSize + 6);
            if (closeBtnRect.Contains(e.Location))
            {
                this.Hide();
                return;
            }

            int contentY = topBarH + (int)(4 * scale);
            int contentH = ClientSize.Height - contentY - margin;
            int rowH = contentH / 5;

            if (e.Y >= contentY)
            {
                int laneIdx = (e.Y - contentY) / rowH;

                if (laneIdx >= 0 && laneIdx < _players.Count)
                {
                    var enemy = _players[laneIdx];
                    int rowY = contentY + (laneIdx * rowH);

                    bool isTopWithQuest = (laneIdx == 0 && enemy.TopQuestUnlocked && enemy.Spell1.Name != "SummonerTeleport" && enemy.Spell2.Name != "SummonerTeleport");

                    int champSize = (int)(32 * scale);
                    int spellSize = (int)(30 * scale);
                    int arrowW = (int)(6 * scale);
                    int pad = (int)(4 * scale);

                    int champX = margin;
                    int arrowX = champX + champSize + pad;
                    int spellStartX = arrowX + arrowW + pad;
                    int spellY = rowY + (rowH - spellSize) / 2;

                    Rectangle spell1Rect = new Rectangle(spellStartX, spellY, spellSize, spellSize);
                    Rectangle spell2Rect = new Rectangle(spellStartX + spellSize + pad, spellY, spellSize, spellSize);
                    Rectangle spell3Rect = new Rectangle(spellStartX + (spellSize * 2) + (pad * 2), spellY, spellSize, spellSize);

                    if (spell1Rect.Contains(e.Location))
                    {
                        ToggleSpellTimer(enemy.Spell1, enemy, isQuestTp: false);
                        Invalidate();
                        return;
                    }

                    if (spell2Rect.Contains(e.Location))
                    {
                        ToggleSpellTimer(enemy.Spell2, enemy, isQuestTp: false);
                        Invalidate();
                        return;
                    }

                    if (isTopWithQuest && spell3Rect.Contains(e.Location))
                    {
                        ToggleSpellTimer(enemy.QuestTeleport, enemy, isQuestTp: true);
                        Invalidate();
                        return;
                    }
                }
            }

            // If Locked, disable moving and resizing on background click
            if (_settings.IsLocked || _settings.ClickThrough) return;

            // Check Resize Grip click (Bottom Right)
            Rectangle resizeGrip = new Rectangle(ClientSize.Width - 16, ClientSize.Height - 16, 16, 16);
            if (resizeGrip.Contains(e.Location))
            {
                _isResizing = true;
                _dragCursorPoint = Cursor.Position;
                return;
            }

            _isDragging = true;
            _dragCursorPoint = Cursor.Position;
            _dragFormPoint = this.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_settings.IsLocked || _settings.ClickThrough)
        {
            this.Cursor = Cursors.Default;
            return;
        }

        Rectangle resizeGrip = new Rectangle(ClientSize.Width - 16, ClientSize.Height - 16, 16, 16);
        if (resizeGrip.Contains(e.Location))
        {
            this.Cursor = Cursors.SizeNWSE;
        }
        else if (!_isDragging && !_isResizing)
        {
            this.Cursor = Cursors.Default;
        }

        if (_isResizing)
        {
            Point diff = Point.Subtract(Cursor.Position, new Size(_dragCursorPoint));
            float scaleDelta = (float)diff.Y / 150.0f;
            float newScale = Math.Clamp(_settings.UIScale + scaleDelta, 0.6f, 2.2f);
            _settings.UIScale = newScale;
            _dragCursorPoint = Cursor.Position;
            Settings.Save(_settings);
            ApplyScaleAndRecalculateLayout();
        }
        else if (_isDragging)
        {
            Point dif = Point.Subtract(Cursor.Position, new Size(_dragCursorPoint));
            this.Location = Point.Add(_dragFormPoint, new Size(dif));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _isDragging = false;
        _isResizing = false;
        this.Cursor = Cursors.Default;
    }

    private void ToggleSpellTimer(SpellInfo spell, EnemyPlayer enemy, bool isQuestTp)
    {
        if (spell.Timer.IsRunning)
        {
            spell.Timer.Cancel();
        }
        else
        {
            int cd;
            if (isQuestTp || spell.Name.Contains("Teleport", StringComparison.OrdinalIgnoreCase))
            {
                cd = TeleportCooldownResolver.CalculateUnleashedTeleportCooldown(enemy.Level, enemy.HasCosmicInsight, enemy.HasIonianBoots);
            }
            else
            {
                cd = LiveClientApi.CalculateCooldown(spell.BaseCooldown, enemy.HasCosmicInsight, enemy.HasIonianBoots);
            }

            spell.Timer.Start(cd);
        }
    }

    public void ResetAllTimers()
    {
        foreach (var p in _players)
        {
            p.Spell1.Timer.Cancel();
            p.Spell2.Timer.Cancel();
            p.QuestTeleport.Timer.Cancel();
        }
        Invalidate();
    }

    public void UpdateClickThrough()
    {
        int ex = GetWindowLong(Handle, GWL_EXSTYLE);
        ex &= ~WS_EX_TRANSPARENT;
        ex |= WS_EX_NOACTIVATE;
        SetWindowLong(Handle, GWL_EXSTYLE, ex | WS_EX_LAYERED);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }

    private static Image? _appLogoImage = null;

    private static Image? GetAppLogo()
    {
        if (_appLogoImage != null) return _appLogoImage;

        string localPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "stopwatchcolor.png");
        string absPath   = @"D:\Downloads\TrackingSpellLOL\src\assets\stopwatchcolor.png";
        string fallback  = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "logo.png");

        string target = System.IO.File.Exists(localPath) ? localPath :
                        System.IO.File.Exists(absPath)   ? absPath   : fallback;

        if (System.IO.File.Exists(target))
        {
            try { _appLogoImage = Image.FromFile(target); return _appLogoImage; } catch { }
        }
        return null;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _settings.X      = Location.X;
        _settings.Y      = Location.Y;
        _settings.Width  = ClientSize.Width;
        _settings.Height = ClientSize.Height;
        _settings.Opacity = (int)(Opacity * 100);
        Settings.Save(_settings);
        base.OnFormClosing(e);
    }
}