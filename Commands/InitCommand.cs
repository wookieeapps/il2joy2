using Il2Joy2.Models;
using Il2Joy2.Services;

namespace Il2Joy2.Commands;

/// <summary>
/// Handles the 'init' command - creates initial configuration from IL2 config files
/// </summary>
public class InitCommand
{
    private readonly JoystickEnumerator _enumerator;
    private readonly Il2ConfigService _il2ConfigService;
    private readonly AppConfigService _appConfigService;
    
    public InitCommand(
        JoystickEnumerator enumerator, 
        Il2ConfigService il2ConfigService,
        AppConfigService appConfigService)
    {
        _enumerator = enumerator;
        _il2ConfigService = il2ConfigService;
        _appConfigService = appConfigService;
    }
    
    public int Execute(string devicesFilePath, string bindingsFilePath)
    {
        Console.WriteLine("=== IL2 Joystick Manager - Initialize Configuration ===\n");
        
        // Validate paths
        if (!File.Exists(devicesFilePath))
        {
            Console.WriteLine($"ERROR: Devices file not found: {devicesFilePath}");
            return 1;
        }
        
        if (!File.Exists(bindingsFilePath))
        {
            Console.WriteLine($"ERROR: Bindings file not found: {bindingsFilePath}");
            return 1;
        }
        
        // Check for existing config
        if (_appConfigService.ConfigExists())
        {
            Console.Write("Configuration already exists. Overwrite? (y/N): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Initialization cancelled.");
                return 0;
            }
        }
        
        // Parse IL2 devices file
        Console.WriteLine($"Reading IL2 devices file: {devicesFilePath}");
        var il2Devices = _il2ConfigService.ParseDevicesFile(devicesFilePath);
        Console.WriteLine($"Found {il2Devices.Count} device(s) in IL2 config.\n");
        
        foreach (var device in il2Devices)
        {
            Console.WriteLine($"  [{device.Id}] {device.Model} (GUID: {device.Guid})");
        }
        
        // Get currently connected devices
        Console.WriteLine("\nScanning connected devices...");
        var connectedDevices = _enumerator.EnumerateJoysticks();
        Console.WriteLine($"Found {connectedDevices.Count} connected device(s).\n");
        
        // Match IL2 devices with connected devices
        var mappings = new List<DeviceMapping>();
        var unmatchedIl2Devices = new List<Il2Device>();
        
        foreach (var il2Device in il2Devices)
        {
            // Try to find matching connected device by GUID or name
            var matchedDevice = FindMatchingDevice(il2Device, connectedDevices);
            
            if (matchedDevice != null)
            {
                mappings.Add(new DeviceMapping
                {
                    UniqueIdentifier = matchedDevice.UniqueIdentifier,
                    Name = il2Device.Model,
                    ExpectedIndex = il2Device.Id,
                    Guid = il2Device.Guid
                });
                
                Console.WriteLine($"? Matched [{il2Device.Id}] {il2Device.Model}");
                Console.WriteLine($"  -> {matchedDevice.UniqueIdentifier}");
            }
            else
            {
                unmatchedIl2Devices.Add(il2Device);
                Console.WriteLine($"? Could not match [{il2Device.Id}] {il2Device.Model}");
            }
        }
        
        if (unmatchedIl2Devices.Count > 0)
        {
            Console.WriteLine($"\nWARNING: {unmatchedIl2Devices.Count} device(s) could not be matched.");
            Console.WriteLine("Make sure all your joysticks are connected before running init.");
            Console.Write("\nContinue with partial configuration? (y/N): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Initialization cancelled.");
                return 1;
            }
        }
        
        // Create and save configuration
        var config = new AppConfig
        {
            DevicesFilePath = Path.GetFullPath(devicesFilePath),
            BindingsFilePath = Path.GetFullPath(bindingsFilePath),
            DeviceMappings = mappings
        };
        
        _appConfigService.SaveConfig(config);
        
        Console.WriteLine($"\n? Configuration initialized with {mappings.Count} device mapping(s).");
        Console.WriteLine($"  Config file: {_appConfigService.GetConfigPath()}");
        
        return 0;
    }
    
    private static JoystickDevice? FindMatchingDevice(Il2Device il2Device, List<JoystickDevice> connectedDevices)
    {
        // First, try exact GUID match
        var byGuid = connectedDevices.FirstOrDefault(d => 
            string.Equals(d.Guid, il2Device.Guid, StringComparison.OrdinalIgnoreCase));
        if (byGuid != null)
            return byGuid;
        
        // Second, try matching by model name
        var normalizedModel = NormalizeName(il2Device.Model);
        var byName = connectedDevices.FirstOrDefault(d => 
            NormalizeName(d.Name).Contains(normalizedModel, StringComparison.OrdinalIgnoreCase) ||
            normalizedModel.Contains(NormalizeName(d.Name), StringComparison.OrdinalIgnoreCase));
        if (byName != null)
            return byName;
        
        // Third, try fuzzy matching on key parts of the name
        var modelParts = normalizedModel.Split(' ', '-', '_')
            .Where(p => p.Length > 2)
            .ToList();
        
        foreach (var device in connectedDevices)
        {
            var deviceNameNormalized = NormalizeName(device.Name);
            var matchingParts = modelParts.Count(part => 
                deviceNameNormalized.Contains(part, StringComparison.OrdinalIgnoreCase));
            
            // If at least half the significant parts match
            if (modelParts.Count > 0 && matchingParts >= Math.Ceiling(modelParts.Count / 2.0))
            {
                return device;
            }
        }
        
        return null;
    }
    
    private static string NormalizeName(string name)
    {
        return name.Replace("VKBsim", "VKB")
                   .Replace("VPC", "Virpil")
                   .Trim();
    }
}
