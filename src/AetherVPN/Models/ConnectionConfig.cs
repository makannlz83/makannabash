using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AetherVPN.Models;

public class ConnectionConfig
{
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "masque";

    [JsonPropertyName("scanMode")]
    public string ScanMode { get; set; } = "balanced";

    [JsonPropertyName("ipVersion")]
    public string IpVersion { get; set; } = "4";

    [JsonPropertyName("socksPort")]
    public int SocksPort { get; set; } = 1819;

    [JsonPropertyName("socksAddress")]
    public string SocksAddress { get; set; } = "127.0.0.1";

    [JsonPropertyName("noizeProfile")]
    public string NoizeProfile { get; set; } = "off";

    [JsonPropertyName("useHttp2")]
    public bool UseHttp2 { get; set; } = false;

    [JsonPropertyName("useFragment")]
    public bool UseFragment { get; set; } = false;

    [JsonPropertyName("enableSystemProxy")]
    public bool EnableSystemProxy { get; set; } = true;

    [JsonPropertyName("autoReconnect")]
    public bool AutoReconnect { get; set; } = true;

    [JsonPropertyName("logLevel")]
    public string LogLevel { get; set; } = "info";

    [JsonPropertyName("upstreamProxy")]
    public string UpstreamProxy { get; set; } = "";

    public string SocksEndpoint => $"{SocksAddress}:{SocksPort}";

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AetherVPN");

    private static string ConfigFile => Path.Combine(ConfigDir, "settings.json");

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFile, json);
    }

    public static ConnectionConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<ConnectionConfig>(json) ?? new ConnectionConfig();
            }
        }
        catch { }
        return new ConnectionConfig();
    }
}
