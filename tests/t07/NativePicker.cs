using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

// Drives the actual Windows IFileDialog from a worker while WPF owns its modal
// loop. No picker abstraction or product test hook is introduced. Timed failure
// always requests dismissal, preventing a stuck CI desktop.
// The native union includes fields populated only by Windows layout.
#pragma warning disable CS0649
internal static class NativePicker
{
    public static long Select(IntPtr owner, string path) => Drive(owner, path);
    public static long Cancel(IntPtr owner) => Drive(owner, null);

    private static long Drive(IntPtr owner, string? path)
    {
        IntPtr dialog = IntPtr.Zero;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (dialog == IntPtr.Zero && DateTime.UtcNow < deadline)
            {
                EnumWindows((hwnd, _) =>
                {
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    var name = new StringBuilder(256); GetClassName(hwnd, name, name.Capacity);
                    if (pid == Environment.ProcessId && name.ToString() == "#32770" && GetWindow(hwnd, 4) == owner && IsWindowVisible(hwnd))
                    { dialog = hwnd; return false; }
                    return true;
                }, IntPtr.Zero);
                if (dialog == IntPtr.Zero) Thread.Sleep(30);
            }
            if (dialog == IntPtr.Zero) throw new InvalidOperationException("Native folder picker did not appear within 20 seconds");
            if (path is null) { PostMessage(dialog, 0x0010, IntPtr.Zero, IntPtr.Zero); return dialog.ToInt64(); }
            SetForegroundWindow(dialog); Thread.Sleep(200);
            Key(0x12, false); Key(0x44, false); Key(0x44, true); Key(0x12, true); // Alt+D address bar
            Thread.Sleep(200);
            foreach (char ch in path)
            {
                Input(new INPUT { type = 1, data = new InputUnion { keyboard = new KEYBDINPUT { scan = ch, flags = 4 } } });
                Input(new INPUT { type = 1, data = new InputUnion { keyboard = new KEYBDINPUT { scan = ch, flags = 6 } } });
            }
            Key(0x0D, false); Key(0x0D, true);
            Thread.Sleep(1200);
            // IDOK=1 is the native dialog's Select Folder button, independent of
            // Windows display language. Navigation above selects the folder.
            IntPtr ok = IntPtr.Zero;
            EnumChildWindows(dialog, (hwnd, _) =>
            {
                var cls = new StringBuilder(64); GetClassName(hwnd, cls, cls.Capacity);
                if (GetDlgCtrlID(hwnd) == 1 && cls.ToString() == "Button" && IsWindowVisible(hwnd)) { ok = hwnd; return false; }
                return true;
            }, IntPtr.Zero);
            if (ok == IntPtr.Zero) throw new InvalidOperationException("Native Select Folder button IDOK was not found");
            PostMessage(ok, 0x00F5, IntPtr.Zero, IntPtr.Zero); // BM_CLICK, async to keep timeout active
            deadline = DateTime.UtcNow.AddSeconds(8);
            while (IsWindowVisible(dialog) && DateTime.UtcNow < deadline) Thread.Sleep(30);
            if (IsWindowVisible(dialog)) throw new InvalidOperationException("Native folder selection did not close the dialog");
            return dialog.ToInt64();
        }
        finally
        {
            if (dialog == IntPtr.Zero)
            {
                EnumWindows((hwnd, _) =>
                {
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    var cls = new StringBuilder(64); GetClassName(hwnd, cls, cls.Capacity);
                    if (pid == Environment.ProcessId && cls.ToString() == "#32770" && IsWindowVisible(hwnd))
                        PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }, IntPtr.Zero);
            }
            if (dialog != IntPtr.Zero && IsWindowVisible(dialog)) PostMessage(dialog, 0x0010, IntPtr.Zero, IntPtr.Zero);
        }
    }
    private static void Key(ushort key, bool up) => Input(new INPUT { type = 1, data = new InputUnion { keyboard = new KEYBDINPUT { vk = key, flags = up ? 2u : 0u } } });
    private static void Input(INPUT input)
    {
        if (SendInput(1, [input], Marshal.SizeOf<INPUT>()) != 1) throw new InvalidOperationException("Windows did not accept native picker input: " + Marshal.GetLastWin32Error());
    }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT keyboard;
        [FieldOffset(0)] public MOUSEINPUT mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx, dy; public uint mouseData, flags, time; public UIntPtr extra; }
    private delegate bool EnumProc(IntPtr hwnd, IntPtr extra);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr extra);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr extra);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int maximum);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
}
