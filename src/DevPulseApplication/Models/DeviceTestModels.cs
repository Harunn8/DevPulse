using DevPulseApplication.Responses;

namespace DevPulseApplication.Models
{
    public enum TestStage { Resolve, Ping, Port, Report }

    public enum StageState { Waiting, Active, Passed, Warning, Skipped, Failed }

    public enum TestEventKind { Started, Stage, Finished }

    public enum TestLogLevel { Info, Success, Warning, Error, System }

    /// <summary>Test sırasında her aşama değişiminde yayınlanan ilerleme bilgisi.</summary>
    public class DeviceTestProgress
    {
        public PagDeviceModel Device { get; set; } = new();
        public TestEventKind Kind { get; set; }
        public TestStage Stage { get; set; }
        public StageState State { get; set; }
        public string Message { get; set; } = "";
        public TestLogLevel Level { get; set; }
        public long? LatencyMs { get; set; }
        public TestResultResponse? Result { get; set; } // sadece Finished olayında dolu
    }

    public class DeviceTestOptions
    {
        public int Parallelism { get; set; } = 4; // aynı anda test edilecek cihaz sayısı
        public int MinStageDurationMs { get; set; } // arayüzde aşamaların izlenebilmesi için asgari süre (0 = beklemesiz)
    }

    public static class PagDeviceModelExtensions
    {
        public static string GetDisplayName(this PagDeviceModel device) =>
            string.IsNullOrWhiteSpace(device.Name) ? device.Device?.ModelName ?? "Adsız cihaz" : device.Name;

        /// <summary>UDP/SNMP tabanlı cihazlarda TCP bağlantı testi anlamsızdır.</summary>
        public static bool IsUdpBased(this PagDeviceModel device)
        {
            var connection = $"{device.Device?.ConnectionTypeName} {device.Device?.CommunicationTypeName}";
            return connection.Contains("udp", StringComparison.OrdinalIgnoreCase) || connection.Contains("snmp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Timeout değeri saniye (≤60) veya milisaniye olarak gelebilir; 500-10000 ms aralığına sabitlenir.</summary>
        public static int GetTimeoutMs(this PagDeviceModel device)
        {
            var timeout = device.Timeout;
            var ms = timeout <= 0 ? 2000 : timeout <= 60 ? timeout * 1000 : timeout;
            return Math.Clamp(ms, 500, 10000);
        }
    }
}
