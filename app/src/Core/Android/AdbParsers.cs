using System.Globalization;
using System.Text.RegularExpressions;

namespace PCManager.Core.Android;

/// <summary>Parses only recorded/actual ADB text; no process or registry access.</summary>
public static class AdbParsers
{
    private static readonly Regex UserPattern = new(
        @"UserInfo\{(\d+):([^:}]*):([^}]*)\}\s*(.*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<AdbDevice> ParseDevices(string output)
    {
        var devices = new List<AdbDevice>();

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 ||
                line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("*", StringComparison.Ordinal) ||
                line.StartsWith("adb server", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            string serial = parts[0];
            int attributeStart = 2;
            AdbDeviceState state;

            if (parts[1].Equals("no", StringComparison.OrdinalIgnoreCase) &&
                parts.Length > 2 &&
                parts[2].Equals("permissions", StringComparison.OrdinalIgnoreCase))
            {
                state = AdbDeviceState.NoPermissions;
                attributeStart = 3;
            }
            else
            {
                state = ParseState(parts[1]);
            }

            string? model = null;
            string? product = null;
            string? device = null;
            string? transport = null;

            for (int index = attributeStart; index < parts.Length; index++)
            {
                int colon = parts[index].IndexOf(':');
                if (colon <= 0 || colon == parts[index].Length - 1)
                {
                    continue;
                }

                string key = parts[index][..colon];
                string value = parts[index][(colon + 1)..];

                switch (key)
                {
                    case "model": model = value; break;
                    case "product": product = value; break;
                    case "device": device = value; break;
                    case "transport_id": transport = value; break;
                }
            }

            devices.Add(new AdbDevice(
                serial,
                state,
                state == AdbDeviceState.NoPermissions ? "no permissions" : parts[1],
                model,
                product,
                device,
                transport,
                ConnectionFor(serial)));
        }

        return devices;
    }

    public static AdbDeviceState ParseState(string value) => value.ToLowerInvariant() switch
    {
        "device" => AdbDeviceState.Device,
        "unauthorized" => AdbDeviceState.Unauthorized,
        "offline" => AdbDeviceState.Offline,
        "no" => AdbDeviceState.NoPermissions,
        _ => AdbDeviceState.Unknown,
    };

    public static IReadOnlyDictionary<string, string> ParseGetProp(string output)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("[", StringComparison.Ordinal))
            {
                continue;
            }

            int closeKey = line.IndexOf("]: [", StringComparison.Ordinal);
            if (closeKey <= 1 || !line.EndsWith("]", StringComparison.Ordinal))
            {
                continue;
            }

            string key = line[1..closeKey];
            string value = line[(closeKey + 4)..^1];
            properties[key] = value;
        }

        return properties;
    }

    public static AdbDeviceInfo BuildInfo(AdbDevice device, IReadOnlyDictionary<string, string> properties)
    {
        string model = Value(properties, "ro.product.marketname", "ro.product.model", "ro.product.device");
        string display = Value(properties, "ro.product.marketname", "ro.product.model", model);

        return new AdbDeviceInfo(
            device.Serial,
            Value(properties, "ro.product.brand"),
            Value(properties, "ro.product.manufacturer"),
            model,
            Value(properties, "ro.product.device"),
            Value(properties, "ro.build.version.release"),
            Value(properties, "ro.build.version.sdk"),
            Value(properties, "ro.build.fingerprint"),
            Value(properties, "ro.build.version.security_patch"),
            Value(properties, "ro.build.version.incremental"),
            display,
            device.Connection switch
            {
                AdbConnectionKind.Emulator => "Emulator",
                AdbConnectionKind.Wireless => "Wireless debugging",
                AdbConnectionKind.Usb => "USB",
                _ => "Unknown",
            });
    }

    public static IReadOnlyList<AdbUser> ParseUsers(string output)
    {
        var users = new List<AdbUser>();

        foreach (string rawLine in output.Split('\n'))
        {
            Match match = UserPattern.Match(rawLine.Trim());
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int id))
            {
                continue;
            }

            string name = match.Groups[2].Value.Trim();
            string flags = match.Groups[3].Value.Trim();
            int bits = int.TryParse(flags, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
            bool work = (bits & 0x20) != 0 ||
                        name.Contains("work", StringComparison.OrdinalIgnoreCase);

            users.Add(new AdbUser(
                id,
                name,
                flags,
                match.Groups[4].Value.Contains("running", StringComparison.OrdinalIgnoreCase),
                work));
        }

        return users;
    }

    /// <summary>Parses only <c>package:&lt;id&gt;</c> lines; header text and blanks are ignored.</summary>
    public static IReadOnlyList<string> ParsePackageList(string output)
    {
        var packages = new List<string>();

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("package:", StringComparison.Ordinal))
            {
                continue;
            }

            string packageId = line["package:".Length..].Trim();
            if (packageId.Length > 0)
            {
                packages.Add(packageId);
            }
        }

        return packages.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static int? ParseBatteryPercent(string output)
    {
        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("level:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = line.Substring("level:".Length).Trim();
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent))
            {
                return percent is >= 0 and <= 100 ? percent : null;
            }
        }
        return null;
    }

    private static string Value(IReadOnlyDictionary<string, string> properties, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (properties.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "Unavailable";
    }

    private static AdbConnectionKind ConnectionFor(string serial)
    {
        if (serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase))
        {
            return AdbConnectionKind.Emulator;
        }

        return serial.Contains(':', StringComparison.Ordinal)
            ? AdbConnectionKind.Wireless
            : AdbConnectionKind.Usb;
    }
}
