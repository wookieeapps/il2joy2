using System.Text.RegularExpressions;
using System.Web;
using Il2Joy2.Models;

namespace Il2Joy2.Services;

/// <summary>
/// Service for reading and writing IL2 configuration files
/// </summary>
public sealed partial class Il2ConfigService
{
    private readonly BackupService _backupService = new();

    /// <summary>
    /// Parses the IL2 devices.txt file
    /// </summary>
    public List<Il2Device> ParseDevicesFile(string filePath)
    {
        var devices = new List<Il2Device>();
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Devices file not found: {filePath}");
        }
        
        var lines = File.ReadAllLines(filePath);
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.Contains(','))
                continue;
                
            var parts = line.Split(',');
            if (parts.Length >= 3)
            {
                // Format: configId,guid,model|
                // Example: 0,%22d04d97a0-e9af-11f0-0000545345440180%22,VKBsim%20T-Rudder|
                
                if (!int.TryParse(parts[0].Trim(), out var id))
                    continue;
                
                var guid = HttpUtility.UrlDecode(parts[1].Trim().Trim('"'));
                var model = HttpUtility.UrlDecode(parts[2].Trim().TrimEnd('|'));
                
                devices.Add(new Il2Device
                {
                    Id = id,
                    Guid = guid.Trim('"'),
                    Model = model
                });
            }
        }
        
        return devices;
    }
    
    /// <summary>
    /// Writes the devices list back to IL2 devices.txt format.
    /// Automatically creates a timestamped backup before writing.
    /// </summary>
    public void WriteDevicesFile(string filePath, List<Il2Device> devices)
    {
        // Create backup before modifying
        if (File.Exists(filePath))
        {
            var backupPath = _backupService.CreateBackup(filePath);
            Console.WriteLine($"  Backup created: {backupPath}");
        }

        var lines = new List<string> { "configId,guid,model|" };
        
        foreach (var device in devices.OrderBy(d => d.Id))
        {
            var guid = HttpUtility.UrlEncode($"\"{device.Guid}\"").Replace("+", "%20");
            var model = HttpUtility.UrlEncode(device.Model).Replace("+", "%20");
            lines.Add($"{device.Id},{guid},{model}");
        }
        
        File.WriteAllLines(filePath, lines);
    }
    
    /// <summary>
    /// Updates joystick references in the bindings file (e.g., current.map).
    /// Automatically creates a timestamped backup before writing.
    /// Maps old device indices to new ones.
    /// </summary>
    public void UpdateBindingsFile(string filePath, Dictionary<int, int> indexMapping)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Bindings file not found: {filePath}");
        }
        
        if (indexMapping.Count == 0)
        {
            Console.WriteLine("No index changes needed.");
            return;
        }
        
        var lines = File.ReadAllLines(filePath);
        var updatedLines = new List<string>();
        var changesCount = 0;
        
        foreach (var line in lines)
        {
            var updatedLine = UpdateJoystickReferences(line, indexMapping);
            if (updatedLine != line)
            {
                changesCount++;
            }
            updatedLines.Add(updatedLine);
        }
        
        if (changesCount > 0)
        {
            // Create timestamped backup before writing
            var backupPath = _backupService.CreateBackup(filePath);
            Console.WriteLine($"  Backup created: {backupPath}");
            
            File.WriteAllLines(filePath, updatedLines);
            Console.WriteLine($"  Updated {changesCount} lines in bindings file.");
        }
        else
        {
            Console.WriteLine("No changes needed in bindings file.");
        }
    }
    
    /// <summary>
    /// Updates a single line, replacing joystick references according to the mapping
    /// </summary>
    private string UpdateJoystickReferences(string line, Dictionary<int, int> indexMapping)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;
        
        var result = line;
        
        // Match patterns like joy0, joy1, joy0_axis_x, joy0_b0, joy0_pov0_0
        // Sort by descending old index to avoid replacement conflicts (joy10 before joy1)
        foreach (var mapping in indexMapping.OrderByDescending(m => m.Key))
        {
            var oldPattern = $"joy{mapping.Key}";
            var newPattern = $"joy{mapping.Value}";
            
            if (result.Contains(oldPattern, StringComparison.OrdinalIgnoreCase))
            {
                // Use regex to ensure we match the exact joystick reference
                // and not partial matches (e.g., joy1 shouldn't match joy10)
                var regex = new Regex($@"\bjoy{mapping.Key}\b", RegexOptions.IgnoreCase);
                result = regex.Replace(result, newPattern);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Extracts all joystick indices used in the bindings file
    /// </summary>
    public HashSet<int> GetUsedJoystickIndices(string filePath)
    {
        var indices = new HashSet<int>();
        
        if (!File.Exists(filePath))
            return indices;
        
        var content = File.ReadAllText(filePath);
        var matches = JoyIndexRegex().Matches(content);
        
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out var index))
            {
                indices.Add(index);
            }
        }
        
        return indices;
    }
    
    [GeneratedRegex(@"\bjoy(\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex JoyIndexRegex();
}

/// <summary>
/// Represents a device entry from IL2's devices.txt
/// </summary>
public class Il2Device
{
    public int Id { get; set; }
    public required string Guid { get; set; }
    public required string Model { get; set; }
}
