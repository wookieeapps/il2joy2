using Il2Joy2.Models;
using Il2Joy2.Services;

namespace Il2Joy2.Commands;

/// <summary>
/// Handles the 'view' command - displays current device information
/// </summary>
public class ViewCommand
{
    private readonly JoystickEnumerator _enumerator;
    private readonly AppConfigService _configService;
    
    public ViewCommand(JoystickEnumerator enumerator, AppConfigService configService)
    {
        _enumerator = enumerator;
        _configService = configService;
    }
    
    public int Execute()
    {
        Console.WriteLine("=== IL2 Joystick Manager - Device View ===\n");
        
        // Get current devices
        Console.WriteLine("Scanning for connected joystick devices...\n");
        var devices = _enumerator.EnumerateJoysticks();
        
        if (devices.Count == 0)
        {
            Console.WriteLine("No joystick devices found.");
            Console.WriteLine("\nNote: Make sure your devices are connected and recognized by Windows.");
            return 0;
        }
        
        Console.WriteLine($"Found {devices.Count} device(s):\n");
        Console.WriteLine(new string('-', 80));
        
        foreach (var device in devices)
        {
            Console.WriteLine($"Device: {device.Name}");
            Console.WriteLine($"  Instance ID: {device.DeviceInstanceId}");
            Console.WriteLine($"  GUID:        {device.Guid}");
            Console.WriteLine($"  Vendor ID:   {device.VendorId ?? "N/A"}");
            Console.WriteLine($"  Product ID:  {device.ProductId ?? "N/A"}");
            Console.WriteLine($"  Unique ID:   {device.UniqueIdentifier}");
            Console.WriteLine(new string('-', 80));
        }
        
        // Show stored configuration if available
        var config = _configService.LoadConfig();
        if (config != null)
        {
            Console.WriteLine("\n=== Stored Configuration ===\n");
            Console.WriteLine($"Devices file: {config.DevicesFilePath}");
            Console.WriteLine($"Bindings file: {config.BindingsFilePath}");
            Console.WriteLine($"\nConfigured mappings ({config.DeviceMappings.Count}):");
            
            foreach (var mapping in config.DeviceMappings.OrderBy(m => m.ExpectedIndex))
            {
                var currentDevice = devices.FirstOrDefault(d => d.UniqueIdentifier == mapping.UniqueIdentifier);
                var status = currentDevice != null ? "? Connected" : "? NOT FOUND";
                
                Console.WriteLine($"  [{mapping.ExpectedIndex}] {mapping.Name}");
                Console.WriteLine($"      Unique ID: {mapping.UniqueIdentifier}");
                Console.WriteLine($"      Status: {status}");
            }
        }
        else
        {
            Console.WriteLine("\nNo configuration found. Run with 'init' to create initial configuration.");
        }
        
        return 0;
    }
}
