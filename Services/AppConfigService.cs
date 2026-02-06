using System.Text.Json;
using Il2Joy2.Models;
using Spectre.Console;

namespace Il2Joy2.Services;

/// <summary>
/// Service for managing the application's configuration file
/// </summary>
public sealed class AppConfigService
{
    private readonly string _configPath;
    
    public AppConfigService(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            Constants.AppConfigFileName);
    }
    
    /// <summary>
    /// Loads the application configuration
    /// </summary>
    public AppConfig? LoadConfig()
    {
        if (!File.Exists(_configPath))
        {
            return null;
        }
        
        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error loading config:[/] [dim]{ex.Message}[/]");
            return null;
        }
    }
    
    /// <summary>
    /// Saves the application configuration
    /// </summary>
    public void SaveConfig(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, AppJsonContext.Default.AppConfig);
        File.WriteAllText(_configPath, json);
        AnsiConsole.MarkupLine($"[green]?[/] Configuration saved to: [yellow]{_configPath}[/]");
    }
    
    /// <summary>
    /// Checks if configuration exists
    /// </summary>
    public bool ConfigExists() => File.Exists(_configPath);
    
    /// <summary>
    /// Gets the path to the configuration file
    /// </summary>
    public string GetConfigPath() => _configPath;
}
