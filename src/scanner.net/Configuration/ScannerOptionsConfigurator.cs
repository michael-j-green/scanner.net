using System.Text.Json;
using ScannerNet.Models;

namespace ScannerNet.Configuration;

public static class ScannerOptionsConfigurator
{
    public static void ApplySimpleEnvironmentOverrides(ScannerOptions options)
    {
        static string? Read(string name) => Environment.GetEnvironmentVariable(name);
        static int ReadInt(string name, int fallback) => int.TryParse(Read(name), out var v) ? v : fallback;

        options.ScannerIp = Read("SCANNER_IP") ?? options.ScannerIp;
        options.BindIp = Read("BIND_IP") ?? options.BindIp;
        options.BindPort = ReadInt("BIND_PORT", options.BindPort);
        options.AdvertiseIp = Read("ADVERTISE_IP") ?? options.AdvertiseIp;
        options.AdvertisePort = ReadInt("ADVERTISE_PORT", options.AdvertisePort);
        options.SnmpCommunity = Read("SNMP_COMMUNITY") ?? options.SnmpCommunity;
        options.SnmpPort = ReadInt("SNMP_PORT", options.SnmpPort);
        options.AdvertiseIntervalSeconds = ReadInt("CONFIG_INTERVAL_SECONDS", options.AdvertiseIntervalSeconds);
        options.ScanDelaySeconds = ReadInt("SCAN_DELAY_SECONDS", options.ScanDelaySeconds);
        options.DefaultOutputDir = Read("OUTPUT_DIR") ?? options.DefaultOutputDir;
        options.TempDir = Read("TEMP_DIR") ?? options.TempDir;

        var menuJson = Read("MENU_JSON");
        if (!string.IsNullOrWhiteSpace(menuJson))
        {
            var menu = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ScanProfile>>>(menuJson);
            if (menu is not null && menu.Count > 0)
            {
                options.Menu = new Dictionary<string, Dictionary<string, ScanProfile>>(menu, StringComparer.OrdinalIgnoreCase);
            }
        }

        var defaultDevice = Read("SANE_DEVICE");
        if (!string.IsNullOrWhiteSpace(defaultDevice))
        {
            foreach (var users in options.Menu.Values)
            {
                foreach (var profile in users.Values)
                {
                    profile.Device ??= defaultDevice;
                }
            }
        }
    }

    public static void EnsureDefaults(ScannerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AdvertiseIp))
        {
            options.AdvertiseIp = options.BindIp;
        }

        if (options.AdvertisePort <= 0)
        {
            options.AdvertisePort = options.BindPort;
        }

        if (options.Menu.Count > 0)
        {
            return;
        }

        options.Menu["FILE"] = new Dictionary<string, ScanProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["Standard"] = new()
            {
                Dir = options.DefaultOutputDir,
                Resolution = 300,
                Width = 210,
                Height = 297,
                Adf = false,
            },
            ["ADF"] = new()
            {
                Dir = options.DefaultOutputDir,
                Resolution = 300,
                Width = 210,
                Height = 297,
                Adf = true,
            },
        };
    }

    public static void ApplyProfileEnvironmentOverrides(ScannerOptions options)
    {
        static string? Read(string name) => Environment.GetEnvironmentVariable(name);
        static int? ReadInt(string name)
        {
            var raw = Read(name);
            return int.TryParse(raw, out var value) ? value : null;
        }

        var flatbedResolution = ReadInt("FLATBED_RESOLUTION");
        var adfResolution = ReadInt("ADF_RESOLUTION");
        var flatbedOutputDir = Read("FLATBED_OUTPUT_DIR");
        var adfOutputDir = Read("ADF_OUTPUT_DIR");

        foreach (var users in options.Menu.Values)
        {
            foreach (var (key, profile) in users)
            {
                var isAdf = profile.Adf || string.Equals(key, "ADF", StringComparison.OrdinalIgnoreCase);
                if (isAdf)
                {
                    profile.Resolution = adfResolution ?? profile.Resolution;
                    if (!string.IsNullOrWhiteSpace(adfOutputDir))
                    {
                        profile.Dir = adfOutputDir;
                    }
                }
                else
                {
                    profile.Resolution = flatbedResolution ?? profile.Resolution;
                    if (!string.IsNullOrWhiteSpace(flatbedOutputDir))
                    {
                        profile.Dir = flatbedOutputDir;
                    }
                }

                profile.Dir ??= options.DefaultOutputDir;
            }
        }
    }
}
