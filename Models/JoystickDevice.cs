namespace Il2Joy2.Models;

/// <summary>
/// Represents a joystick device with its identification properties
/// </summary>
public class JoystickDevice
{
    /// <summary>
    /// Windows device instance ID (unique per device connection)
    /// </summary>
    public required string DeviceInstanceId { get; set; }
    
    /// <summary>
    /// Device GUID (matches IL2's guid format)
    /// </summary>
    public required string Guid { get; set; }
    
    /// <summary>
    /// Human-readable device name/model
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// USB Vendor ID
    /// </summary>
    public string? VendorId { get; set; }
    
    /// <summary>
    /// USB Product ID
    /// </summary>
    public string? ProductId { get; set; }
    
    /// <summary>
    /// Current IL2 device index (joy0, joy1, etc.)
    /// </summary>
    public int Il2DeviceIndex { get; set; } = -1;
    
    /// <summary>
    /// Gets a unique identifier for device matching using VID/PID + Name.
    /// </summary>
    public string UniqueIdentifier => 
        $"VIDPID:{VendorId ?? "0000"}:{ProductId ?? "0000"}:{Name}";
    
    public override string ToString() => 
        $"[{Il2DeviceIndex}] {Name} (GUID: {Guid}, VID: {VendorId}, PID: {ProductId})";
}
