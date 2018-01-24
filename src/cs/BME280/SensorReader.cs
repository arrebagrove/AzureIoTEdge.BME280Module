using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

namespace AzureIoTEdge.BME280Module
{

    /// Sensor reader
    public class SensorReader : IDisposable
    {
        IoTEdgeModuleState state;
        private readonly string outputName;
        private Timer timer;

        public SensorReader(IoTEdgeModuleState state, string outputName)
        {
            this.state = state;
            this.outputName = outputName;
        }


        public void StartReading(int intervalInSeconds = 60)
        {
            this.timer = new Timer(async (s) => await ReadAndSend(s), this, 0, 1000 * intervalInSeconds);
        }

        internal (MeasurementMessage, string) GetMeasurements()
        {
            var psi = new ProcessStartInfo("python", $"../Adafruit_Python_BME280/jsonoutput.py")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };

            var process = Process.Start(psi);

            string rawMeasurement = process.StandardOutput.ReadToEnd();            
            process.WaitForExit();

            MeasurementMessage result = null;
            if (!string.IsNullOrEmpty(rawMeasurement))
            {
                rawMeasurement = rawMeasurement.Replace(Environment.NewLine.ToString(), string.Empty);
                result = JsonConvert.DeserializeObject<MeasurementMessage>(rawMeasurement);
            }
            
            return (result, rawMeasurement);
        }

        static async Task ReadAndSend(object state)
        {
            var sensorReader = (SensorReader)state;
            try
            {
                var (measurements, rawMeasurement) = sensorReader.GetMeasurements();

                var client = await sensorReader.state.CreateAndOpenClient();
                
                var eventMessage = new Message(System.Text.Encoding.UTF8.GetBytes(rawMeasurement));
            
                await client.SendEventAsync(sensorReader.outputName, eventMessage);

                Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Event sent to sink '{sensorReader.outputName}': {rawMeasurement}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Error in message send: \n{ex.ToString()}");
            }
        }

        bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                this.timer?.Dispose();
            }

            disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        ~SensorReader()
        {
            Dispose(false);
        }
    }

}