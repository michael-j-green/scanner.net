using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScannerNet.Models;

namespace ScannerNet.Services;

public sealed class SnmpAdvertiseService : BackgroundService
{
    private const string BrotherScanOid = "1.3.6.1.4.1.2435.2.3.9.2.11.1.1.0";
    private readonly ScannerOptions _options;
    private readonly ILogger<SnmpAdvertiseService> _logger;

    public SnmpAdvertiseService(IOptions<ScannerOptions> options, ILogger<SnmpAdvertiseService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TryAdvertise();
            await Task.Delay(TimeSpan.FromSeconds(_options.AdvertiseIntervalSeconds), stoppingToken);
        }
    }

    private void TryAdvertise()
    {
        try
        {
            var advertiseAddress = $"{_options.AdvertiseIp}:{_options.AdvertisePort}";
            var endpoint = new IPEndPoint(IPAddress.Parse(_options.ScannerIp), _options.SnmpPort);
            var community = new OctetString(_options.SnmpCommunity);

            var appNum = 1;
            foreach (var (func, users) in _options.Menu)
            {
                foreach (var user in users.Keys)
                {
                    var cmd =
                        $"TYPE=BR;BUTTON=SCAN;USER=\"{user}\";FUNC={func.ToUpperInvariant()};HOST={advertiseAddress};APPNUM={appNum};DURATION={Math.Max(60, _options.AdvertiseIntervalSeconds)};BRID=;";

                    var variables = new List<Variable>
                    {
                        new(new ObjectIdentifier(BrotherScanOid), new OctetString(cmd)),
                    };
                    _ = Messenger.Set(VersionCode.V1, endpoint, community, variables, 1000);
                    appNum++;
                }
            }
        }
        catch (Exception ex)
        {
            // Fail silently when printer is offline.
            _logger.LogDebug(ex, "SNMP advertise attempt failed");
        }
    }
}
