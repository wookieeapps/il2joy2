using Il2Joy2.Models;
using Il2Joy2.Services;
using Spectre.Console;

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
        AnsiConsole.Write(new Rule("[cyan1]Update Check[/]").LeftJustified());
        AnsiConsole.WriteLine();
        
        // Load configuration
        var config = _appConfigService.LoadConfig();
        if (config == null)
        {
            AnsiConsole.MarkupLine("[red]ERROR:[/] No configuration found.");
            AnsiConsole.MarkupLine("Run [cyan]il2joy2 init <config_folder>[/] to create initial configuration.");
            return 1;
        }
        
        // Validate IL2 files exist
        if (!File.Exists(config.DevicesFilePath))
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] IL2 devices file not found: [yellow]{config.DevicesFilePath}[/]");
            return 1;
        }
        
        if (!File.Exists(config.BindingsFilePath))
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] IL2 bindings file not found: [yellow]{config.BindingsFilePath}[/]");
            return 1;
        }
        
        // Get current connected devices
        List<JoystickDevice> connectedDevices = [];
        AnsiConsole.Status()
            .Start("[yellow]Scanning connected devices...[/]", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                connectedDevices = _enumerator.EnumerateJoysticks();
            });
        
        AnsiConsole.MarkupLine($"[green]Found {connectedDevices.Count} connected device(s).[/]\n");
        
        // Parse current IL2 devices file
        var il2Devices = _il2ConfigService.ParseDevicesFile(config.DevicesFilePath);
        
        // Check for missing/duplicate devices
        var errors = new List<string>();
        var warnings = new List<string>();
        var matchedDevices = new Dictionary<DeviceMapping, JoystickDevice>();
        
        AnsiConsole.MarkupLine("[yellow]Checking device mappings...[/]\n");
        
        foreach (var mapping in config.DeviceMappings)
        {
            var connectedDevice = connectedDevices.FirstOrDefault(d => 
                d.UniqueIdentifier == mapping.UniqueIdentifier);
            
            
            if (connectedDevice == null)
            {
                errors.Add($"Device not found: [joy{mapping.ExpectedIndex}] {Markup.Escape(mapping.Name)}");
            }
            else
            {
                matchedDevices[mapping] = connectedDevice;
                AnsiConsole.MarkupLine($"  [green]?[/] Found: [cyan]joy{mapping.ExpectedIndex}[/] {Markup.Escape(mapping.Name)}");
            }
        }
        
        // Check for duplicates
        var duplicates = matchedDevices.GroupBy(m => m.Value.UniqueIdentifier)
            .Where(g => g.Count() > 1)
            .ToList();
        
        foreach (var dup in duplicates)
        {
            errors.Add($"Duplicate device mapping: {dup.First().Value.Name}");
        }
        
        // Report errors and abort if any
        if (errors.Count > 0)
        {
            AnsiConsole.WriteLine();
            var errorPanel = new Panel(string.Join("\n", errors.Select(e => $"[red]?[/] {e}")))
            {
                Header = new PanelHeader(" [red]ERRORS[/] ", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red)
            };
            AnsiConsole.Write(errorPanel);
            
            AnsiConsole.MarkupLine("\n[red]Aborting due to device errors.[/]");
            AnsiConsole.MarkupLine("[yellow]Please connect all configured devices and try again.[/]");
            return 1;
        }
        
        // Determine current IL2 indices for matched devices
        AnsiConsole.MarkupLine("\n[yellow]Checking IL2 device indices...[/]\n");
        
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
                    AnsiConsole.MarkupLine($"  [yellow]?[/] Index change needed: [cyan]{Markup.Escape(mapping.Name)}[/]");
                    AnsiConsole.MarkupLine($"    [dim]Current:[/] [red]joy{currentIl2Device.Id}[/] [dim]?[/] [green]joy{mapping.ExpectedIndex}[/]");
                    
                    indexMapping[currentIl2Device.Id] = mapping.ExpectedIndex;
                    devicesNeedingUpdate.Add((mapping, currentIl2Device, currentIl2Device.Id));
                }
                else
                {
                    AnsiConsole.MarkupLine($"  [green]?[/] {Markup.Escape(mapping.Name)} - [cyan]joy{currentIl2Device.Id}[/] [dim](OK)[/]");
                }
            }
            else
            {
                warnings.Add($"Device {Markup.Escape(mapping.Name)} not found in IL2 devices.txt");
            }
        }
        
        // Report warnings
        if (warnings.Count > 0)
        {
            AnsiConsole.WriteLine();
            var warningPanel = new Panel(string.Join("\n", warnings.Select(w => $"[yellow]?[/] {w}")))
            {
                Header = new PanelHeader(" [yellow]WARNINGS[/] ", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            };
            AnsiConsole.Write(warningPanel);
            AnsiConsole.MarkupLine("\n[dim]Consider running[/] [cyan]il2joy2 init[/] [dim]again if devices are missing.[/]");
        }
        
        // Apply updates if needed
        if (indexMapping.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]{indexMapping.Count} index change(s) required.[/]\n");
            
            AnsiConsole.Status()
                .Start("[yellow]Updating configuration files...[/]", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    
                    // Update IL2 devices.txt
                    ctx.Status("[yellow]Updating IL2 devices file...[/]");
                    UpdateIl2DevicesFile(config.DevicesFilePath, il2Devices, devicesNeedingUpdate);
                    
                    // Update bindings file
                    ctx.Status("[yellow]Updating IL2 bindings file...[/]");
                    _il2ConfigService.UpdateBindingsFile(config.BindingsFilePath, indexMapping);
                });
            
            var successPanel = new Panel("[green]? Configuration updated successfully![/]")
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Green)
            };
            AnsiConsole.Write(successPanel);
        }
        else
        {
            var okPanel = new Panel("[green]? All devices are correctly configured. No changes needed.[/]")
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Green)
            };
            AnsiConsole.Write(okPanel);
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
