using Newtonsoft.Json;

namespace AzureIoTEdge.MiddlewareModule
{
    // Sensor measurement message
    public class MeasurementMessage
    {
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("device")]
        public string Device { get; set; }

        [JsonProperty("temperature")]
        public double Temperature { get; set; }

        [JsonProperty("pressure")]
        public double Pressure { get; set; }

        [JsonProperty("humidity")]
        public double Humidity { get; set; }
    }
}