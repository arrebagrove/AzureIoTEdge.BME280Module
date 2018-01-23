
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
namespace AzureIoTEdge.BME280Module
{
    /// Simple iot edge module state keeper
    /// Establishes connection to IoT hub
    public class IoTEdgeModuleState
    {
        public string ConnectionString { get; set; }
        public bool BypassCertVerification { get; set; }
        public DeviceClient ioTHubModuleClient { get; private set; }

        ITransportSettings[] CreateTransportSettings()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            // During dev you might want to bypass the cert verification. It is highly recommended to verify certs systematically in production
            if (BypassCertVerification)
            {
                mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            ITransportSettings[] settings = { mqttSetting };
            return settings;

        }

        public async Task<DeviceClient> CreateAndOpenClient()
        {
            if (ioTHubModuleClient == null)
            {
                try
                {
                    this.ioTHubModuleClient = DeviceClient.CreateFromConnectionString(this.ConnectionString, CreateTransportSettings());
                    Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Opening connection to {this.ConnectionString}...");
                    await ioTHubModuleClient.OpenAsync();
                    Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Connection opened!");
                    return ioTHubModuleClient;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Error connecting to IoT Hub\n{ex.ToString()}");
                }
            }

            return ioTHubModuleClient;
        }
    }
}