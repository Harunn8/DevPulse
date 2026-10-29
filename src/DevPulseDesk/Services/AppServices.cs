using System.Text.Json;
using DevPulseApplication.HttpClients.Services;
using DevPulseApplication.HttpClients.Services.Base;
using DevPulseApplication.HttpClients.Settings;
using DevPulseApplication.Models;
using DevPulseApplication.Services;
using DevPulseApplication.Services.Base;
using Microsoft.Extensions.Options;

namespace DevPulseDesk.Services
{
    /// <summary>
    /// appsettings.json'dan okunan ayarlar ve DevPulseApplication servislerinin oluşturulması.
    /// </summary>
    internal sealed class AppServices
    {
        private AppServices(DevPulseHttpClientSettings settings, string defaultServerUrl, int parallelism, int minStageMs)
        {
            Settings = settings;
            DefaultServerUrl = defaultServerUrl;
            Parallelism = parallelism;
            MinStageDurationMs = minStageMs;
        }

        public DevPulseHttpClientSettings Settings { get; }
        public string DefaultServerUrl { get; }
        public int Parallelism { get; }
        public int MinStageDurationMs { get; }

        public static AppServices Load()
        {
            var settings = new DevPulseHttpClientSettings
            {
                LoginUrl = "api/auth/login",
                GetPagDevicesUrl = "api/pagdevices",
                GetPagDeviceByDeviceIdUrl = "api/pagdevices/device",
                GetDeviceByIdUrl = "api/devices"
            };
            var serverUrl = "http://localhost:5000";
            var parallelism = 4;
            var minStage = 350;

            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
                    var root = doc.RootElement;
                    if (root.TryGetProperty("DefaultServerUrl", out var url)) serverUrl = url.GetString() ?? serverUrl;
                    if (root.TryGetProperty(nameof(DevPulseHttpClientSettings), out var section))
                    {
                        settings = section.Deserialize<DevPulseHttpClientSettings>() ?? settings;
                    }
                    if (root.TryGetProperty("TestRunner", out var runner))
                    {
                        if (runner.TryGetProperty("Parallelism", out var p)) parallelism = Math.Clamp(p.GetInt32(), 1, 32);
                        if (runner.TryGetProperty("MinStageDurationMs", out var m)) minStage = Math.Clamp(m.GetInt32(), 0, 3000);
                    }
                }
                catch (JsonException)
                {
                    // Bozuk ayar dosyasında varsayılanlarla devam edilir.
                }
            }

            return new AppServices(settings, serverUrl, parallelism, minStage);
        }

        public ILoginHttpClientService CreateLoginService(string baseUrl) =>
            new LoginHttpClientService(Options.Create(Settings)) { BaseUrl = baseUrl };

        public IDeviceHttpClientService CreateDeviceService(Session session) =>
            new DeviceHttpClientService(Options.Create(Settings)) { BaseUrl = session.BaseUrl, Token = session.Token };

        public IDeviceTestService CreateTestService(Session session)
        {
            var options = Options.Create(new DeviceTestOptions { Parallelism = Parallelism, MinStageDurationMs = MinStageDurationMs });
            return session.IsDemo ? new DemoDeviceTestService(options) : new DeviceTestService(options);
        }

        public static string NormalizeUrl(string url)
        {
            url = url.Trim().TrimEnd('/');
            if (url.Length > 0 && !url.Contains("://")) url = "http://" + url;
            return url;
        }
    }

    internal sealed record Session(string UserName, Guid UserId, string Token, string BaseUrl, bool IsDemo)
    {
        public DateTime LoginTime { get; } = DateTime.Now;
    }

    /// <summary>"Beni hatırla" tercihleri (%AppData%\DevPulse\preferences.json). Şifre asla saklanmaz.</summary>
    internal sealed class UserPreferences
    {
        private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevPulse", "preferences.json");

        public string? ServerUrl { get; set; }
        public string? UserName { get; set; }
        public bool RememberMe { get; set; }

        public static UserPreferences Load()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(FilePath)) ?? new();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
            }
            return new();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
