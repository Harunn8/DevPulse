using DevPulseApplication.Models;
using DevPulseApplication.Responses;

namespace DevPulseApplication.Services.Base
{
    public interface IDeviceTestService
    {
        Task<List<TestResultResponse>> RunTestsAsync(IReadOnlyList<PagDeviceModel> devices, IProgress<DeviceTestProgress>? progress = null, CancellationToken cancellationToken = default); // cihazları paralel test eder
        Task<TestResultResponse> TestDeviceAsync(PagDeviceModel device, IProgress<DeviceTestProgress>? progress = null, CancellationToken cancellationToken = default); // adres çözümleme → ping → TCP port → rapor
    }
}
