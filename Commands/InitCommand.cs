using Il2Joy2.Models;
using Il2Joy2.Services;
using Spectre.Console;

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
        AnsiConsole.Write(new Rule("[cyan1]Initialize Configuration[/]").LeftJustified());
        AnsiConsole.WriteLine();
        
        // Validate paths
        if (!File.Exists(devicesFilePath))
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] Devices file not found: [yellow]{devicesFilePath}[/]");
            return 1;
        }
        
        if (!File.Exists(bindingsFilePath))
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] Bindings file not found: [yellow]{bindingsFilePath}[/]");
            return 1;
        }
        
        // Check for existing config
        if (_appConfigService.ConfigExists())
        {
            if (!AnsiConsole.Confirm("[yellow]Configuration already exists. Overwrite?[/]", false))
            {
                AnsiConsole.MarkupLine("[yellow]Initialization cancelled.[/]");
                return 0;
            }
        }
        
        // Parse IL2 devices file
        List<Il2Device> il2Devices = [];
        AnsiConsole.Status()
            .Start("[yellow]Reading IL2 devices file...[/]", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                il2Devices = _il2ConfigService.ParseDevicesFile(devicesFilePath);
            });
        
        AnsiConsole.MarkupLine($"[green]Found {il2Devices.Count} device(s) in IL2 config.[/]\n");
        
        var il2Table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn(new TableColumn("[cyan]Index[/]").Centered())
            .AddColumn(new TableColumn("[green]Device Name[/]"))
            .AddColumn(new TableColumn("[blue]GUID[/]"));
        
        foreach (var device in il2Devices)
        {
            il2Table.AddRow($"[cyan]joy{device.Id}[/]", Markup.Escape(device.Model), $"[dim]{Markup.Escape(device.Guid)}[/]");
        }
        
        AnsiConsole.Write(il2Table);
        AnsiConsole.WriteLine();
        
        // Get currently connected devices
        List<JoystickDevice> connectedDevices = [];
        AnsiConsole.Status()
            .Start("[yellow]Scanning connected devices...[/]", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                connectedDevices = _enumerator.EnumerateJoysticks();
            });
        
        AnsiConsole.MarkupLine($"[green]Found {connectedDevices.Count} connected device(s).[/]\n");
        
        // Match IL2 devices with connected devices
        var mappings = new List<DeviceMapping>();
        var unmatchedIl2Devices = new List<Il2Device>();
        
        AnsiConsole.MarkupLine("[yellow]Matching devices...[/]\n");
        
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
                
                AnsiConsole.MarkupLine($"  [green]?[/] Matched [cyan]joy{il2Device.Id}[/] {Markup.Escape(il2Device.Model)}");
                AnsiConsole.MarkupLine($"    [dim]? {Markup.Escape(matchedDevice.UniqueIdentifier)}[/]");
            }
            else
            {
                unmatchedIl2Devices.Add(il2Device);
                AnsiConsole.MarkupLine($"  [red]?[/] Could not match [cyan]joy{il2Device.Id}[/] {Markup.Escape(il2Device.Model)}");
            }
        }
        
        if (unmatchedIl2Devices.Count > 0)
        {
            AnsiConsole.WriteLine();
            var warningPanel = new Panel(
                $"[yellow]{unmatchedIl2Devices.Count} device(s) could not be matched.[/]\n" +
                "[dim]Make sure all your joysticks are connected before running init.[/]")
            {
                Header = new PanelHeader(" [yellow]WARNING[/] ", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            };
            AnsiConsole.Write(warningPanel);
            
            if (!AnsiConsole.Confirm("\n[yellow]Continue with partial configuration?[/]", false))
            {
                AnsiConsole.MarkupLine("[yellow]Initialization cancelled.[/]");
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
        
        AnsiConsole.WriteLine();
        _appConfigService.SaveConfig(config);
        
        var successPanel = new Panel(
            $"[green]? Configuration initialized with {mappings.Count} device mapping(s).[/]\n" +
            $"[dim]Config file:[/] [yellow]{_appConfigService.GetConfigPath()}[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };
        AnsiConsole.Write(successPanel);
        
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
