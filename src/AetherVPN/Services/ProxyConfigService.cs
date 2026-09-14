using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AetherVPN.Services;

public class ProxyConfigService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    private string? _previousProxyServer;
    private int _previousProxyEnable;
    private bool _hasSavedState;

    public void EnableProxy(string socksEndpoint)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            if (key == null) return;

            // Save current state
            if (!_hasSavedState)
            {
                _previousProxyServer = key.GetValue("ProxyServer") as string;
                _previousProxyEnable = (int)(key.GetValue("ProxyEnable") ?? 0);
                _hasSavedState = true;
            }

            // Set SOCKS proxy
            key.SetValue("ProxyServer", $"socks={socksEndpoint}");
            key.SetValue("ProxyEnable", 1);

            // Bypass for local addresses
            key.SetValue("ProxyOverride", "localhost;127.*;10.*;192.168.*;<local>");

            // Notify the system of the change
            RefreshProxySettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set proxy: {ex.Message}");
        }
    }

    public void DisableProxy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            if (key == null) return;

            if (_hasSavedState)
            {
                // Restore previous state
                if (_previousProxyServer != null)
                    key.SetValue("ProxyServer", _previousProxyServer);
                else
                    key.DeleteValue("ProxyServer", false);

                key.SetValue("ProxyEnable", _previousProxyEnable);
                _hasSavedState = false;
            }
            else
            {
                // Just disable
                key.SetValue("ProxyEnable", 0);
            }

            RefreshProxySettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to disable proxy: {ex.Message}");
        }
    }

    private static void RefreshProxySettings()
    {
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
    }
}
