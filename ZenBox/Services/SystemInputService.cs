using System.Runtime.InteropServices;
using System.Windows;

namespace ZenBox.Services;

public sealed class SystemInputService
{
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, UIntPtr dwExtraInfo);

    public void MoveCursorRelative(double dx, double dy)
    {
        if (!GetCursorPos(out var current))
        {
            return;
        }

        var maxW = (int)Math.Max(1920, SystemParameters.PrimaryScreenWidth * 2);
        var maxH = (int)Math.Max(1080, SystemParameters.PrimaryScreenHeight * 2);
        var nextX = Math.Clamp(current.X + (int)Math.Round(dx), 0, maxW);
        var nextY = Math.Clamp(current.Y + (int)Math.Round(dy), 0, maxH);
        SetCursorPos(nextX, nextY);
    }

    public void LeftClickAtCursor()
    {
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    public void ScrollVertical(double deltaY)
    {
        int wheelAmount = (int)Math.Round(deltaY * 3.0);
        if (wheelAmount != 0)
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, wheelAmount, UIntPtr.Zero);
        }
    }
}
