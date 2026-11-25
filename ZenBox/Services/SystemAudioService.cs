using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ZenBox.Services;

public sealed class SystemAudioService
{
    private const int WM_APPCOMMAND = 0x0319;
    private const int APPCOMMAND_VOLUME_DOWN = 9;
    private const int APPCOMMAND_VOLUME_UP = 10;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    public void VolumeUp()
    {
        SendAppCommand(APPCOMMAND_VOLUME_UP);
    }

    public void VolumeDown()
    {
        SendAppCommand(APPCOMMAND_VOLUME_DOWN);
    }

    private static void SendAppCommand(int command)
    {
        IntPtr hwnd = IntPtr.Zero;

        if (Application.Current?.MainWindow is Window mainWin)
        {
            hwnd = new WindowInteropHelper(mainWin).Handle;
        }

        if (hwnd == IntPtr.Zero)
        {
            hwnd = GetForegroundWindow();
        }

        if (hwnd != IntPtr.Zero)
        {
            var lParam = (IntPtr)(command << 16);
            SendMessageW(hwnd, WM_APPCOMMAND, hwnd, lParam);
        }
    }
}
