namespace ScannerNet.Models;

public sealed class ScannerOptions
{
    public string BindIp { get; set; } = "0.0.0.0";
    public int BindPort { get; set; } = 54925;
    public string AdvertiseIp { get; set; } = "0.0.0.0";
    public int AdvertisePort { get; set; } = 54925;
    public string ScannerIp { get; set; } = "";
    public int SnmpPort { get; set; } = 161;
    public string SnmpCommunity { get; set; } = "internal";
    public int AdvertiseIntervalSeconds { get; set; } = 600;
    public int ScanDelaySeconds { get; set; } = 30;
    public string DefaultOutputDir { get; set; } = "/output/scan";
    public string TempDir { get; set; } = "/tmp/scan";
    public Dictionary<string, Dictionary<string, ScanProfile>> Menu { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
