using System.Net.NetworkInformation;

namespace ZenBox.Services;

public sealed class NetworkMonitorService : IDisposable
{
    public bool IsOnline { get; private set; }

    public event EventHandler<bool>? NetworkStatusChanged;

    public NetworkMonitorService()
    {
        IsOnline = CheckNetworkAvailable();
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    public static bool CheckNetworkAvailable()
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return false;
            }

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        RefreshStatus();
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        RefreshStatus();
    }

    public void RefreshStatus()
    {
        var current = CheckNetworkAvailable();
        if (current != IsOnline)
        {
            IsOnline = current;
            NetworkStatusChanged?.Invoke(this, current);
        }
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
    }
}
