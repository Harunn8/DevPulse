using DevPulseApplication.Models;
using DevPulseApplication.Responses;

namespace DevPulseApplication.HttpClients.Services.Base
{
    public interface ILoginHttpClientService
    {
        Task<LoginResponse> LoginAsync(LoginModel loginModel); // Diğer isteklerden token almak için ve aynı zamanda, sadece Visionic'de tanımlı olan kullanıcıların kullanması için yazılmıştır.
    }
}