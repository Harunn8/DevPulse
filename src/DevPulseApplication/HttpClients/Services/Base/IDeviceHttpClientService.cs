using DevPulseApplication.Models;

namespace DevPulseApplication.HttpClients.Services.Base
{
    public interface IDeviceHttpClientService
    {
        string BaseUrl { get; set; }
        string Token { get; set; }
        Task<List<PagDeviceModel>> GetPagDevices();
        Task<List<Device>> GetDeviceById(Guid id);
    }
}