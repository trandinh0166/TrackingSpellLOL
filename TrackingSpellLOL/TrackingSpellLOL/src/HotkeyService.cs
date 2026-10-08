using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class HotkeyService : IDisposable
{
    private readonly IntPtr _windowHandle;
    private readonly Dictionary<int, Action> _actions = new();
    private int _currentId = 0;

    public HotkeyService()
    {
        var form = new NativeWindow();
        form.CreateHandle(new CreateParams());
        _windowHandle = form.Handle;
        Application.AddMessageFilter(new HotkeyMessageFilter(_windowHandle, OnHotkey));
    }

    public void RegisterHotKey(Keys key, Action callback)
    {
        int id = ++_currentId;
        _actions[id] = callback;
        const uint MOD_NONE = 0x0000;
        if (!RegisterHotKey(_windowHandle, id, MOD_NONE, (uint)key))
            throw new InvalidOperationException($"Failed to register hotkey {key}");
    }

    private void OnHotkey(int id)
    {
        if (_actions.TryGetValue(id, out var act)) act?.Invoke();
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys)
            UnregisterHotKey(_windowHandle, id);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

internal class HotkeyMessageFilter : IMessageFilter
{
    private const int WM_HOTKEY = 0x0312;
    private readonly IntPtr _hWnd;
    private readonly Action<int> _callback;

    public HotkeyMessageFilter(IntPtr hWnd, Action<int> callback)
    {
        _hWnd = hWnd;
        _callback = callback;
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.HWnd == _hWnd)
        {
            int id = m.WParam.ToInt32();
            _callback(id);
            return true;
        }
        return false;
    }
}