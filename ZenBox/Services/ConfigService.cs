using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using ZenBox.Models;

namespace ZenBox.Services;

public sealed class ConfigService
{
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "ZenBox";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configFilePath;
    private readonly ErrorHandlingService _errorService;

    public AppConfig Current { get; private set; }

    public ConfigService(ErrorHandlingService errorService, string? baseDirectory = null)
    {
        _errorService = errorService;
        var rootDir = string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory;
        _configFilePath = Path.Combine(rootDir, "config.json");
        Current = Load();
    }

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.StartWithWindows = IsStartupEnabledInRegistry();
                    Current = loaded;
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, "Load config.json", ex);
        }

        var fallback = new AppConfig
        {
            StartWithWindows = IsStartupEnabledInRegistry()
        };
        Current = fallback;
        Save(fallback);
        return fallback;
    }

    public void Save(AppConfig? config = null)
    {
        if (config is not null)
        {
            Current = config;
        }

        try
        {
            var dir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, "Save config.json", ex);
        }
    }

    public bool IsStartupEnabledInRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
            var val = key?.GetValue(AppRegistryName) as string;
            return !string.IsNullOrWhiteSpace(val);
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, "Check Startup Registry", ex);
            return false;
        }
    }

    public void SetStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunRegistryPath, writable: true);

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    key.SetValue(AppRegistryName, $"\"{exePath}\"");
                }
            }
            else
            {
                if (key.GetValue(AppRegistryName) is not null)
                {
                    key.DeleteValue(AppRegistryName, throwOnMissingValue: false);
                }
            }

            Current.StartWithWindows = enable;
            Save();
        }
        catch (Exception ex)
        {
            _errorService.LogError(ErrorCategory.UnexpectedSystemError, "Update Startup Registry", ex);
        }
    }
}
