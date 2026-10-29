using DevPulseApplication.HttpClients.Services.Base;
using DevPulseApplication.HttpClients.Settings;
using DevPulseApplication.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DevPulseApplication.HttpClients.Services
{
    public class DeviceHttpClientService : IDeviceHttpClientService
    {
        private readonly HttpClient _httpClient;
        private readonly DevPulseHttpClientSettings _settings;
        public string BaseUrl { get; set; } // dışarıdan girilebilir bir baseAddress için kullanılır.
        public string Token { get; set; } // login işleminden sonra buraya atama yapılmalı.

        public DeviceHttpClientService(IOptions<DevPulseHttpClientSettings> settings)
        {
            _httpClient = new HttpClient();
            _settings = settings.Value;
        }

        public async Task<List<Device>> GetDeviceById(Guid id)
        {
            var device = await _httpClient.GetAsync($"{BaseUrl}/{_settings.GetDeviceByIdUrl}/{id}");
        }

        public async Task<List<PagDeviceModel>> GetPagDevices()
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);

            var pagDevices = await _httpClient.GetAsync($"{BaseUrl}/{_settings.GetPagDevicesUrl}");

            if (pagDevices?.StatusCode != HttpStatusCode.OK) return null;

            var response = JsonConvert.DeserializeObject<List<PagDeviceModel>>(await pagDevices.Content.ReadAsStringAsync());

            return response;
        }
    }
}