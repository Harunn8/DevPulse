using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevPulseApplication.Responses
{
    /// <summary>API'nin tüm yanıtları sardığı zarf: { "status": 200, "message": null, "result": { ... } }</summary>
    public class ApiResponse<T>
    {
        public int Status { get; set; }
        public string? Message { get; set; }
        public T? Result { get; set; }
    }

    public static class ApiResponseReader
    {
        /// <summary>
        /// Yanıt zarflıysa "result" alanını, zarfsız geliyorsa doğrudan gövdeyi <typeparamref name="T"/> olarak çözer.
        /// </summary>
        public static T? Read<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;

            var token = JToken.Parse(json);
            if (token is JObject obj && obj.TryGetValue("result", StringComparison.OrdinalIgnoreCase, out var result))
            {
                return result.Type == JTokenType.Null ? default : result.ToObject<T>(JsonSerializer.CreateDefault());
            }
            return token.ToObject<T>(JsonSerializer.CreateDefault());
        }
    }
}
