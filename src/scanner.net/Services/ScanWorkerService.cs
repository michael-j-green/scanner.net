using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using ScannerNet.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ScannerNet.Services;

public sealed class ScanWorkerService : BackgroundService
{
    private readonly ScannerOptions _options;
    private readonly IScanQueue _queue;
    private readonly ILogger<ScanWorkerService> _logger;

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    public ScanWorkerService(IOptions<ScannerOptions> options, IScanQueue queue, ILogger<ScanWorkerService> logger)
    {
        _options = options.Value;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await HandleScanAsync(request, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scan handling failed for {Func}/{User}", request.Func, request.User);
            }
        }
    }

    private async Task HandleScanAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        var profile = request.Profile;
        var dstDir = string.IsNullOrWhiteSpace(profile.Dir) ? _options.DefaultOutputDir : profile.Dir;
        var now = DateTime.Now.ToString("yyyyMMddHHmmss");
        var tempDir = Path.Combine(_options.TempDir, now);

        Directory.CreateDirectory(dstDir!);
        Directory.CreateDirectory(tempDir);

        await HandleBrotherScanAsync(profile, tempDir, dstDir!, now, cancellationToken);
    }

    private async Task HandleBrotherScanAsync(
        ScanProfile profile,
        string tempDir,
        string dstDir,
        string now,
        CancellationToken cancellationToken)
    {
        var resolution = profile.Resolution ?? 300;
        var mode = MapSaneModeToBrother(profile.Mode);
        var configPath = Path.Combine(tempDir, "brother.config");

        await File.WriteAllTextAsync(configPath,
            $"scan.func /app/scan-hook.sh\n" +
            $"scan.param R {resolution},{resolution}\n" +
            $"scan.param M {mode}\n" +
            $"\n" +
            $"ip {_options.ScannerIp}\n" +
            $"preset default FILE\n",
            cancellationToken);

        _logger.LogInformation("Starting brother-scan-cli for {ScannerIp}", _options.ScannerIp);

        var scanResult = await RunProcessAsync("/app/brother-scan-cli", new[] { "-c", configPath }, cancellationToken,
            workingDirectory: tempDir);

        var pageFiles = Directory
            .GetFiles(tempDir, "scan*.*")
            .Where(f => !f.EndsWith(".config", StringComparison.OrdinalIgnoreCase)
                     && !f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .OrderBy(NumericSuffix)
            .ToList();

        if (pageFiles.Count == 0)
        {
            var tempEntries = Directory.GetFiles(tempDir).Select(Path.GetFileName).OrderBy(x => x).ToArray();
            LogProcessOutput("brother-scan-cli", scanResult);
            _logger.LogWarning(
                "No page files found in {TempDir} after scan. Temp contents: {TempEntries}",
                tempDir,
                tempEntries.Length == 0 ? "<empty>" : string.Join(", ", tempEntries));
            return;
        }

        _logger.LogInformation("Received {Count} page(s): {Files}", pageFiles.Count,
            string.Join(", ", pageFiles.Select(Path.GetFileName)));

        var pdfFiles = new List<string>(pageFiles.Count);
        foreach (var pageFile in pageFiles)
        {
            TrimTrailingGrayBlock(pageFile);
            var pdfFile = Path.ChangeExtension(pageFile, ".pdf");
            await ConvertToPdfAsync(pageFile, pdfFile, cancellationToken);
            pdfFiles.Add(pdfFile);
        }

        if (!string.Equals(dstDir, "/output/duplex", StringComparison.Ordinal))
        {
            await WriteStandardOutputAsync(pdfFiles, tempDir, dstDir, now, cancellationToken);
        }
        else
        {
            var oddFile = Path.Combine(dstDir, "odd.pdf");
            var evenFile = Path.Combine(dstDir, "even.pdf");
            var target = File.Exists(oddFile) ? evenFile : oddFile;
            await MergePdfAsync(pdfFiles, target, cancellationToken);

            if (File.Exists(oddFile) && File.Exists(evenFile))
            {
                var outFile = Path.Combine(dstDir, $"{now}.pdf");
                _ = await RunProcessAsync("pdftk", new[]
                {
                    $"A={oddFile}", $"B={evenFile}",
                    "shuffle", "A", "Bend-1", "output", outFile,
                }, cancellationToken);
                File.Delete(oddFile);
                File.Delete(evenFile);
            }
        }

        foreach (var f in pdfFiles.Where(File.Exists))
        {
            File.Delete(f);
        }

        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }

    private static async Task WriteStandardOutputAsync(
        List<string> pdfFiles,
        string tempDir,
        string dstDir,
        string now,
        CancellationToken cancellationToken)
    {
        var tempMergedOutputFile = Path.Combine(tempDir, $"{now}.pdf");
        var finalOutputFile = Path.Combine(dstDir, $"{now}.pdf");

        await MergePdfAsync(pdfFiles, tempMergedOutputFile, cancellationToken);
        File.Move(tempMergedOutputFile, finalOutputFile, overwrite: true);
    }

    private static string MapSaneModeToBrother(string? saneMode) => saneMode?.ToUpperInvariant() switch
    {
        "GRAY" or "GRAYSCALE" => "GRAY64",
        "LINEART" or "TEXT" => "TEXT",
        _ => "CGRAY",
    };

    private static int NumericSuffix(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var i = name.Length;
        while (i > 0 && char.IsDigit(name[i - 1]))
        {
            i--;
        }

        return (i < name.Length && int.TryParse(name[i..], out var n)) ? n : 0;
    }

    private void TrimTrailingGrayBlock(string inputFile)
    {
        var extension = Path.GetExtension(inputFile);
        if (!string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var image = Image.Load<Rgba32>(inputFile);
        var trimmedHeight = FindTrimmedHeight(image);
        if (trimmedHeight >= image.Height)
        {
            return;
        }

        image.Mutate(context => context.Crop(new Rectangle(0, 0, image.Width, trimmedHeight)));
        image.Save(inputFile);
        _logger.LogInformation(
            "Trimmed trailing gray rows from {FileName}: {OriginalHeight} -> {TrimmedHeight}",
            Path.GetFileName(inputFile),
            image.Height,
            trimmedHeight);
    }

    private static int FindTrimmedHeight(Image<Rgba32> image)
    {
        const int minTrimRows = 24;
        const int maxChannelSpread = 18;
        const int maxLumaSpread = 12;
        const int minGrayLuma = 40;
        const int maxGrayLuma = 235;

        var sampleStep = Math.Max(1, image.Width / 256);
        var trailingGrayRows = 0;

        for (var y = image.Height - 1; y >= 0; y--)
        {
            var minLuma = 255;
            var maxLuma = 0;
            var minChannel = 255;
            var maxChannel = 0;
            long totalLuma = 0;
            var samples = 0;

            for (var x = 0; x < image.Width; x += sampleStep)
            {
                var pixel = image[x, y];
                var luma = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                totalLuma += luma;
                samples++;

                minLuma = Math.Min(minLuma, luma);
                maxLuma = Math.Max(maxLuma, luma);

                minChannel = Math.Min(minChannel, Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)));
                maxChannel = Math.Max(maxChannel, Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)));
            }

            var averageLuma = (int)(totalLuma / Math.Max(1, samples));
            var looksUniformGray = averageLuma >= minGrayLuma
                && averageLuma <= maxGrayLuma
                && (maxLuma - minLuma) <= maxLumaSpread
                && (maxChannel - minChannel) <= maxChannelSpread;

            if (!looksUniformGray)
            {
                break;
            }

            trailingGrayRows++;
        }

        if (trailingGrayRows < minTrimRows)
        {
            return image.Height;
        }

        return Math.Max(1, image.Height - trailingGrayRows);
    }

    private static Task ConvertToPdfAsync(
        string inputFile,
        string pdfFile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var document = new PdfDocument();
        using var image = XImage.FromFile(inputFile);

        var page = document.AddPage();
        page.Width = image.PointWidth;
        page.Height = image.PointHeight;

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawImage(image, 0, 0, page.Width, page.Height);
        }

        document.Save(pdfFile);
        return Task.CompletedTask;
    }

    private static Task MergePdfAsync(List<string> inputFiles, string outputFile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var output = new PdfDocument();
        foreach (var inputFile in inputFiles)
        {
            using var input = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import);
            for (var i = 0; i < input.PageCount; i++)
            {
                output.AddPage(input.Pages[i]);
            }
        }

        output.Save(outputFile);
        return Task.CompletedTask;
    }

    private void LogProcessOutput(string fileName, ProcessResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            _logger.LogInformation("{FileName} stdout:{NewLine}{Output}", fileName, Environment.NewLine, result.StandardOutput.Trim());
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            _logger.LogWarning("{FileName} stderr:{NewLine}{Output}", fileName, Environment.NewLine, result.StandardError.Trim());
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken,
        int[]? allowedExtraCodes = null,
        string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Unable to start {fileName}");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken));
        var result = new ProcessResult(process.ExitCode, stdoutTask.Result, stderrTask.Result);

        if (process.ExitCode != 0 && allowedExtraCodes?.Contains(process.ExitCode) != true)
        {
            throw new InvalidOperationException($"{fileName} failed with code {process.ExitCode}: {result.StandardError}");
        }

        return result;
    }
}
