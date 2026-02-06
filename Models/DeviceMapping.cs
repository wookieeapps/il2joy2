namespace Il2Joy2.Models;

/// <summary>
/// Represents a device mapping stored in the app's configuration
/// </summary>
public class DeviceMapping
{
    /// <summary>
    /// Unique identifier for matching (serial number or VID/PID combo)
    /// </summary>
    public required string UniqueIdentifier { get; set; }
    
    /// <summary>
    /// Device name for display purposes
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Expected IL2 device index
    /// </summary>
    public int ExpectedIndex { get; set; }
    
    /// <summary>
    /// Device GUID from IL2 config
    /// </summary>
    public required string Guid { get; set; }
}
