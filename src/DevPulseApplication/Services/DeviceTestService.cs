using DevPulseApplication.Models;
using DevPulseApplication.Responses;
using DevPulseApplication.Services.Base;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DevPulseApplication.Services
{
    public class DeviceTestService : IDeviceTestService
    {
        private readonly DeviceTestOptions _options;
        private readonly Random _random = new();

        public DeviceTestService(IOptions<DeviceTestOptions> options)
        {
            _options = options.Value;
        }

        public async Task<List<TestResultResponse>> RunTestsAsync(IReadOnlyList<PagDeviceModel> devices, IProgress<DeviceTestProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            using var gate = new SemaphoreSlim(Math.Max(1, _options.Parallelism));
            var tasks = devices.Select(async device =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await TestDeviceAsync(device, progress, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            });
            return (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();
        }

        public async Task<TestResultResponse> TestDeviceAsync(PagDeviceModel device, IProgress<DeviceTestProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var ct = cancellationToken;
            var name = device.GetDisplayName();
            var host = device.IpHost?.Trim() ?? "";
            var port = device.Port;

            void Report(TestEventKind kind, TestStage stage, StageState state, string message, TestLogLevel level, long? latency = null, TestResultResponse? result = null) =>
                progress?.Report(new DeviceTestProgress { Device = device, Kind = kind, Stage = stage, State = state, Message = message, Level = level, LatencyMs = latency, Result = result });

            Report(TestEventKind.Started, TestStage.Resolve, StageState.Active, "Test başlatıldı", TestLogLevel.Info);

            // 1) Adres çözümleme
            Report(TestEventKind.Stage, TestStage.Resolve, StageState.Active, $"Adres çözümleniyor: {host}", TestLogLevel.Info);
            var watch = Stopwatch.StartNew();
            var address = await ResolveAsync(device, ct).ConfigureAwait(false);
            await PaceAsync(watch, ct).ConfigureAwait(false);
            if (address == null)
            {
                Report(TestEventKind.Stage, TestStage.Resolve, StageState.Failed, $"{name}: '{host}' adresi çözümlenemedi", TestLogLevel.Error);
                return Finish(device, null, false, "Adres çözümlenemedi", Report);
            }
            Report(TestEventKind.Stage, TestStage.Resolve, StageState.Passed, $"{name}: adres doğrulandı ({address})", TestLogLevel.Info);

            // 2) ICMP ping – yanıt alınamaması tek başına başarısızlık sayılmaz (ICMP sıklıkla engellenir)
            Report(TestEventKind.Stage, TestStage.Ping, StageState.Active, $"ICMP ping gönderiliyor → {address}", TestLogLevel.Info);
            watch.Restart();
            var ping = await PingAsync(device, address, ct).ConfigureAwait(false);
            await PaceAsync(watch, ct).ConfigureAwait(false);
            var pingState = ping.HasValue ? StageState.Passed : StageState.Warning;
            Report(TestEventKind.Stage, TestStage.Ping, pingState,
                ping.HasValue ? $"{name}: ping yanıtı {ping} ms" : $"{name}: ICMP yanıtı alınamadı (güvenlik duvarı engelliyor olabilir)",
                ping.HasValue ? TestLogLevel.Success : TestLogLevel.Warning, ping);

            // 3) TCP port
            StageState portState;
            if (port <= 0)
            {
                portState = StageState.Skipped;
                Report(TestEventKind.Stage, TestStage.Port, portState, $"{name}: port tanımlı değil, port testi atlandı", TestLogLevel.Info);
            }
            else if (device.IsUdpBased())
            {
                portState = StageState.Skipped;
                Report(TestEventKind.Stage, TestStage.Port, portState, $"{name}: UDP/SNMP portu {port} – TCP bağlantı testi atlandı", TestLogLevel.Info);
            }
            else
            {
                Report(TestEventKind.Stage, TestStage.Port, StageState.Active, $"TCP bağlantısı kuruluyor → {address}:{port}", TestLogLevel.Info);
                watch.Restart();
                var (portLatency, error) = await ConnectAsync(device, address, ct).ConfigureAwait(false);
                await PaceAsync(watch, ct).ConfigureAwait(false);
                portState = portLatency.HasValue ? StageState.Passed : StageState.Failed;
                Report(TestEventKind.Stage, TestStage.Port, portState,
                    portLatency.HasValue ? $"{name}: port {port} açık ({portLatency} ms)" : $"{name}: port {port} erişilemez – {error}",
                    portLatency.HasValue ? TestLogLevel.Success : TestLogLevel.Error, portLatency);
            }

            var success = portState == StageState.Passed || (portState == StageState.Skipped && pingState == StageState.Passed);
            var summary = success
                ? (portState == StageState.Passed ? $"Port {port} üzerinden erişilebilir" : "ICMP üzerinden erişilebilir")
                : portState == StageState.Failed ? $"Port {port} yanıt vermiyor" : "Cihaza ulaşılamıyor";
            return Finish(device, address, success, summary, Report);
        }

        private static TestResultResponse Finish(PagDeviceModel device, IPAddress? address, bool success, string summary,
            Action<TestEventKind, TestStage, StageState, string, TestLogLevel, long?, TestResultResponse?> report)
        {
            var name = device.GetDisplayName();
            var result = new TestResultResponse
            {
                DeviceName = name,
                IPAddress = address?.ToString() ?? device.IpHost,
                Port = device.Port,
                Status = success ? TestResult.Success : TestResult.Fail
            };

            report(TestEventKind.Stage, TestStage.Report, success ? StageState.Passed : StageState.Failed, summary, TestLogLevel.Info, null, null);
            report(TestEventKind.Finished, TestStage.Report, success ? StageState.Passed : StageState.Failed,
                success ? $"{name} → BAŞARILI • {summary}" : $"{name} → BAŞARISIZ • {summary}",
                success ? TestLogLevel.Success : TestLogLevel.Error, null, result);
            return result;
        }

        private async Task PaceAsync(Stopwatch watch, CancellationToken ct)
        {
            if (_options.MinStageDurationMs > 0)
            {
                var remaining = _options.MinStageDurationMs + _random.Next(0, 220) - (int)watch.ElapsedMilliseconds;
                if (remaining > 0) await Task.Delay(remaining, ct).ConfigureAwait(false);
            }
            ct.ThrowIfCancellationRequested();
        }

        protected virtual async Task<IPAddress?> ResolveAsync(PagDeviceModel device, CancellationToken ct)
        {
            var host = device.IpHost?.Trim();
            if (string.IsNullOrWhiteSpace(host)) return null;
            if (IPAddress.TryParse(host, out var ip)) return ip;
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
                return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
            }
            catch (SocketException)
            {
                return null;
            }
        }

        /// <returns>Gecikme (ms); yanıt yoksa null.</returns>
        protected virtual async Task<long?> PingAsync(PagDeviceModel device, IPAddress address, CancellationToken ct)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, TimeSpan.FromMilliseconds(device.GetTimeoutMs()), cancellationToken: ct).ConfigureAwait(false);
                return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
            }
            catch (PingException)
            {
                return null;
            }
        }

        /// <returns>Bağlantı süresi (ms) veya hata açıklaması.</returns>
        protected virtual async Task<(long? Latency, string Error)> ConnectAsync(PagDeviceModel device, IPAddress address, CancellationToken ct)
        {
            var timeoutMs = device.GetTimeoutMs();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(timeoutMs);
            var watch = Stopwatch.StartNew();
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(address, device.Port, timeout.Token).ConfigureAwait(false);
                return (watch.ElapsedMilliseconds, "");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return (null, $"zaman aşımı ({timeoutMs} ms)");
            }
            catch (SocketException ex)
            {
                return (null, ex.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused => "bağlantı reddedildi",
                    SocketError.HostUnreachable or SocketError.NetworkUnreachable => "ağ erişilemez",
                    SocketError.TimedOut => "zaman aşımı",
                    _ => ex.SocketErrorCode.ToString()
                });
            }
        }
    }
}
