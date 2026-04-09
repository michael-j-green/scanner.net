using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ScannerNet.Configuration;
using ScannerNet.Models;
using ScannerNet.Services;

namespace ScannerNet;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        var configPath = Environment.GetEnvironmentVariable("CONFIG_FILE");
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: true);
        }

        builder.Configuration.AddEnvironmentVariables(prefix: "SCANNER_");

        var options = new ScannerOptions();
        builder.Configuration.GetSection("Scanner").Bind(options);
        ScannerOptionsConfigurator.ApplySimpleEnvironmentOverrides(options);
        ScannerOptionsConfigurator.EnsureDefaults(options);
        ScannerOptionsConfigurator.ApplyProfileEnvironmentOverrides(options);

        if (string.IsNullOrWhiteSpace(options.ScannerIp))
        {
            throw new InvalidOperationException("ScannerIp is required. Set SCANNER_IP.");
        }

        builder.Services.AddSingleton(Options.Create(options));
        builder.Services.AddSingleton<IScanQueue, ScanQueue>();
        builder.Services.AddHostedService<SnmpAdvertiseService>();
        builder.Services.AddHostedService<UdpListenerService>();
        builder.Services.AddHostedService<ScanWorkerService>();

        var app = builder.Build();
        await app.RunAsync();
    }
}
