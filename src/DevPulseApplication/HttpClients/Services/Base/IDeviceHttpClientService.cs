using DevPulseApplication.Models;

namespace DevPulseApplication.HttpClients.Services.Base
{
    public interface IDeviceHttpClientService
    {
        Task<List<PagDeviceModel>> GetPagDevices();
        Task<List<Device>> GetDeviceById(Guid id);
    }
}