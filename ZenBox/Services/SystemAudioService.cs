using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ZenBox.Services;

public sealed class SystemAudioService
{
    private const int WM_APPCOMMAND = 0x0319;
    private const int APPCOMMAND_VOLUME_MUTE = 8;
    private const int APPCOMMAND_VOLUME_DOWN = 9;
    private const int APPCOMMAND_VOLUME_UP = 10;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    public int GetMasterVolumePercent()
    {
        try
        {
            var endpoint = GetAudioEndpointVolume();
            if (endpoint is not null)
            {
                endpoint.GetMasterVolumeLevelScalar(out var level);
                return (int)Math.Round(Math.Clamp(level, 0f, 1f) * 100.0);
            }
        }
        catch
        {
            // Fallback an toàn nếu máy không có thiết bị đầu ra âm thanh
        }

        return 60;
    }

    public void SetMasterVolumePercent(int percent)
    {
        var clamped = Math.Clamp(percent, 0, 100) / 100f;
        try
        {
            var endpoint = GetAudioEndpointVolume();
            if (endpoint is not null)
            {
                var ctx = Guid.Empty;
                endpoint.SetMasterVolumeLevelScalar(clamped, ref ctx);
                if (percent > 0)
                {
                    endpoint.SetMute(false, ref ctx);
                }
                return;
            }
        }
        catch
        {
            // Bỏ qua nếu thiết bị âm thanh bận
        }
    }

    public bool IsMuted()
    {
        try
        {
            var endpoint = GetAudioEndpointVolume();
            if (endpoint is not null)
            {
                endpoint.GetMute(out var muted);
                return muted;
            }
        }
        catch
        {
        }

        return false;
    }

    public void ToggleMute()
    {
        try
        {
            var endpoint = GetAudioEndpointVolume();
            if (endpoint is not null)
            {
                endpoint.GetMute(out var muted);
                var ctx = Guid.Empty;
                endpoint.SetMute(!muted, ref ctx);
                return;
            }
        }
        catch
        {
        }

        SendAppCommand(APPCOMMAND_VOLUME_MUTE);
    }

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

    private static IAudioEndpointVolume? GetAudioEndpointVolume()
    {
        var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
        if (enumeratorType is null)
        {
            return null;
        }

        if (Activator.CreateInstance(enumeratorType) is not IMMDeviceEnumerator enumerator)
        {
            return null;
        }

        // eRender = 0, eMultimedia = 1
        if (enumerator.GetDefaultAudioEndpoint(0, 1, out var device) != 0 || device is null)
        {
            return null;
        }

        var iidAudioEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        if (device.Activate(ref iidAudioEndpointVolume, 23, IntPtr.Zero, out var obj) != 0)
        {
            return null;
        }

        return obj as IAudioEndpointVolume;
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? ppDevice);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? ppInterface);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out int pnChannelCount);

        [PreserveSig]
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);

        [PreserveSig]
        int GetMasterVolumeLevel(out float pfLevelDB);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float pfLevel);

        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }
}
