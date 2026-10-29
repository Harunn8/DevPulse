using DevPulseApplication.HttpClients.Services.Base;
using DevPulseApplication.HttpClients.Settings;
using DevPulseApplication.Models;
using DevPulseApplication.Responses;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Json;

namespace DevPulseApplication.HttpClients.Services
{
    public class LoginHttpClientService : ILoginHttpClientService
    {
        private readonly HttpClient _httpClient;
        private readonly DevPulseHttpClientSettings _settings;
        public string BaseUrl {  get; set; } // dışarıdan girilebilir bir baseAddress için kullanılır.

        public LoginHttpClientService(IOptions<DevPulseHttpClientSettings> settings)
        {
            _httpClient = new HttpClient();
            _settings = settings.Value;
        }

        public async Task<LoginResponse> LoginAsync(LoginModel loginModel)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/{_settings.LoginUrl}", loginModel);

            if (response?.StatusCode != System.Net.HttpStatusCode.OK) return null;

            var userResult = ApiResponseReader.Read<LoginResponse>(await response.Content.ReadAsStringAsync()); // yanıt { status, message, result } zarfı ile gelir

            return userResult;
        }
    }
}