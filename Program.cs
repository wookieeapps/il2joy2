using System.Runtime.Versioning;
using Il2Joy2;
using Il2Joy2.Commands;
using Il2Joy2.Services;
using Spectre.Console;

[SupportedOSPlatform("windows")]
internal class Program
{
    private static int Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("IL2Joy2").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[dim]Joystick Manager v1.0[/]\n");
        
        // Initialize services
        var enumerator = new JoystickEnumerator();
        var il2ConfigService = new Il2ConfigService();
        var appConfigService = new AppConfigService();
        
        // Parse command
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "update";
        
        return command switch
        {
            "view" => ExecuteViewCommand(enumerator, appConfigService),
            "init" => ExecuteInitCommand(args, enumerator, il2ConfigService, appConfigService),
            "update" or "" => ExecuteUpdateCommand(enumerator, il2ConfigService, appConfigService),
            "help" or "-h" or "--help" or "/?" => ShowHelp(),
            _ => ShowUnknownCommand(command)
        };
    }
    
    private static int ExecuteViewCommand(JoystickEnumerator enumerator, AppConfigService appConfigService)
    {
        var viewCommand = new ViewCommand(enumerator, appConfigService);
        return viewCommand.Execute();
    }
    
    private static int ExecuteInitCommand(
        string[] args, 
        JoystickEnumerator enumerator, 
        Il2ConfigService il2ConfigService,
        AppConfigService appConfigService)
    {
        if (args.Length < 2)
        {
            AnsiConsole.MarkupLine("[red]ERROR:[/] init requires path to IL-2 input config folder.");
            AnsiConsole.MarkupLine("[yellow]Usage:[/] il2joy2 init <config_folder>");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[cyan]Example:[/]");
            AnsiConsole.MarkupLine(@"  il2joy2 init ""C:\Games\IL-2\data\input""");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("The folder should contain:");
            AnsiConsole.MarkupLine($"  - [cyan]{Constants.DevicesFileName}[/]");
            AnsiConsole.MarkupLine($"  - [cyan]{Constants.BindingsFileName}[/]");
            return 1;
        }
        
        // Clean up the path - remove quotes, trailing slashes, and whitespace
        var configFolder = args[1]
            .Trim()
            .Trim('"', '\'')
            .TrimEnd('\\', '/');
        
        // Normalize the path
        try
        {
            configFolder = Path.GetFullPath(configFolder);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] Invalid path format: {args[1]}");
            AnsiConsole.MarkupLine($"  [dim]Details: {ex.Message}[/]");
            return 1;
        }
        
        // Validate folder exists
        if (!Directory.Exists(configFolder))
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] Folder not found: [yellow]{configFolder}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Troubleshooting tips:[/]");
            AnsiConsole.MarkupLine("  - Make sure the path is enclosed in quotes if it contains spaces");
            AnsiConsole.MarkupLine(@"  - Example: il2joy2 init ""C:\Path With Spaces\input""");
            AnsiConsole.MarkupLine($"  - Received argument: '[dim]{args[1]}[/]'");
            return 1;
        }
        
        // Look for required files
        var devicesFile = Path.Combine(configFolder, Constants.DevicesFileName);
        var bindingsFile = Path.Combine(configFolder, Constants.BindingsFileName);
        
        if (!File.Exists(devicesFile))
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] [cyan]{Constants.DevicesFileName}[/] not found in: [yellow]{configFolder}[/]");
            return 1;
        }
        
        if (!File.Exists(bindingsFile))
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] [cyan]{Constants.BindingsFileName}[/] not found in: [yellow]{configFolder}[/]");
            return 1;
        }
        
        var initCommand = new InitCommand(enumerator, il2ConfigService, appConfigService);
        return initCommand.Execute(devicesFile, bindingsFile);
    }
    
    private static int ExecuteUpdateCommand(
        JoystickEnumerator enumerator,
        Il2ConfigService il2ConfigService,
        AppConfigService appConfigService)
    {
        var updateCommand = new UpdateCommand(enumerator, il2ConfigService, appConfigService);
        return updateCommand.Execute();
    }
    
    private static int ShowHelp()
    {
        AnsiConsole.Write(new Rule("[cyan1]IL2 Joystick Manager[/]").LeftJustified());
        AnsiConsole.MarkupLine("[dim]Automatic joystick ID management for IL-2 Sturmovik[/]\n");
        
        AnsiConsole.MarkupLine("[yellow]DESCRIPTION:[/]");
        AnsiConsole.MarkupLine("  Prevents IL-2 Sturmovik joystick bindings from breaking when USB");
        AnsiConsole.MarkupLine("  devices are plugged/unplugged by automatically updating device IDs.\n");
        
        AnsiConsole.MarkupLine("[yellow]USAGE:[/]");
        AnsiConsole.Markup("  [cyan]il2joy2[/] ");
        AnsiConsole.Markup("[green]<command>[/] ");
        AnsiConsole.MarkupLine("[blue]<arguments>[/]\n");
        
        AnsiConsole.MarkupLine("[yellow]COMMANDS:[/]\n");
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[green]Command[/]").Width(20))
            .AddColumn(new TableColumn("[cyan]Description[/]"));
        
        table.AddRow("[green]help[/] | -h | --help | /?", "Show this help message");
        table.AddRow("[green]view[/]", "Display all connected joystick devices with details\n" +
                                      "[dim]Shows: Device name, VID/PID, GUID, unique ID[/]");
        table.AddRow("[green]init[/] [blue]<config_folder>[/]", "Initialize configuration from IL-2 config folder\n" +
                                                $"[dim]Folder must contain {Constants.DevicesFileName} and {Constants.BindingsFileName}[/]");
        table.AddRow("[green]update[/] (default)", "Check and update IL-2 configuration if needed\n" +
                                                   "[dim]- Detects device ID changes\n" +
                                                   "- Creates timestamped backups\n" +
                                                   "- Updates configuration files[/]");
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine("[yellow]EXAMPLES:[/]\n");
        
        var examples = new Panel(
            "[cyan]1.[/] View connected devices:\n" +
            "   [green]il2joy2 view[/]\n\n" +
            "[cyan]2.[/] Initialize (run once with all joysticks connected):\n" +
            @"   [green]il2joy2 init[/] [blue]""C:\Games\IL-2\data\input""[/]" + "\n\n" +
            "[cyan]3.[/] Check and update before launching IL-2:\n" +
            "   [green]il2joy2[/]  [dim]or[/]  [green]il2joy2 update[/]")
        {
            Header = new PanelHeader(" Quick Start ", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1)
        };
        AnsiConsole.Write(examples);
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine("[yellow]WORKFLOW:[/]");
        var workflow = new BarChart()
            .Width(60)
            .Label("[green bold]Setup Steps[/]")
            .CenterLabel();
        
        AnsiConsole.MarkupLine("  [cyan]1.[/] Connect all flight sim devices (joystick, throttle, pedals)");
        AnsiConsole.MarkupLine("  [cyan]2.[/] Run IL-2 and configure controls normally");
        AnsiConsole.MarkupLine("  [cyan]3.[/] Run [green]il2joy2 init <config_folder>[/]");
        AnsiConsole.MarkupLine("  [cyan]4.[/] Add [green]il2joy2[/] to IL-2 startup script");
        AnsiConsole.MarkupLine("  [cyan]5.[/] [green1]Enjoy - IDs fixed automatically![/]\n");
        
        AnsiConsole.MarkupLine("[yellow]DEVICE MATCHING:[/]");
        AnsiConsole.MarkupLine("  Devices matched using [cyan]VID/PID + Device name[/] combination.");
        AnsiConsole.MarkupLine("  [dim]This ensures correct identification when USB ports change.[/]\n");
        
        return 0;
    }
    
    private static int ShowUnknownCommand(string command)
    {
        AnsiConsole.MarkupLine($"[red]Unknown command:[/] [yellow]{command}[/]");
        AnsiConsole.MarkupLine("Use [cyan]il2joy2 help[/] for usage information.");
        return 1;
    }
}

