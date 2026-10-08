using System;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
            MessageBox.Show(e.Exception.ToString(), "Lỗi ứng dụng",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            MessageBox.Show(e.ExceptionObject?.ToString(), "Lỗi nghiêm trọng",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);

        try
        {
            var settings = Settings.Load();
            var timerMgr = new TimerManager();

            var overlay = new OverlayForm(timerMgr, settings);

            // System tray service
            using var tray = new TrayService(overlay, settings);

            Application.Run(overlay);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Lỗi khởi động",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}