using DevPulseApplication.Models;
using DevPulseApplication.Responses;

namespace DevPulseDesk.Services
{
    public enum TestState { Pending, Running, Success, Fail, Cancelled }

    /// <summary>Bir PAG cihazının arayüzdeki test durumu.</summary>
    public sealed class DeviceTestItem
    {
        public static readonly int StageCount = Enum.GetValues<TestStage>().Length;

        public DeviceTestItem(PagDeviceModel source)
        {
            Source = source;
            Name = source.GetDisplayName();
            var brand = string.Join(" ", new[] { source.Device?.BrandName, source.Device?.ModelName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            Subtitle = string.Join("  •  ", new[] { brand, source.Pag?.PagName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (Subtitle.Length == 0) Subtitle = source.Device?.CategoryName ?? "Cihaz";
            Host = source.IpHost?.Trim() ?? "";
            Port = source.Port;

            IsUdp = source.IsUdpBased();
            InMaintenance = source.InMaintenance || source.Pag?.InMaintenance == true;
        }

        public PagDeviceModel Source { get; }
        public string Name { get; }
        public string Subtitle { get; }
        public string Host { get; }
        public int Port { get; }
        public bool IsUdp { get; }
        public bool InMaintenance { get; }

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

        public void Apply(DeviceTestProgress e)
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
