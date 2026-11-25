using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ZenBox.Services;

public static class NetworkAddressHelper
{
    private static readonly string[] IgnoredAdapterKeywords =
    [
        "radmin",
        "hamachi",
        "tailscale",
        "zerotier",
        "vmware",
        "virtualbox",
        "vbox",
        "wsl",
        "hyper-v",
        "vethernet",
        "loopback",
        "tunnel",
        "teredo",
        "tap-",
        "tun",
        "wireguard",
        " NordLynx",
        "openvpn"
    ];

    /// <summary>
    /// Lấy danh sách tất cả địa chỉ IPv4 mạng nội bộ thật (Wi-Fi / Ethernet),
    /// loại bỏ hoàn toàn các card mạng ảo VPN như Radmin VPN (26.x.x.x), WSL, VMware...
    /// </summary>
    public static IReadOnlyList<string> GetRealLanIpAddresses()
    {
        var candidates = new List<(string Ip, int Priority)>();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var combinedName = $"{nic.Name} {nic.Description}".ToLowerInvariant();
                if (IgnoredAdapterKeywords.Any(k => combinedName.Contains(k.Trim().ToLowerInvariant())))
                {
                    continue;
                }

                var ipProps = nic.GetIPProperties();
                var hasIpv4Gateway = ipProps.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.Any.Equals(g.Address));

                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork ||
                        IPAddress.IsLoopback(unicast.Address))
                    {
                        continue;
                    }

                    var bytes = unicast.Address.GetAddressBytes();
                    // Bỏ qua APIPA 169.254.x.x và dải Radmin VPN 26.x.x.x
                    if ((bytes[0] == 169 && bytes[1] == 254) || bytes[0] == 26)
                    {
                        continue;
                    }

                    bool isPrivateLan =
                        bytes[0] == 192 && bytes[1] == 168 ||
                        bytes[0] == 10 ||
                        (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31);

                    int score = 0;
                    if (isPrivateLan) score += 100;
                    if (hasIpv4Gateway) score += 50;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 30;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 20;

                    candidates.Add((unicast.Address.ToString(), score));
                }
            }
        }
        catch
        {
            // Fallback below
        }

        if (candidates.Count > 0)
        {
            return candidates
                .OrderByDescending(c => c.Priority)
                .Select(c => c.Ip)
                .Distinct()
                .ToList();
        }

        // Dự phòng qua bảng định tuyến UDP ra Internet (không phát gói tin thật)
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                return [endPoint.Address.ToString()];
            }
        }
        catch
        {
        }

        return ["127.0.0.1"];
    }

    public static string GetPrimaryLanIp()
    {
        return GetRealLanIpAddresses().FirstOrDefault() ?? "127.0.0.1";
    }
}
