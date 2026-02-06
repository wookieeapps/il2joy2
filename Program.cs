using System.Runtime.Versioning;
using Il2Joy2;
using Il2Joy2.Commands;
using Il2Joy2.Services;

[SupportedOSPlatform("windows")]
internal class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine("IL2 Joystick Manager v1.0\n");
        
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
            Console.WriteLine("ERROR: init requires path to IL-2 input config folder.");
            Console.WriteLine("Usage: il2joy2 init <config_folder>");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine(@"  il2joy2 init ""C:\Games\IL-2\data\input""");
            Console.WriteLine();
            Console.WriteLine("The folder should contain:");
            Console.WriteLine($"  - {Constants.DevicesFileName}");
            Console.WriteLine($"  - {Constants.BindingsFileName}");
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
            Console.WriteLine($"ERROR: Invalid path format: {args[1]}");
            Console.WriteLine($"  Details: {ex.Message}");
            return 1;
        }
        
        // Validate folder exists
        if (!Directory.Exists(configFolder))
        {
            Console.WriteLine($"ERROR: Folder not found: {configFolder}");
            Console.WriteLine();
            Console.WriteLine("Troubleshooting tips:");
            Console.WriteLine("  - Make sure the path is enclosed in quotes if it contains spaces");
            Console.WriteLine(@"  - Example: il2joy2 init ""C:\Path With Spaces\input""");
            Console.WriteLine($"  - Received argument: '{args[1]}'");
            return 1;
        }
        
        // Look for required files
        var devicesFile = Path.Combine(configFolder, Constants.DevicesFileName);
        var bindingsFile = Path.Combine(configFolder, Constants.BindingsFileName);
        
        if (!File.Exists(devicesFile))
        {
            Console.WriteLine($"ERROR: {Constants.DevicesFileName} not found in: {configFolder}");
            return 1;
        }
        
        if (!File.Exists(bindingsFile))
        {
            Console.WriteLine($"ERROR: {Constants.BindingsFileName} not found in: {configFolder}");
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
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("  IL2 Joystick Manager - Automatic joystick ID management for IL-2");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("DESCRIPTION:");
        Console.WriteLine("  Prevents IL-2 Sturmovik joystick bindings from breaking when USB");
        Console.WriteLine("  devices are plugged/unplugged by automatically updating device IDs.");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  il2joy2 [command] [arguments]");
        Console.WriteLine();
        Console.WriteLine("COMMANDS:");
        Console.WriteLine();
        Console.WriteLine("  help | -h | --help | /?");
        Console.WriteLine("      Show this help message");
        Console.WriteLine();
        Console.WriteLine("  view");
        Console.WriteLine("      Display all connected joystick devices with their details");
        Console.WriteLine("      Shows: Device name, VID/PID, GUID, serial number, unique ID");
        Console.WriteLine("      Also displays current configuration if initialized");
        Console.WriteLine();
        Console.WriteLine("  init <config_folder>");
        Console.WriteLine("      Initialize configuration from IL-2 config folder");
        Console.WriteLine("      Arguments:");
        Console.WriteLine("        config_folder - Path to IL-2's input config folder");
        Console.WriteLine($"                        (contains {Constants.DevicesFileName} and {Constants.BindingsFileName})");
        Console.WriteLine($"      Creates: {Constants.AppConfigFileName} in application directory");
        Console.WriteLine();
        Console.WriteLine("  update (or no command)");
        Console.WriteLine("      Check connected devices and update IL-2 configuration if needed");
        Console.WriteLine("      - Scans for configured joysticks");
        Console.WriteLine("      - Detects ID changes");
        Console.WriteLine($"      - Updates {Constants.DevicesFileName} and {Constants.BindingsFileName}");
        Console.WriteLine("      - Creates timestamped backup files before any changes");
        Console.WriteLine("      - Fails if configured device is missing");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine();
        Console.WriteLine("  1. View connected devices:");
        Console.WriteLine("     il2joy2 view");
        Console.WriteLine();
        Console.WriteLine("  2. Initialize configuration (run once with all joysticks connected):");
        Console.WriteLine(@"     il2joy2 init ""C:\Games\IL-2\data\input""");
        Console.WriteLine();
        Console.WriteLine("  3. Check and update before launching IL-2 (default command):");
        Console.WriteLine("     il2joy2");
        Console.WriteLine("     il2joy2 update");
        Console.WriteLine();
        Console.WriteLine("  4. Show help:");
        Console.WriteLine("     il2joy2 help");
        Console.WriteLine("     il2joy2 -h");
        Console.WriteLine("     il2joy2 --help");
        Console.WriteLine("     il2joy2 /?");
        Console.WriteLine();
        Console.WriteLine("WORKFLOW:");
        Console.WriteLine("  Step 1: Connect all your flight sim devices (joystick, throttle, pedals)");
        Console.WriteLine("  Step 2: Run IL-2 and configure your controls normally");
        Console.WriteLine("  Step 3: Run 'il2joy2 init <config_folder>' to save current config");
        Console.WriteLine("  Step 4: Add 'il2joy2' to your IL-2 startup script or run manually before launch");
        Console.WriteLine("  Step 5: Enjoy - joystick IDs will be fixed automatically!");
        Console.WriteLine();
        Console.WriteLine("FILES:");
        Console.WriteLine($"  {Constants.AppConfigFileName}");
        Console.WriteLine("      Application configuration file created by 'init' command");
        Console.WriteLine("      Contains: IL-2 file paths and device mappings");
        Console.WriteLine("      Location: Same directory as il2joy2.exe");
        Console.WriteLine();
        Console.WriteLine($"  {Constants.DevicesFileName}.backup_YYYYMMDD_HHmmss");
        Console.WriteLine($"      Timestamped backup created before updating IL-2's {Constants.DevicesFileName}");
        Console.WriteLine($"      Location: Same folder as {Constants.DevicesFileName}");
        Console.WriteLine();
        Console.WriteLine($"  {Constants.BindingsFileName}.backup_YYYYMMDD_HHmmss");
        Console.WriteLine($"      Timestamped backup created before updating IL-2's {Constants.BindingsFileName}");
        Console.WriteLine($"      Location: Same folder as {Constants.BindingsFileName}");
        Console.WriteLine();
        Console.WriteLine("DEVICE MATCHING:");
        Console.WriteLine("  Devices are matched using VID/PID + Device name combination.");
        Console.WriteLine("  This ensures correct identification even when USB ports change.");
        Console.WriteLine();
        Console.WriteLine("EXIT CODES:");
        Console.WriteLine("  0 - Success");
        Console.WriteLine("  1 - Error (missing device, invalid config, etc.)");
        Console.WriteLine();
        
        return 0;
    }
    
    private static int ShowUnknownCommand(string command)
    {
        Console.WriteLine($"Unknown command: {command}");
        Console.WriteLine("Use 'il2joy2 help' for usage information.");
        return 1;
    }
}

