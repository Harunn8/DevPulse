using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DevPulseApplication.Responses;

namespace DevPulseDesk.Services
{
    /// <summary>
    /// Cihazları çok aşamalı olarak test eder: adres çözümleme → ICMP ping → TCP port → raporlama.
    /// Her aşama <see cref="IProgress{T}"/> ile UI'a bildirilir ve sonuç <see cref="TestResultResponse"/> olarak üretilir.
    /// </summary>
    internal sealed class DeviceTestRunner
    {
        private readonly Random _random = new();

        /// <summary>Gerçek ağ erişimi yerine rastgele sonuçlar üretir (demo modu).</summary>
        public bool Simulate { get; init; }

        public int Parallelism { get; init; } = 4;

        /// <summary>Animasyonların izlenebilmesi için her aşamanın ekranda kalacağı asgari süre.</summary>
        public int MinStageDurationMs { get; init; } = 350;

        public async Task<IReadOnlyList<TestResultResponse>> RunAsync(IReadOnlyList<DeviceTestItem> items, IProgress<TestEvent> progress, CancellationToken ct)
        {
            using var gate = new SemaphoreSlim(Parallelism);
            var tasks = items.Select(async item =>
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    return await TestDeviceAsync(item, progress, ct).ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            });
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task<TestResultResponse> TestDeviceAsync(DeviceTestItem item, IProgress<TestEvent> progress, CancellationToken ct)
        {
            void Report(TestEventKind kind, TestStage stage, StageState state, string message, LogLevel level, long? latency = null, TestResultResponse? result = null) =>
                progress.Report(new TestEvent(item, kind, stage, state, message, level, latency, result));

            Report(TestEventKind.Started, TestStage.Resolve, StageState.Active, "Test başlatıldı", LogLevel.Info);

            // 1) Adres çözümleme
            Report(TestEventKind.Stage, TestStage.Resolve, StageState.Active, $"Adres çözümleniyor: {item.Host}", LogLevel.Info);
            var watch = Stopwatch.StartNew();
            var address = await ResolveAsync(item, ct).ConfigureAwait(false);
            await PaceAsync(watch, ct).ConfigureAwait(false);
            if (address == null)
            {
                Report(TestEventKind.Stage, TestStage.Resolve, StageState.Failed, $"{item.Name}: '{item.Host}' adresi çözümlenemedi", LogLevel.Error);
                return Finish(item, null, false, "Adres çözümlenemedi", Report);
            }
            Report(TestEventKind.Stage, TestStage.Resolve, StageState.Passed, $"{item.Name}: adres doğrulandı ({address})", LogLevel.Info);

            // 2) ICMP ping
            Report(TestEventKind.Stage, TestStage.Ping, StageState.Active, $"ICMP ping gönderiliyor → {address}", LogLevel.Info);
            watch.Restart();
            var ping = await PingAsync(item, address, ct).ConfigureAwait(false);
            await PaceAsync(watch, ct).ConfigureAwait(false);
            var pingState = ping.HasValue ? StageState.Passed : StageState.Warning;
            Report(TestEventKind.Stage, TestStage.Ping, pingState,
                ping.HasValue ? $"{item.Name}: ping yanıtı {ping} ms" : $"{item.Name}: ICMP yanıtı alınamadı (güvenlik duvarı engelliyor olabilir)",
                ping.HasValue ? LogLevel.Success : LogLevel.Warning, ping);

            // 3) TCP port
            StageState portState;
            if (item.Port <= 0)
            {
                portState = StageState.Skipped;
                Report(TestEventKind.Stage, TestStage.Port, portState, $"{item.Name}: port tanımlı değil, port testi atlandı", LogLevel.Info);
            }
            else if (item.IsUdp)
            {
                portState = StageState.Skipped;
                Report(TestEventKind.Stage, TestStage.Port, portState, $"{item.Name}: UDP/SNMP portu {item.Port} – TCP bağlantı testi atlandı", LogLevel.Info);
            }
            else
            {
                Report(TestEventKind.Stage, TestStage.Port, StageState.Active, $"TCP bağlantısı kuruluyor → {address}:{item.Port}", LogLevel.Info);
                watch.Restart();
                var (portLatency, error) = await ConnectAsync(item, address, ct).ConfigureAwait(false);
                await PaceAsync(watch, ct).ConfigureAwait(false);
                portState = portLatency.HasValue ? StageState.Passed : StageState.Failed;
                Report(TestEventKind.Stage, TestStage.Port, portState,
                    portLatency.HasValue ? $"{item.Name}: port {item.Port} açık ({portLatency} ms)" : $"{item.Name}: port {item.Port} erişilemez – {error}",
                    portLatency.HasValue ? LogLevel.Success : LogLevel.Error, portLatency);
            }

            var success = portState == StageState.Passed || (portState == StageState.Skipped && pingState == StageState.Passed);
            var summary = success
                ? (portState == StageState.Passed ? $"Port {item.Port} üzerinden erişilebilir" : "ICMP üzerinden erişilebilir")
                : portState == StageState.Failed ? $"Port {item.Port} yanıt vermiyor" : "Cihaza ulaşılamıyor";
            return Finish(item, address, success, summary, Report);
        }

        private TestResultResponse Finish(DeviceTestItem item, IPAddress? address, bool success, string summary,
            Action<TestEventKind, TestStage, StageState, string, LogLevel, long?, TestResultResponse?> report)
        {
            var result = new TestResultResponse
            {
                DeviceName = item.Name,
                IPAddress = address?.ToString() ?? item.Host,
                Port = item.Port,
                Status = success ? TestResult.Success : TestResult.Fail
            };

            report(TestEventKind.Stage, TestStage.Report, success ? StageState.Passed : StageState.Failed, summary, LogLevel.Info, null, null);
            report(TestEventKind.Finished, TestStage.Report, success ? StageState.Passed : StageState.Failed,
                success ? $"{item.Name} → BAŞARILI • {summary}" : $"{item.Name} → BAŞARISIZ • {summary}",
                success ? LogLevel.Success : LogLevel.Error, null, result);
            return result;
        }

        private async Task PaceAsync(Stopwatch watch, CancellationToken ct)
        {
            var remaining = MinStageDurationMs + _random.Next(0, 220) - (int)watch.ElapsedMilliseconds;
            if (remaining > 0) await Task.Delay(remaining, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }

        private async Task<IPAddress?> ResolveAsync(DeviceTestItem item, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(item.Host)) return null;
            if (Simulate)
            {
                await Task.Delay(_random.Next(60, 260), ct).ConfigureAwait(false);
                return IPAddress.TryParse(item.Host, out var simulated) && _random.NextDouble() > 0.04 ? simulated : null;
            }

            if (IPAddress.TryParse(item.Host, out var ip)) return ip;
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(item.Host, ct).ConfigureAwait(false);
                return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
            }
            catch (SocketException)
            {
                return null;
            }
        }

        private async Task<long?> PingAsync(DeviceTestItem item, IPAddress address, CancellationToken ct)
        {
            if (Simulate)
            {
                var latency = _random.Next(2, 90);
                await Task.Delay(latency * 3, ct).ConfigureAwait(false);
                return _random.NextDouble() < 0.82 ? latency : null;
            }

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, TimeSpan.FromMilliseconds(item.TimeoutMs), cancellationToken: ct).ConfigureAwait(false);
                return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
            }
            catch (PingException)
            {
                return null;
            }
        }

        private async Task<(long? Latency, string Error)> ConnectAsync(DeviceTestItem item, IPAddress address, CancellationToken ct)
        {
            if (Simulate)
            {
                var latency = _random.Next(3, 120);
                await Task.Delay(latency * 4, ct).ConfigureAwait(false);
                return _random.NextDouble() < 0.8 ? (latency, "") : (null, _random.NextDouble() < 0.5 ? "zaman aşımı" : "bağlantı reddedildi");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(item.TimeoutMs);
            var watch = Stopwatch.StartNew();
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(address, item.Port, timeout.Token).ConfigureAwait(false);
                return (watch.ElapsedMilliseconds, "");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return (null, $"zaman aşımı ({item.TimeoutMs} ms)");
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
