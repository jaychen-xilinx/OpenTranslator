using System.Text.Json;
using OpenTranslator.Helpers;
using OpenTranslator.Models;

namespace OpenTranslator.Services;

/// <summary>
/// 应用配置管理服务
/// </summary>
public class AppConfigService
{
    private readonly string _configPath;
    private AppConfig _config;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppConfigService(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.ConfigFileName);
        _config = LoadConfig();
    }

    public AppConfig GetConfig() => _config;

    public void SaveConfig(AppConfig config)
    {
        _config = config;
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    public void UpdateConfig(Action<AppConfig> update)
    {
        update(_config);
        SaveConfig(_config);
    }

    private AppConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { /* 配置损坏，使用默认值 */ }

        return new AppConfig();
    }
}