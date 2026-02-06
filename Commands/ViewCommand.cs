using Il2Joy2.Models;
using Il2Joy2.Services;
using Spectre.Console;

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
        AnsiConsole.Write(new Rule("[cyan1]Device View[/]").LeftJustified());
        AnsiConsole.WriteLine();
        
        // Get current devices
        AnsiConsole.Status()
            .Start("[yellow]Scanning for connected joystick devices...[/]", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                Thread.Sleep(500); // Brief pause for visual effect
            });
        
        var devices = _enumerator.EnumerateJoysticks();
        
        if (devices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No joystick devices found.[/]");
            AnsiConsole.MarkupLine("\n[dim]Note: Make sure your devices are connected and recognized by Windows.[/]");
            return 0;
        }
        
        AnsiConsole.MarkupLine($"[green]Found {devices.Count} device(s):[/]\n");
        
        // Create device table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn(new TableColumn("[cyan]Property[/]").Width(15))
            .AddColumn(new TableColumn("[green]Value[/]"));
        
        foreach (var device in devices)
        {
            table.AddRow("[yellow]Device[/]", $"[bold]{Markup.Escape(device.Name)}[/]");
            table.AddRow("[dim]Instance ID[/]", $"[dim]{Markup.Escape(device.DeviceInstanceId)}[/]");
            table.AddRow("[dim]GUID[/]", $"[cyan]{Markup.Escape(device.Guid)}[/]");
            table.AddRow("[dim]Vendor ID[/]", $"[green]{Markup.Escape(device.VendorId ?? "N/A")}[/]");
            table.AddRow("[dim]Product ID[/]", $"[green]{Markup.Escape(device.ProductId ?? "N/A")}[/]");
            table.AddRow("[dim]Unique ID[/]", $"[blue]{Markup.Escape(device.UniqueIdentifier)}[/]");
            
            if (device != devices.Last())
                table.AddEmptyRow();
        }
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        
        // Show stored configuration if available
        var config = _configService.LoadConfig();
        if (config != null)
        {
            AnsiConsole.Write(new Rule("[cyan1]Stored Configuration[/]").LeftJustified());
            AnsiConsole.WriteLine();
            
            AnsiConsole.MarkupLine($"[dim]Devices file:[/] [yellow]{config.DevicesFilePath}[/]");
            AnsiConsole.MarkupLine($"[dim]Bindings file:[/] [yellow]{config.BindingsFilePath}[/]");
            AnsiConsole.MarkupLine($"\n[cyan]Configured mappings ({config.DeviceMappings.Count}):[/]\n");
            
            var mappingTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[cyan]Index[/]").Centered())
                .AddColumn(new TableColumn("[green]Device Name[/]"))
                .AddColumn(new TableColumn("[blue]Unique ID[/]"))
                .AddColumn(new TableColumn("[yellow]Status[/]").Centered());
            
            foreach (var mapping in config.DeviceMappings.OrderBy(m => m.ExpectedIndex))
            {
                var currentDevice = devices.FirstOrDefault(d => d.UniqueIdentifier == mapping.UniqueIdentifier);
                var status = currentDevice != null 
                    ? "[green]? Connected[/]" 
                    : "[red]? NOT FOUND[/]";
                
                mappingTable.AddRow(
                    $"[cyan]joy{mapping.ExpectedIndex}[/]",
                    Markup.Escape(mapping.Name),
                    $"[dim]{Markup.Escape(mapping.UniqueIdentifier)}[/]",
                    status
                );
            }
            
            AnsiConsole.Write(mappingTable);
        }
        else
        {
            AnsiConsole.MarkupLine("\n[yellow]No configuration found.[/] Run [cyan]il2joy2 init[/] to create initial configuration.");
        }
        
        return 0;
    }
}
