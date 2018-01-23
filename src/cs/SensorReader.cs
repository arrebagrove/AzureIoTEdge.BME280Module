using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;

namespace AzureIoTEdge.BME280Module
{

    public class SensorReader : IDisposable
    {
        IoTEdgeModuleState state;
        private Timer timer;

        public SensorReader(IoTEdgeModuleState state)
        {
            this.state = state;
        }


        public void StartReading(int intervalInSeconds = 60)
        {
            this.timer = new Timer(async (s) => await ReadAndSend(s), this, 0, 1000 * intervalInSeconds);
        }

        static string GetMeasurementsFromPython()
        {
            var psi = new ProcessStartInfo("python", $"../Adafruit_Python_BME280/jsonoutput.py")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };

            var process = Process.Start(psi);

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }

        static async Task ReadAndSend(object state)
        {
            var sensorReader = (SensorReader)state;
            try
            {
                var messageText = GetMeasurementsFromPython()?.Replace(Environment.NewLine.ToString(), string.Empty);

                var client = await sensorReader.state.CreateAndOpenClient();
                var m = new Message(System.Text.Encoding.UTF8.GetBytes(messageText));

                await client.SendEventAsync(m);

                Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Message sent to IoT Hub: {messageText}");
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