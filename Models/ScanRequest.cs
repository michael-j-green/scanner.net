namespace ScannerNet.Models;

public sealed record ScanRequest(string Func, string User, ScanProfile Profile);
