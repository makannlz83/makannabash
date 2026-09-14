using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AetherVPN.Models;

namespace AetherVPN.Services;

public class AetherCoreService : IDisposable
{
    private Process? _process;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public event Action<string>? OnLogReceived;
    public event Action<ConnectionState, string>? OnStatusChanged;
    public event Action<string, string, string>? OnIdentityReady; // deviceId, ipv4, ipv6
    public event Action<string>? OnEdgeConnected; // endpoint
    public event Action? OnProxyReady;
    public event Action<int>? OnProcessExited; // exit code

    private static readonly Regex IdentityRegex = new(
        @"identity ready: device=(?<device>\S+) ipv4=(?<ipv4>\S+) ipv6=(?<ipv6>\S+)",
        RegexOptions.Compiled);

    private static readonly Regex EdgeRegex = new(
        @"using cloudflare edge (?<endpoint>\S+)",
        RegexOptions.Compiled);

    private static readonly Regex ProxyRegex = new(
        @"(?:socks5|proxy).*(?:listening|bound|started)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string FindAetherExe()
    {
        // Look for aether.exe in several locations
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aether.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "core", "aether.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "aether.exe"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        // Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir, "aether.exe");
            if (File.Exists(fullPath)) return fullPath;
        }

        throw new FileNotFoundException(
            "aether.exe not found. Place it alongside AetherVPN.exe or in PATH.");
    }

    public async Task StartAsync(ConnectionConfig config)
    {
        if (_process is { HasExited: false })
        {
            throw new InvalidOperationException("Aether core is already running.");
        }

        var exePath = FindAetherExe();
        _cts = new CancellationTokenSource();

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AetherVPN");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "aether.toml");

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? configDir,
        };

        // Set environment variables based on config
        var env = psi.Environment;
        env["AETHER_PROTOCOL"] = config.Protocol;
        env["AETHER_SCAN"] = config.ScanMode;
        env["AETHER_IPV"] = config.IpVersion;
        env["AETHER_SOCKS"] = config.SocksEndpoint;
        env["AETHER_CONFIG"] = configPath;
        env["AETHER_LOG_LEVEL"] = config.LogLevel;

        if (!string.IsNullOrWhiteSpace(config.NoizeProfile) && config.NoizeProfile != "off")
        {
            env["AETHER_NOIZE"] = config.NoizeProfile;
        }

        if (config.UseHttp2)
        {
            env["AETHER_MASQUE_HTTP2"] = "1";
        }

        if (config.UseFragment)
        {
            env["AETHER_MASQUE_FRAGMENT"] = "1";
        }

        if (!string.IsNullOrWhiteSpace(config.UpstreamProxy))
        {
            env["AETHER_UPSTREAM"] = config.UpstreamProxy;
        }

        // Disable interactive prompts
        env["AETHER_ACCEPT_TOS"] = "1";

        OnStatusChanged?.Invoke(ConnectionState.Connecting, "Starting Aether core...");
        OnLogReceived?.Invoke($"[AetherVPN] Starting: {exePath}");
        OnLogReceived?.Invoke($"[AetherVPN] Protocol: {config.Protocol}, Scan: {config.ScanMode}");

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _process.Exited += (_, _) =>
        {
            var exitCode = -1;
            try { exitCode = _process.ExitCode; } catch { }
            OnLogReceived?.Invoke($"[AetherVPN] Process exited with code {exitCode}");
            OnProcessExited?.Invoke(exitCode);
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException("Failed to start aether.exe");
        }

        OnLogReceived?.Invoke($"[AetherVPN] Process started (PID: {_process.Id})");

        // Read stdout and stderr concurrently
        _ = ReadOutputAsync(_process.StandardOutput, _cts.Token);
        _ = ReadOutputAsync(_process.StandardError, _cts.Token);
    }

    private async Task ReadOutputAsync(StreamReader reader, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                OnLogReceived?.Invoke(line);
                ParseLogLine(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            OnLogReceived?.Invoke($"[AetherVPN] Read error: {ex.Message}");
        }
    }

    private void ParseLogLine(string line)
    {
        // Identity ready detection
        var identityMatch = IdentityRegex.Match(line);
        if (identityMatch.Success)
        {
            OnIdentityReady?.Invoke(
                identityMatch.Groups["device"].Value,
                identityMatch.Groups["ipv4"].Value,
                identityMatch.Groups["ipv6"].Value);
            OnStatusChanged?.Invoke(ConnectionState.Connecting, "Identity provisioned, scanning...");
            return;
        }

        // Edge connected detection
        var edgeMatch = EdgeRegex.Match(line);
        if (edgeMatch.Success)
        {
            OnEdgeConnected?.Invoke(edgeMatch.Groups["endpoint"].Value);
            OnStatusChanged?.Invoke(ConnectionState.Connected, "Connected to Cloudflare edge");
            return;
        }

        // Proxy ready detection
        if (ProxyRegex.IsMatch(line))
        {
            OnProxyReady?.Invoke();
            return;
        }

        // Scanning status
        if (line.Contains("scanning", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("probing", StringComparison.OrdinalIgnoreCase))
        {
            OnStatusChanged?.Invoke(ConnectionState.Connecting, "Scanning for endpoints...");
            return;
        }

        // Reconnection detection
        if (line.Contains("reconnect", StringComparison.OrdinalIgnoreCase))
        {
            OnStatusChanged?.Invoke(ConnectionState.Reconnecting, "Reconnecting...");
            return;
        }

        // Connected via tunnel detection (for MASQUE mode)
        if (line.Contains("tunnel established", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("masque session ready", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("connected to", StringComparison.OrdinalIgnoreCase))
        {
            OnStatusChanged?.Invoke(ConnectionState.Connected, "Tunnel active");
            return;
        }

        // Error detection
        if (line.Contains("fatal", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("error", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("[-]")
           )
        {
            OnStatusChanged?.Invoke(ConnectionState.Error, line);
        }
    }

    public async Task StopAsync()
    {
        if (_process == null || _process.HasExited) return;

        OnStatusChanged?.Invoke(ConnectionState.Disconnecting, "Stopping Aether core...");
        OnLogReceived?.Invoke("[AetherVPN] Stopping core process...");

        _cts?.Cancel();

        try
        {
            // Try graceful shutdown: send Ctrl+C
            if (!_process.HasExited)
            {
                // On Windows, we use taskkill to send Ctrl+C equivalent
                try
                {
                    using var killer = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {_process.Id} /T",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    });
                    killer?.WaitForExit(3000);
                }
                catch { }
            }

            // Wait up to 5 seconds for graceful shutdown
            if (!_process.HasExited)
            {
                var exited = _process.WaitForExit(5000);
                if (!exited)
                {
                    OnLogReceived?.Invoke("[AetherVPN] Force-killing process...");
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(3000);
                }
            }
        }
        catch (Exception ex)
        {
            OnLogReceived?.Invoke($"[AetherVPN] Error stopping: {ex.Message}");
        }

        OnLogReceived?.Invoke("[AetherVPN] Core stopped.");
        OnStatusChanged?.Invoke(ConnectionState.Disconnected, "Disconnected");
    }

    public bool IsRunning => _process is { HasExited: false };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();

        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch { }
        }
        _process?.Dispose();
    }
}
