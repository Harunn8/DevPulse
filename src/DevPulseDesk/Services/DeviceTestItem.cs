using DevPulseApplication.Models;
using DevPulseApplication.Responses;

namespace DevPulseDesk.Services
{
    public enum TestState { Pending, Running, Success, Fail, Cancelled }

    public enum TestStage { Resolve, Ping, Port, Report }

    public enum StageState { Waiting, Active, Passed, Warning, Skipped, Failed }

    public enum LogLevel { Info, Success, Warning, Error, System }

    public enum TestEventKind { Started, Stage, Finished }

    /// <summary>Test motorundan UI'a akan olay. UI iş parçacığında <see cref="DeviceTestItem.Apply"/> ile uygulanır.</summary>
    public sealed record TestEvent(
        DeviceTestItem Item,
        TestEventKind Kind,
        TestStage Stage,
        StageState State,
        string Message,
        LogLevel Level,
        long? LatencyMs = null,
        TestResultResponse? Result = null);

    /// <summary>Bir PAG cihazının arayüzdeki test durumu.</summary>
    public sealed class DeviceTestItem
    {
        public static readonly int StageCount = Enum.GetValues<TestStage>().Length;

        public DeviceTestItem(PagDeviceModel source)
        {
            Source = source;
            Name = string.IsNullOrWhiteSpace(source.Name) ? source.Device?.ModelName ?? "Adsız cihaz" : source.Name;
            var brand = string.Join(" ", new[] { source.Device?.BrandName, source.Device?.ModelName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            Subtitle = string.Join("  •  ", new[] { brand, source.Pag?.PagName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (Subtitle.Length == 0) Subtitle = source.Device?.CategoryName ?? "Cihaz";
            Host = source.IpHost?.Trim() ?? "";
            Port = source.Port;

            var connection = $"{source.Device?.ConnectionTypeName} {source.Device?.CommunicationTypeName}";
            IsUdp = connection.Contains("udp", StringComparison.OrdinalIgnoreCase) || connection.Contains("snmp", StringComparison.OrdinalIgnoreCase);

            // Timeout değeri saniye veya milisaniye olarak gelebilir.
            var timeout = source.Timeout;
            TimeoutMs = timeout <= 0 ? 2000 : timeout <= 60 ? timeout * 1000 : timeout;
            TimeoutMs = Math.Clamp(TimeoutMs, 500, 10000);
        }

        public PagDeviceModel Source { get; }
        public string Name { get; }
        public string Subtitle { get; }
        public string Host { get; }
        public int Port { get; }
        public bool IsUdp { get; }
        public int TimeoutMs { get; }

        public TestState State { get; private set; } = TestState.Pending;
        public StageState[] Stages { get; } = new StageState[StageCount];
        public string StatusText { get; private set; } = "Test bekleniyor";
        public long? PingMs { get; private set; }
        public long? PortMs { get; private set; }
        public TestResultResponse? Result { get; private set; }

        public long? LatencyMs => PortMs ?? PingMs;

        public string Endpoint => Port > 0 ? $"{Host}:{Port}" : Host;

        public bool IsDone => State is TestState.Success or TestState.Fail;

        public void Reset()
        {
            State = TestState.Pending;
            Array.Fill(Stages, StageState.Waiting);
            StatusText = "Sırada bekliyor";
            PingMs = PortMs = null;
            Result = null;
        }

        public void Cancel()
        {
            if (State != TestState.Running && State != TestState.Pending) return;
            State = TestState.Cancelled;
            for (var i = 0; i < Stages.Length; i++)
            {
                if (Stages[i] == StageState.Active) Stages[i] = StageState.Waiting;
            }
            StatusText = "Test iptal edildi";
        }

        public void Apply(TestEvent e)
        {
            switch (e.Kind)
            {
                case TestEventKind.Started:
                    State = TestState.Running;
                    Array.Fill(Stages, StageState.Waiting);
                    break;
                case TestEventKind.Stage:
                    Stages[(int)e.Stage] = e.State;
                    if (e.LatencyMs is { } latency)
                    {
                        if (e.Stage == TestStage.Ping) PingMs = latency;
                        if (e.Stage == TestStage.Port) PortMs = latency;
                    }
                    break;
                case TestEventKind.Finished:
                    Result = e.Result;
                    State = e.Result?.Status == TestResult.Success ? TestState.Success : TestState.Fail;
                    break;
            }
            StatusText = e.Message;
        }
    }
}
