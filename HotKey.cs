using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Nuvo;

/// <summary>
/// Global key without modifiers via a low-level keyboard hook. RegisterHotKey is not enough for PrtScr:
/// Windows hands the key to Snipping Tool first, while the hook sees it earlier and can swallow it.
/// </summary>
sealed class HotKey : IDisposable
{
    const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;

    delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);

    readonly uint _vk;
    readonly HookProc _proc; // kept in a field so the GC doesn't collect the native callback
    readonly IntPtr _hook;
    readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
    bool _down;

    public bool Registered => _hook != IntPtr.Zero;
    public event Action? Pressed;

    public HotKey(uint vk)
    {
        _vk = vk;
        _proc = Hook;
        using var m = Process.GetCurrentProcess().MainModule!;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(m.ModuleName), 0);
    }

    IntPtr Hook(int code, IntPtr w, IntPtr l)
    {
        if (code >= 0 && Marshal.ReadInt32(l) == _vk) // KBDLLHOOKSTRUCT.vkCode
        {
            int msg = w.ToInt32();
            if (msg is WM_KEYDOWN or WM_SYSKEYDOWN)
            {
                if (!_down) { _down = true; _ui.BeginInvoke(() => Pressed?.Invoke()); } // return from the hook fast
                return (IntPtr)1;
            }
            if (msg is WM_KEYUP or WM_SYSKEYUP) { _down = false; return (IntPtr)1; }
        }
        return CallNextHookEx(_hook, code, w, l);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
    }

    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc p, IntPtr mod, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
}
