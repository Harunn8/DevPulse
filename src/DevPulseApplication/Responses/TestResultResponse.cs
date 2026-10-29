namespace DevPulseApplication.Responses
{
    public class TestResultResponse : DeviceResponse
    {
        public TestResult Status {  get; set; }
    }

    public enum TestResult
    {
        Success,
        Fail 
    }
}