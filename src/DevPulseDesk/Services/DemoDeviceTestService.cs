using System.Net;
using DevPulseApplication.Models;
using DevPulseApplication.Services;
using Microsoft.Extensions.Options;

namespace DevPulseDesk.Services
{
    /// <summary>
    /// Demo modu: test akışı kütüphanedeki <see cref="DeviceTestService"/> ile aynıdır,
    /// yalnızca ağ yoklamaları (çözümleme, ping, port) rastgele sonuç üretir.
    /// </summary>
    internal sealed class DemoDeviceTestService(IOptions<DeviceTestOptions> options) : DeviceTestService(options)
    {
        private readonly Random _random = new();

        protected override async Task<IPAddress?> ResolveAsync(PagDeviceModel device, CancellationToken ct)
        {
            await Task.Delay(_random.Next(60, 260), ct);
            return IPAddress.TryParse(device.IpHost, out var ip) && _random.NextDouble() > 0.04 ? ip : null;
        }

        protected override async Task<long?> PingAsync(PagDeviceModel device, IPAddress address, CancellationToken ct)
        {
            var latency = _random.Next(2, 90);
            await Task.Delay(latency * 3, ct);
            return _random.NextDouble() < 0.82 ? latency : null;
        }

        protected override async Task<(long? Latency, string Error)> ConnectAsync(PagDeviceModel device, IPAddress address, CancellationToken ct)
        {
            var latency = _random.Next(3, 120);
            await Task.Delay(latency * 4, ct);
            return _random.NextDouble() < 0.8 ? (latency, "") : (null, _random.NextDouble() < 0.5 ? "zaman aşımı" : "bağlantı reddedildi");
        }
    }
}
