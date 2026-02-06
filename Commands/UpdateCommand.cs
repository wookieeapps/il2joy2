using Il2Joy2.Models;
using Il2Joy2.Services;

namespace Il2Joy2.Commands;

/// <summary>
/// Handles the regular (default) command - checks and updates configurations
/// </summary>
public class UpdateCommand
{
    private readonly JoystickEnumerator _enumerator;
    private readonly Il2ConfigService _il2ConfigService;
    private readonly AppConfigService _appConfigService;
    
    public UpdateCommand(
        JoystickEnumerator enumerator,
        Il2ConfigService il2ConfigService,
        AppConfigService appConfigService)
    {
        _enumerator = enumerator;
        _il2ConfigService = il2ConfigService;
        _appConfigService = appConfigService;
    }
    
    public int Execute()
    {
        Console.WriteLine("=== IL2 Joystick Manager - Update Check ===\n");
        
        // Load configuration
        var config = _appConfigService.LoadConfig();
        if (config == null)
        {
            Console.WriteLine("ERROR: No configuration found.");
            Console.WriteLine("Run with 'init <devices.txt> <bindings.txt>' to create initial configuration.");
            return 1;
        }
        
        // Validate IL2 files exist
        if (!File.Exists(config.DevicesFilePath))
        {
            Console.WriteLine($"ERROR: IL2 devices file not found: {config.DevicesFilePath}");
            return 1;
        }
        
        if (!File.Exists(config.BindingsFilePath))
        {
            Console.WriteLine($"ERROR: IL2 bindings file not found: {config.BindingsFilePath}");
            return 1;
        }
        
        // Get current connected devices
        Console.WriteLine("Scanning connected devices...");
        var connectedDevices = _enumerator.EnumerateJoysticks();
        Console.WriteLine($"Found {connectedDevices.Count} connected device(s).\n");
        
        // Parse current IL2 devices file
        var il2Devices = _il2ConfigService.ParseDevicesFile(config.DevicesFilePath);
        
        // Check for missing/duplicate devices
        var errors = new List<string>();
        var warnings = new List<string>();
        var matchedDevices = new Dictionary<DeviceMapping, JoystickDevice>();
        
        Console.WriteLine("Checking device mappings...\n");
        
        foreach (var mapping in config.DeviceMappings)
        {
            var connectedDevice = connectedDevices.FirstOrDefault(d => 
                d.UniqueIdentifier == mapping.UniqueIdentifier);
            
            if (connectedDevice == null)
            {
                errors.Add($"Device not found: [{mapping.ExpectedIndex}] {mapping.Name} (ID: {mapping.UniqueIdentifier})");
            }
            else
            {
                matchedDevices[mapping] = connectedDevice;
                Console.WriteLine($"? Found: [{mapping.ExpectedIndex}] {mapping.Name}");
            }
        }
        
        // Check for duplicates
        var duplicates = matchedDevices.GroupBy(m => m.Value.UniqueIdentifier)
            .Where(g => g.Count() > 1)
            .ToList();
        
        foreach (var dup in duplicates)
        {
            errors.Add($"Duplicate device mapping: {dup.First().Value.Name} is mapped multiple times");
        }
        
        // Report errors and abort if any
        if (errors.Count > 0)
        {
            Console.WriteLine("\n=== ERRORS ===");
            foreach (var error in errors)
            {
                Console.WriteLine($"? {error}");
            }
            Console.WriteLine("\nAborting due to device errors.");
            Console.WriteLine("Please connect all configured devices and try again.");
            return 1;
        }
        
        // Determine current IL2 indices for matched devices
        Console.WriteLine("\nChecking IL2 device indices...\n");
        
        var indexMapping = new Dictionary<int, int>(); // old index -> new index
        var devicesNeedingUpdate = new List<(DeviceMapping Mapping, Il2Device? CurrentIl2Device, int? CurrentIndex)>();
        
        foreach (var (mapping, connectedDevice) in matchedDevices)
        {
            // Find this device in IL2's current device list
            var currentIl2Device = il2Devices.FirstOrDefault(d => 
                MatchesDevice(d, connectedDevice, mapping));
            
            if (currentIl2Device != null)
            {
                if (currentIl2Device.Id != mapping.ExpectedIndex)
                {
                    Console.WriteLine($"  Index change needed: {mapping.Name}");
                    Console.WriteLine($"    Current: joy{currentIl2Device.Id} -> Expected: joy{mapping.ExpectedIndex}");
                    
                    indexMapping[currentIl2Device.Id] = mapping.ExpectedIndex;
                    devicesNeedingUpdate.Add((mapping, currentIl2Device, currentIl2Device.Id));
                }
                else
                {
                    Console.WriteLine($"  ? {mapping.Name} - joy{currentIl2Device.Id} (OK)");
                }
            }
            else
            {
                warnings.Add($"Device {mapping.Name} not found in IL2 devices.txt - may need to re-init");
            }
        }
        
        // Report warnings
        if (warnings.Count > 0)
        {
            Console.WriteLine("\n=== WARNINGS ===");
            foreach (var warning in warnings)
            {
                Console.WriteLine($"? {warning}");
            }
        }
        
        // Apply updates if needed
        if (indexMapping.Count > 0)
        {
            Console.WriteLine($"\n{indexMapping.Count} index change(s) required.");
            
            // Update IL2 devices.txt
            Console.WriteLine("\nUpdating IL2 devices file...");
            UpdateIl2DevicesFile(config.DevicesFilePath, il2Devices, devicesNeedingUpdate);
            
            // Update bindings file
            Console.WriteLine("Updating IL2 bindings file...");
            _il2ConfigService.UpdateBindingsFile(config.BindingsFilePath, indexMapping);
            
            Console.WriteLine("\n? Configuration updated successfully.");
        }
        else
        {
            Console.WriteLine("\n? All devices are correctly configured. No changes needed.");
        }
        
        return 0;
    }
    
    private static bool MatchesDevice(Il2Device il2Device, JoystickDevice connectedDevice, DeviceMapping mapping)
    {
        // Match by GUID first
        if (string.Equals(il2Device.Guid, mapping.Guid, StringComparison.OrdinalIgnoreCase))
            return true;
        
        // Match by name
        var normalizedIl2Name = il2Device.Model.ToLowerInvariant();
        var normalizedMappingName = mapping.Name.ToLowerInvariant();
        
        return normalizedIl2Name.Contains(normalizedMappingName) ||
               normalizedMappingName.Contains(normalizedIl2Name);
    }
    
    private void UpdateIl2DevicesFile(
        string filePath, 
        List<Il2Device> currentDevices,
        List<(DeviceMapping Mapping, Il2Device? CurrentIl2Device, int? CurrentIndex)> updates)
    {
        // Create a new device list with updated indices
        var updatedDevices = new List<Il2Device>();
        var usedIndices = new HashSet<int>();
        
        // First, add devices that need new indices
        foreach (var (mapping, currentIl2Device, _) in updates)
        {
            if (currentIl2Device != null)
            {
                updatedDevices.Add(new Il2Device
                {
                    Id = mapping.ExpectedIndex,
                    Guid = currentIl2Device.Guid,
                    Model = currentIl2Device.Model
                });
                usedIndices.Add(mapping.ExpectedIndex);
            }
        }
        
        // Then add devices that don't need changes
        foreach (var device in currentDevices)
        {
            var needsUpdate = updates.Any(u => u.CurrentIl2Device?.Id == device.Id);
            if (!needsUpdate && !usedIndices.Contains(device.Id))
            {
                updatedDevices.Add(device);
                usedIndices.Add(device.Id);
            }
        }
        
        // WriteDevicesFile handles backup automatically
        _il2ConfigService.WriteDevicesFile(filePath, updatedDevices);
    }
}
