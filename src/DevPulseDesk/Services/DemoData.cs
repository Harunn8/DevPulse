using DevPulseApplication.Models;

namespace DevPulseDesk.Services
{
    /// <summary>Sunucu olmadan arayüzü keşfetmek için örnek PAG cihaz listesi.</summary>
    internal static class DemoData
    {
        private static readonly (string Name, string Brand, string Model, string Category, string Connection, int Port)[] Templates =
        [
            ("Trafo Merkezi RTU", "Schneider", "Saitel DR", "RTU", "TCP", 2404),
            ("Pompa İstasyonu PLC", "Siemens", "S7-1500", "PLC", "TCP", 102),
            ("Ana Dağıtım UPS", "APC", "Smart-UPS SRT", "UPS", "SNMP", 161),
            ("Enerji Analizörü", "Janitza", "UMG 604", "Analizör", "TCP", 502),
            ("Güvenlik NVR", "Hikvision", "DS-7616NI", "Kamera", "TCP", 554),
            ("Saha Switch", "Moxa", "EDS-510E", "Network", "SNMP", 161),
            ("Klima Kontrol Ünitesi", "Carel", "pCO5+", "HVAC", "TCP", 502),
            ("Hücresel Router", "Teltonika", "RUT955", "Network", "TCP", 443),
            ("Jeneratör Kontrolü", "ComAp", "InteliGen", "Jeneratör", "TCP", 23),
            ("Seviye Sensörü Ağ Geçidi", "Endress+Hauser", "Fieldgate", "Sensör", "TCP", 80),
            ("Koruma Rölesi", "ABB", "REF615", "Röle", "TCP", 102),
            ("OPC UA Sunucusu", "Kepware", "KEPServerEX", "Sunucu", "TCP", 4840),
            ("Akıllı Sayaç", "Landis+Gyr", "E650", "Sayaç", "TCP", 4059),
            ("Yangın Paneli", "Bosch", "AVENAR 2000", "Güvenlik", "TCP", 8080),
            ("Kesintisiz Güç PDU", "Eaton", "ePDU G3", "PDU", "SNMP", 161),
            ("Frekans İnvertörü", "Danfoss", "VLT FC-302", "Sürücü", "TCP", 502)
        ];

        private static readonly string[] Stations = ["Merkez Kampüs", "Kuzey Pompa İst.", "Liman Trafo", "Batı Depo"];

        public static async Task<List<PagDeviceModel>> GetDevicesAsync()
        {
            await Task.Delay(1400); // yükleme animasyonunu göstermek için
            var random = new Random(42);
            return Templates.Select((t, i) => new PagDeviceModel
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                Name = t.Name,
                IpHost = $"10.20.{1 + i / 6}.{10 + i * 7 % 200}",
                Port = t.Port,
                Timeout = 2,
                Device = new Device
                {
                    BrandName = t.Brand,
                    ModelName = t.Model,
                    CategoryName = t.Category,
                    ConnectionTypeName = t.Connection,
                    CommunicationTypeName = t.Connection
                },
                Pag = new Pag { PagName = Stations[random.Next(Stations.Length)] }
            }).ToList();
        }
    }
}
