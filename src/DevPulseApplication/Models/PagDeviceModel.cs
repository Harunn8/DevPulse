using Newtonsoft.Json;

namespace DevPulseApplication.Models
{
    public class PagDeviceModel
    {
        public Guid Id { get; set; }
        public Guid PagId { get; set; }
        public Guid DeviceId { get; set; }
        public string Name { get; set; }
        public bool IsView { get; set; }
        public bool IsControl { get; set; }
        public bool IsShowAlarm { get; set; }
        public string IpHost { get; set; }
        public int Port { get; set; }
        public string WebUrl { get; set; }
        public int Timeout { get; set; }
        public int QueryInterval { get; set; }
        public bool InMaintenance { get; set; }
        public bool IsOnlyChangedData { get; set; }
        public int QueryRepetition { get; set; }
        public string Notes { get; set; }

        public Device Device { get; set; }
        public Pag Pag { get; set; }
    }

    public class Device
    {
        public Guid Id { get; set; }
        public string BrandName { get; set; }
        public string ModelName { get; set; }
        public string CategoryName { get; set; }
        public string Version { get; set; }
        public string ConnectionTypeName { get; set; }
        public string CommunicationTypeName { get; set; }
        public string VersionNote { get; set; }
        public DateTime UpdateDate { get; set; }
        public bool IsDeleted { get; set; }
        public CommunicationData CommunicationData { get; set; }
    }

    public class CommunicationData
    {
        public List<object> Commands { get; set; } = new();
        public List<Query> Queries { get; set; } = new();
        public string EncodingType { get; set; }
        public string FormatType { get; set; }
        public int PreambleDelay { get; set; }
        public bool IsCarriageReturn { get; set; }
        public bool IsLineFeed { get; set; }
        public List<Parameter> Parameters { get; set; } = new();
    }

    public class Query
    {
        public Guid Id { get; set; }

        [JsonProperty("query")]
        public string QueryText { get; set; }
        public string Parser { get; set; }
        public bool IsActive { get; set; }
        public bool IsOptional { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class Parameter
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class Pag
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public string PagName { get; set; }
        public Guid StationId { get; set; }
        public string PagIP { get; set; }
        public int? Port { get; set; }
        public int? Timeout { get; set; }
        public int? RefreshTime { get; set; }
        public double? PagLatitude { get; set; }
        public double? PagLongitude { get; set; }
        public bool InMaintenance { get; set; }
        public DateTime UpdateDate { get; set; }
        public bool IsDeleted { get; set; }
    }
}