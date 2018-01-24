namespace AzureIoTEdge.BME280Module
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
        
    class Program
    {
        static SensorReader sensorReader;
        static void Main(string[] args)
        {
            // The Edge runtime gives us the connection string we need -- it is injected as an environment variable
            string connectionString = Environment.GetEnvironmentVariable("EdgeHubConnectionString");

            // Cert verification is not yet fully functional when using Windows OS for the container
            bool bypassCertVerification = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (!bypassCertVerification) InstallCert();

            IoTEdgeModuleState state = null;
            try
            {
                state = Init(connectionString, bypassCertVerification).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Error in the initialization\n{ex.ToString()}");
                Environment.ExitCode = 1;
                return;
            }


            var outputName = Environment.GetEnvironmentVariable("OutputName");
            if (string.IsNullOrEmpty(outputName))
            {
                outputName = "sensor";
            };

            using (sensorReader = new SensorReader(state, outputName))
            {

                sensorReader.StartReading();

                // Wait until the app unloads or is cancelled
                var cts = new CancellationTokenSource();
                AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
                Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
                WhenCancelled(cts.Token).Wait();
            }
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Add certificate in local cert store for use by client for secure connection to IoT Edge runtime
        /// </summary>
        static void InstallCert()
        {
            string certPath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
            if (string.IsNullOrWhiteSpace(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing path to certificate file.");
            }
            else if (!File.Exists(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing certificate file.");
            }
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate2.CreateFromCertFile(certPath)));
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Added Cert: " + certPath);
            store.Close();
        }

        static DateTime startupTime = DateTime.UtcNow;


        /// <summary>
        /// Initializes the DeviceClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task<IoTEdgeModuleState> Init(string connectionString, bool bypassCertVerification = false)
        {
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Connection String {connectionString}");

            var state = new IoTEdgeModuleState()
            {
                ConnectionString = connectionString,
                BypassCertVerification = bypassCertVerification,
            };


            // open connection to edge
            var ioTHubModuleClient = await state.CreateAndOpenClient();
            if (ioTHubModuleClient == null)
            {
                Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Could not connect to {state.ConnectionString}. Aborting");

            }
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] IoT Hub module client initialized.");            

            await ioTHubModuleClient.SetMethodHandlerAsync("bme280status", HandleStatusMethod, null);
            return state;
        }

        private static Task<MethodResponse> HandleStatusMethod(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Status method called");

            var (measurement, _) = sensorReader.GetMeasurements();
            
            var responseBody = new Dictionary<string, object>()
            {
                { "startTime", startupTime },
                { "uptimeSeconds", DateTime.UtcNow.Subtract(startupTime).TotalSeconds },
                { "device", measurement.Device },
                { "temp", measurement.Temperature },
                { "humidity", measurement.Humidity },
                { "pressure", measurement.Pressure }
            };


            var jsonResponseBody = JsonConvert.SerializeObject(responseBody);

            var res = new MethodResponse(Encoding.UTF8.GetBytes(jsonResponseBody), 200);

            Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Status method returned: {jsonResponseBody}");                
            
            return Task.FromResult(res);
        }
    }
}
