namespace ScannerNet.Models;

public sealed class ScanProfile
{
    public string? Dir { get; set; }
    public string? Device { get; set; }
    public int? Resolution { get; set; }
    public string? Mode { get; set; }
    public string? Source { get; set; }
    public int? Brightness { get; set; }
    public int? Contrast { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Left { get; set; }
    public int? Top { get; set; }
    public bool Adf { get; set; }
}
