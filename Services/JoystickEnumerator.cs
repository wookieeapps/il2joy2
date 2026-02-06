using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Il2Joy2.Models;
using Microsoft.Win32;

namespace Il2Joy2.Services;

/// <summary>
/// Service for enumerating joystick devices using Windows SetupAPI (AOT compatible)
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class JoystickEnumerator
{
    // HID Device Interface GUID
    private static readonly Guid GUID_DEVINTERFACE_HID = new("4D1E55B2-F16F-11CF-88CB-001111000030");
    
    // SetupAPI constants
    private const int DIGCF_PRESENT = 0x02;
    private const int DIGCF_DEVICEINTERFACE = 0x10;
    private const int SPDRP_DEVICEDESC = 0x00;
    private const int SPDRP_HARDWAREID = 0x01;

    /// <summary>
    /// Enumerates all connected joystick/gamepad devices
    /// </summary>
    public List<JoystickDevice> EnumerateJoysticks()
    {
        var devices = new List<JoystickDevice>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Method 1: Use SetupAPI to enumerate HID devices
        EnumerateHidDevices(devices, seenKeys);

        // Method 2: Check OEM registry for proper device names
        EnrichFromOemRegistry(devices, seenKeys);

        return devices;
    }


    private void EnumerateHidDevices(List<JoystickDevice> devices, HashSet<string> seenKeys)
    {
        var hidGuid = GUID_DEVINTERFACE_HID;
        var deviceInfoSet = SetupDiGetClassDevs(
            ref hidGuid,
            IntPtr.Zero,
            IntPtr.Zero,
            DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
            return;

        try
        {
            var deviceInfoData = new SP_DEVINFO_DATA
            {
                cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
            };

            uint deviceIndex = 0;
            while (SetupDiEnumDeviceInfo(deviceInfoSet, deviceIndex++, ref deviceInfoData))
            {
                var deviceDesc = GetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, SPDRP_DEVICEDESC);
                var hardwareId = GetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, SPDRP_HARDWAREID);

                if (string.IsNullOrEmpty(hardwareId))
                    continue;

                var vid = ExtractVidFromString(hardwareId);
                var pid = ExtractPidFromString(hardwareId);

                if (string.IsNullOrEmpty(vid) || string.IsNullOrEmpty(pid))
                    continue;

                var name = deviceDesc ?? "Unknown Device";

                // Filter: must be a game controller
                if (!IsGameControllerByName(name))
                    continue;

                var uniqueKey = $"{vid}:{pid}".ToLowerInvariant();
                if (seenKeys.Contains(uniqueKey))
                    continue;
                seenKeys.Add(uniqueKey);

                var deviceGuid = GenerateIl2StyleGuid(vid, pid, hardwareId);

                devices.Add(new JoystickDevice
                {
                    DeviceInstanceId = hardwareId,
                    Name = name,
                    Guid = deviceGuid,
                    VendorId = vid,
                    ProductId = pid
                });
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static string? GetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, int property)
    {
        // First call to get required buffer size
        SetupDiGetDeviceRegistryProperty(
            deviceInfoSet,
            ref deviceInfoData,
            (uint)property,
            out _,
            null,
            0,
            out uint requiredSize);

        if (requiredSize == 0)
            return null;

        var buffer = new byte[requiredSize];
        if (SetupDiGetDeviceRegistryProperty(
            deviceInfoSet,
            ref deviceInfoData,
            (uint)property,
            out _,
            buffer,
            requiredSize,
            out _))
        {
            // For multi-string properties (like HARDWAREID), take the first string
            var result = Encoding.Unicode.GetString(buffer).TrimEnd('\0');
            var firstNull = result.IndexOf('\0');
            return firstNull >= 0 ? result[..firstNull] : result;
        }

        return null;
    }

    private void EnrichFromOemRegistry(List<JoystickDevice> devices, HashSet<string> seenKeys)
    {
        try
        {
            using var oemKey = Registry.CurrentUser.OpenSubKey(
                @"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM");

            if (oemKey == null)
                return;

            foreach (var vidPidKey in oemKey.GetSubKeyNames())
            {
                try
                {
                    using var deviceKey = oemKey.OpenSubKey(vidPidKey);
                    if (deviceKey == null) continue;

                    var oemName = deviceKey.GetValue("OEMName")?.ToString();
                    if (string.IsNullOrEmpty(oemName))
                        continue;

                    var vid = ExtractVidFromString(vidPidKey);
                    var pid = ExtractPidFromString(vidPidKey);

                    if (string.IsNullOrEmpty(vid) || string.IsNullOrEmpty(pid))
                        continue;

                    // Update existing device with better name
                    var existingDevice = devices.Find(d =>
                        string.Equals(d.VendorId, vid, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(d.ProductId, pid, StringComparison.OrdinalIgnoreCase));

                    if (existingDevice != null)
                    {
                        if (existingDevice.Name.Contains("HID", StringComparison.OrdinalIgnoreCase) ||
                            existingDevice.Name.Contains("Unknown", StringComparison.OrdinalIgnoreCase))
                        {
                            existingDevice.Name = oemName;
                        }
                        continue;
                    }

                    // Add new device from registry
                    var uniqueKey = $"{vid}:{pid}".ToLowerInvariant();
                    if (seenKeys.Contains(uniqueKey))
                        continue;
                    seenKeys.Add(uniqueKey);

                    var guid = GenerateIl2StyleGuid(vid, pid, vidPidKey);

                    devices.Add(new JoystickDevice
                    {
                        DeviceInstanceId = vidPidKey,
                        Name = oemName,
                        Guid = guid,
                        VendorId = vid,
                        ProductId = pid
                    });
                }
                catch
                {
                    // Continue with next device
                }
            }
        }
        catch
        {
            // Registry access failed, continue without enrichment
        }
    }

    private static bool IsGameControllerByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        var lowerName = name.ToLowerInvariant();

        // Exclude non-controllers first
        ReadOnlySpan<string> excludeKeywords =
        [
            "keyboard", "mouse", "touchpad", "hub", "host controller",
            "card reader", "camera", "audio", "microphone", "speaker",
            "storage", "disk", "bluetooth", "wireless adapter", "network",
            "monitor", "display", "printer", "virtual", "root",
            "system controller", "consumer control", "vendor-defined"
        ];

        foreach (var keyword in excludeKeywords)
        {
            if (lowerName.Contains(keyword))
                return false;
        }

        // Must match positive indicators
        ReadOnlySpan<string> positiveKeywords =
        [
            "joystick", "gamepad", "game controller", "throttle", "rudder",
            "stick", "hotas", "vkb", "virpil", "thrustmaster", "saitek",
            "t.16000", "t16000", "warthog", "cougar", "x52", "x55", "x56",
            "gunfighter", "gladiator", "ch pro", "pedals", "mfg", "crosswind",
            "flight stick", "flight controller"
        ];

        foreach (var keyword in positiveKeywords)
        {
            if (lowerName.Contains(keyword))
                return true;
        }

        return false;
    }

    private static string GenerateIl2StyleGuid(string? vid, string? pid, string deviceId)
    {
        vid = (vid ?? "0000").ToLowerInvariant().PadLeft(4, '0');
        pid = (pid ?? "0000").ToLowerInvariant().PadLeft(4, '0');

        var hash = Math.Abs(deviceId.GetHashCode());
        var hashStr = hash.ToString("x8");

        return $"{pid}{vid}-{hashStr[..4]}-{hashStr.Substring(4, 4)}-0000000000000000";
    }

    private static string? ExtractVidFromString(string input)
    {
        var match = VidRegex().Match(input);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string? ExtractPidFromString(string input)
    {
        var match = PidRegex().Match(input);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    [GeneratedRegex(@"VID[_&]([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidRegex();

    [GeneratedRegex(@"PID[_&]([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex PidRegex();

    #region SetupAPI P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [LibraryImport("setupapi.dll", SetLastError = true, EntryPoint = "SetupDiGetClassDevsW")]
    private static partial IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        IntPtr enumerator,
        IntPtr hwndParent,
        int flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SP_DEVINFO_DATA deviceInfoData);

    [LibraryImport("setupapi.dll", SetLastError = true, EntryPoint = "SetupDiGetDeviceRegistryPropertyW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDeviceRegistryProperty(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        uint property,
        out uint propertyRegDataType,
        byte[]? propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    #endregion
}
