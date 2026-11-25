using System.Diagnostics;

namespace ZenBox.Services;

public static class FirewallHelperService
{
    public const string RuleName = "ZenBox Remote Control";

    /// <summary>
    /// Kiểm tra xem Tường lửa Windows đã có quy tắc mở cổng điều khiển cho ZenBox chưa.
    /// </summary>
    public static bool IsPortAllowedInFirewall(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall show rule name=\"{RuleName}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return false;
            }

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);
            return proc.ExitCode == 0 && output.Contains(port.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Mở hộp thoại xác nhận chuẩn của Windows (UAC) để cho phép các điện thoại trong mạng Wi-Fi
    /// (kể cả mạng Wi-Fi đặt ở chế độ Public hoặc Private) kết nối vào cổng điều khiển ZenBox.
    /// </summary>
    public static bool RequestOpenFirewallPort(int port)
    {
        try
        {
            var args =
                $"/c netsh advfirewall firewall delete rule name=\"{RuleName}\" >nul 2>&1 & " +
                $"netsh advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol=TCP localport={port} profile=any";

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = args,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(8000);
            return IsPortAllowedInFirewall(port);
        }
        catch
        {
            // Người dùng bấm Hủy trên hộp thoại UAC
            return false;
        }
    }
}
