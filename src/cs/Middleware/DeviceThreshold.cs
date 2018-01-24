namespace AzureIoTEdge.MiddlewareModule
{
    public class DeviceThreshold
    {
        public DeviceThreshold()
        {
        }

        public DeviceThreshold(DeviceThreshold other)
        {
            this.MaxHumidity = other.MaxHumidity;
            this.MinHumidity = other.MinHumidity;

            this.MaxPressure = other.MaxPressure;
            this.MinPressure = other.MinPressure;
            
            this.MaxTemperature = other.MaxTemperature;
            this.MinTemperature = other.MinTemperature;
        }

        public double? MaxTemperature { get; set; }
        public double? MinTemperature { get; set; }

        public double? MaxPressure { get; set; }
        public double? MinPressure { get; set; }
        public double? MaxHumidity { get; set; }
        public double? MinHumidity { get; set; }
    }

}