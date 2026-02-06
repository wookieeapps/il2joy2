namespace Il2Joy2.Models;

/// <summary>
/// Application configuration stored in JSON
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Path to IL2's devices.txt file
    /// </summary>
    public required string DevicesFilePath { get; set; }
    
    /// <summary>
    /// Path to IL2's key bindings file (e.g., current.txt)
    /// </summary>
    public required string BindingsFilePath { get; set; }
    
    /// <summary>
    /// List of configured device mappings
    /// </summary>
    public List<DeviceMapping> DeviceMappings { get; set; } = [];
}
