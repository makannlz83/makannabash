using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AetherVPN.Models;
using AetherVPN.Services;

namespace AetherVPN.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly AetherCoreService _coreService;
    private readonly ProxyConfigService _proxyService;
    private readonly LogService _logService;
    private readonly DispatcherTimer _durationTimer;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public MainViewModel()
    {
        _coreService = new AetherCoreService();
        _proxyService = new ProxyConfigService();
        _logService = new LogService();
        _dispatcher = Dispatcher.CurrentDispatcher;

        // Load saved settings
        Config = ConnectionConfig.Load();
        _selectedProtocol = Config.Protocol;
        _selectedScanMode = Config.ScanMode;
        _selectedIpVersion = Config.IpVersion;
        _selectedNoizeProfile = Config.NoizeProfile;
        _socksPort = Config.SocksPort;
        _useHttp2 = Config.UseHttp2;
        _useFragment = Config.UseFragment;
        _enableSystemProxy = Config.EnableSystemProxy;
        _autoReconnect = Config.AutoReconnect;
        _upstreamProxy = Config.UpstreamProxy;

        // Wire up events
        _coreService.OnLogReceived += line => _dispatcher.BeginInvoke(() =>
        {
            _logService.AddLine(line);
            LogText = _logService.LogText;
        });

        _coreService.OnStatusChanged += (state, text) => _dispatcher.BeginInvoke(() =>
        {
            Status = state;
            StatusText = text;
            if (state == ConnectionState.Connected)
            {
                ConnectedAt = DateTime.Now;
                _ = FetchExitIpAsync();
            }
        });

        _coreService.OnIdentityReady += (deviceId, ipv4, ipv6) => _dispatcher.BeginInvoke(() =>
        {
            DeviceId = deviceId;
            AssignedIpv4 = ipv4;
            AssignedIpv6 = ipv6;
        });

        _coreService.OnEdgeConnected += endpoint => _dispatcher.BeginInvoke(() =>
        {
            EdgeEndpoint = endpoint;
        });

        _coreService.OnProcessExited += exitCode => _dispatcher.BeginInvoke(() =>
        {
            if (Status != ConnectionState.Disconnecting && Status != ConnectionState.Disconnected)
            {
                if (AutoReconnect && !_disposed)
                {
                    Status = ConnectionState.Reconnecting;
                    StatusText = "Reconnecting...";
                    _ = ReconnectAsync();
                }
                else
                {
                    Status = ConnectionState.Disconnected;
                    StatusText = $"Disconnected (exit code: {exitCode})";
                    CleanupConnection();
                }
            }
        });

        // Duration timer
        _durationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _durationTimer.Tick += (_, _) =>
        {
            if (ConnectedAt.HasValue)
            {
                var duration = DateTime.Now - ConnectedAt.Value;
                ConnectionDuration = duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                    : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
        };
    }

    // --- Configuration ---
    public ConnectionConfig Config { get; }

    [ObservableProperty] private string _selectedProtocol = "masque";
    [ObservableProperty] private string _selectedScanMode = "balanced";
    [ObservableProperty] private string _selectedIpVersion = "4";
    [ObservableProperty] private string _selectedNoizeProfile = "off";
    [ObservableProperty] private int _socksPort = 1819;
    [ObservableProperty] private bool _useHttp2;
    [ObservableProperty] private bool _useFragment;
    [ObservableProperty] private bool _enableSystemProxy = true;
    [ObservableProperty] private bool _autoReconnect = true;
    [ObservableProperty] private string _upstreamProxy = "";

    // --- Status ---
    [ObservableProperty] private ConnectionState _status = ConnectionState.Disconnected;
    [ObservableProperty] private string _statusText = "Disconnected";
    [ObservableProperty] private string _connectionDuration = "--:--";
    [ObservableProperty] private string? _exitIp;
    [ObservableProperty] private string? _deviceId;
    [ObservableProperty] private string? _assignedIpv4;
    [ObservableProperty] private string? _assignedIpv6;
    [ObservableProperty] private string? _edgeEndpoint;
    [ObservableProperty] private DateTime? _connectedAt;
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private bool _showAdvancedSettings;
    [ObservableProperty] private bool _isConnecting;

    // --- Protocol Options ---
    public string[] Protocols => ["masque", "wg", "gool", "mim"];
    public string[] ProtocolLabels => ["MASQUE (Recommended)", "WireGuard", "Gool (Double WG)", "MasqueInMasque"];
    public string[] ScanModes => ["balanced", "quick", "turbo", "full"];
    public string[] ScanModeLabels => ["Balanced", "Quick", "Turbo", "Full Scan"];
    public string[] IpVersions => ["4", "6", "46"];
    public string[] IpVersionLabels => ["IPv4", "IPv6", "Both"];
    public string[] NoizeProfiles => ["off", "firewall", "balanced", "heavy"];
    public string[] NoizeProfileLabels => ["Off", "Firewall", "Balanced", "Heavy"];

    public bool CanConnect => Status == ConnectionState.Disconnected || Status == ConnectionState.Error;
    public bool CanDisconnect => Status == ConnectionState.Connected ||
                                  Status == ConnectionState.Connecting ||
                                  Status == ConnectionState.Reconnecting;

    // --- Commands ---
    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (!CanConnect) return;
        IsConnecting = true;

        try
        {
            SaveCurrentSettings();
            _logService.Clear();
            LogText = "";

            await _coreService.StartAsync(Config);

            if (EnableSystemProxy)
            {
                _proxyService.EnableProxy(Config.SocksEndpoint);
                _logService.AddLine("[AetherVPN] System proxy enabled");
                LogText = _logService.LogText;
            }

            _durationTimer.Start();
        }
        catch (Exception ex)
        {
            Status = ConnectionState.Error;
            StatusText = ex.Message;
            _logService.AddLine($"[AetherVPN] ERROR: {ex.Message}");
            LogText = _logService.LogText;
        }
        finally
        {
            IsConnecting = false;
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanDisconnect));
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        if (!CanDisconnect) return;

        Status = ConnectionState.Disconnecting;
        StatusText = "Disconnecting...";

        await _coreService.StopAsync();
        CleanupConnection();

        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanDisconnect));
    }

    [RelayCommand]
    private void ToggleAdvancedSettings()
    {
        ShowAdvancedSettings = !ShowAdvancedSettings;
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _logService.Clear();
        LogText = "";
    }

    [RelayCommand]
    private void SaveLogs()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text Files|*.txt|All Files|*.*",
            FileName = $"aethervpn-log-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            _logService.SaveToFile(dialog.FileName);
        }
    }

    // --- Private Methods ---
    private void SaveCurrentSettings()
    {
        Config.Protocol = SelectedProtocol;
        Config.ScanMode = SelectedScanMode;
        Config.IpVersion = SelectedIpVersion;
        Config.SocksPort = SocksPort;
        Config.NoizeProfile = SelectedNoizeProfile;
        Config.UseHttp2 = UseHttp2;
        Config.UseFragment = UseFragment;
        Config.EnableSystemProxy = EnableSystemProxy;
        Config.AutoReconnect = AutoReconnect;
        Config.UpstreamProxy = UpstreamProxy;
        Config.Save();
    }

    private void CleanupConnection()
    {
        _durationTimer.Stop();
        _proxyService.DisableProxy();
        ConnectedAt = null;
        ConnectionDuration = "--:--";
        ExitIp = null;
        DeviceId = null;
        AssignedIpv4 = null;
        AssignedIpv6 = null;
        EdgeEndpoint = null;

        Status = ConnectionState.Disconnected;
        StatusText = "Disconnected";
    }

    private async Task ReconnectAsync()
    {
        _logService.AddLine("[AetherVPN] Auto-reconnecting in 3 seconds...");
        LogText = _logService.LogText;

        await Task.Delay(3000);

        if (_disposed || Status == ConnectionState.Disconnected) return;

        try
        {
            await _coreService.StartAsync(Config);
        }
        catch (Exception ex)
        {
            Status = ConnectionState.Error;
            StatusText = $"Reconnect failed: {ex.Message}";
            _logService.AddLine($"[AetherVPN] Reconnect failed: {ex.Message}");
            LogText = _logService.LogText;
        }
    }

    private async Task FetchExitIpAsync()
    {
        try
        {
            var proxy = new WebProxy($"socks5://127.0.0.1:{SocksPort}");
            var handler = new HttpClientHandler { Proxy = proxy, UseProxy = true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

            var response = await client.GetStringAsync("https://www.cloudflare.com/cdn-cgi/trace");
            foreach (var line in response.Split('\n'))
            {
                if (line.StartsWith("ip="))
                {
                    ExitIp = line[3..].Trim();
                    break;
                }
            }
        }
        catch
        {
            ExitIp = "Unable to detect";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _durationTimer.Stop();
        _proxyService.DisableProxy();
        _coreService.Dispose();
    }
}
