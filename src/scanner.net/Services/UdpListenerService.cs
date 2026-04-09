using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScannerNet.Models;

namespace ScannerNet.Services;

public sealed class UdpListenerService : BackgroundService
{
    private readonly ScannerOptions _options;
    private readonly IScanQueue _queue;
    private readonly ILogger<UdpListenerService> _logger;

    public UdpListenerService(
        IOptions<ScannerOptions> options,
        IScanQueue queue,
        ILogger<UdpListenerService> logger)
    {
        _options = options.Value;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(_options.BindIp), _options.BindPort);
        using var client = new UdpClient(endpoint);
        _logger.LogInformation("Listening on {BindIp}:{BindPort}", _options.BindIp, _options.BindPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpReceiveResult packet;
            try
            {
                packet = await client.ReceiveAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var message = ParseBrotherMessage(packet.Buffer);
            if (message is null)
            {
                continue;
            }

            if (!message.TryGetValue("FUNC", out var func) || !message.TryGetValue("USER", out var user))
            {
                continue;
            }

            if (!TryResolveProfile(func, user, out var profile))
            {
                continue;
            }

            // Echo the packet back; the printer waits for this ACK before opening TCP 54921.
            try
            {
                await client.SendAsync(packet.Buffer, packet.RemoteEndPoint, stoppingToken);
            }
            catch (Exception echoEx)
            {
                _logger.LogDebug(echoEx, "UDP echo failed");
            }

            await _queue.EnqueueAsync(new ScanRequest(func, user, profile), stoppingToken);
        }
    }

    private bool TryResolveProfile(string func, string user, out ScanProfile profile)
    {
        profile = new ScanProfile();

        foreach (var (menuFunc, users) in _options.Menu)
        {
            if (!string.Equals(menuFunc, func, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var (menuUser, entry) in users)
            {
                if (string.Equals(menuUser, user, StringComparison.OrdinalIgnoreCase))
                {
                    profile = entry;
                    return true;
                }
            }
        }

        return false;
    }

    private static Dictionary<string, string>? ParseBrotherMessage(byte[] data)
    {
        if (data.Length < 4 || data[0] != 2 || data[1] != 0 || data[3] != 0x30)
        {
            return null;
        }

        var payload = Encoding.UTF8.GetString(data, 4, data.Length - 4);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var name = part[..idx];
            var value = part[(idx + 1)..];
            if (string.Equals(name, "USER", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Trim('"');
            }

            dict[name] = value;
        }

        return dict;
    }
}
