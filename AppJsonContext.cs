using System.Text.Json.Serialization;
using Il2Joy2.Models;

namespace Il2Joy2;

/// <summary>
/// JSON source generator context for AOT-compatible serialization
/// </summary>
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(DeviceMapping))]
[JsonSerializable(typeof(List<DeviceMapping>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
