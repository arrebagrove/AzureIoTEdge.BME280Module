namespace AzureIoTEdge.MiddlewareModule
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
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    class Program
    {
        static DeviceThreshold threshold = new DeviceThreshold();
        static DateTime startupTime = DateTime.UtcNow;
        static string outputName;

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
                Logger.Error("Error in the initialization", ex);
                Environment.ExitCode = 1;
                return;
            }

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
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
                Logger.Log($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing path to certificate file.");
            }
            else if (!File.Exists(certPath))
            {
                // We cannot proceed further without a proper cert file
                Logger.Error($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing certificate file.");
            }
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate2.CreateFromCertFile(certPath)));
            Logger.Log("Added Cert: " + certPath);
            store.Close();
        }




        /// <summary>
        /// Initializes the DeviceClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task<IoTEdgeModuleState> Init(string connectionString, bool bypassCertVerification = false)
        {
            Logger.Log($"Connection String {connectionString}");

            var state = new IoTEdgeModuleState()
            {
                ConnectionString = connectionString,
                BypassCertVerification = bypassCertVerification,
            };


            // open connection to edge
            var ioTHubModuleClient = await state.CreateAndOpenClient();
            if (ioTHubModuleClient == null)
            {
                Logger.Error($"Could not connect to {state.ConnectionString}. Aborting");

            }
            Logger.Log($"IoT Hub module client initialized.");

            await ioTHubModuleClient.SetMethodHandlerAsync("middlewarestatus", HandleStatusMethod, null);
            Logger.Log($"Setup handing for method 'status'");

            // Attach callback for Twin desired properties updates
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, null);
            Logger.Log($"Setup desired property update handler");

            var twin = await ioTHubModuleClient.GetTwinAsync();
            if (twin.Properties?.Desired?.Count > 0)
            {
                Logger.Log($"Twin desired has {twin.Properties?.Desired?.Count ?? 0} properties");
                UpdateThreshold(twin.Properties.Desired);
            }

            // Resolve output name
            outputName = Environment.GetEnvironmentVariable("OutputName");
            if (string.IsNullOrEmpty(outputName))
            {
                outputName = "middlewareoutput";
            };
            Logger.Log($"IoT Hub module is outputting message '{outputName}'.");

            // Register callback to be called when a message is received by the module
            var inputName = Environment.GetEnvironmentVariable("InputName");
            if (string.IsNullOrEmpty(inputName))
            {
                inputName = "sensor";
            };
            await ioTHubModuleClient.SetInputMessageHandlerAsync(inputName, HandleInputMessages, ioTHubModuleClient);
            Logger.Log($"IoT Hub module is listening for input messages on '{inputName}'.");

                        

            return state;
        }


        // Handles input messages
        private static async Task<MessageResponse> HandleInputMessages(Message message, object userContext)
        {
            try
            {
                DeviceClient deviceClient = (DeviceClient)userContext;

                byte[] messageBytes = message.GetBytes();
                string messageString = Encoding.UTF8.GetString(messageBytes);
                Logger.Log($"Received message: {messageString}");

                // Get message body
                var measurement = JsonConvert.DeserializeObject<MeasurementMessage>(messageString);

                // we will send the message anyway, 
                // we just argument if we found that the message exceeds the threshold
                if (measurement != null)
                {
                    var hasThresold =  false;
                    var argumentedMessage = new Message(messageBytes);
                    if (threshold.MaxHumidity.HasValue && measurement.Humidity > threshold.MaxHumidity.Value)
                    {
                        argumentedMessage.Properties.Add("MaxHumidityThresold", measurement.Humidity.ToString());                        
                        Logger.Log($"{nameof(measurement.Humidity)} threshold detected: {measurement.Humidity}");
                        hasThresold = true;
                    }

                    if (threshold.MinHumidity.HasValue && measurement.Humidity < threshold.MinHumidity.Value)
                    {
                        argumentedMessage.Properties.Add("MaxHumidityThresold", measurement.Humidity.ToString());                        
                        Logger.Log($"{nameof(measurement.Humidity)} threshold detected: {measurement.Humidity}");
                        hasThresold = true;
                    }

                    if (threshold.MaxTemperature.HasValue && measurement.Temperature > threshold.MaxTemperature.Value)                    
                    {
                        argumentedMessage.Properties.Add("TemperatureThresold", measurement.Temperature.ToString());
                        Logger.Log($"{nameof(measurement.Temperature)} threshold detected: {measurement.Temperature}");
                        hasThresold = true;
                    }

                    if (threshold.MinTemperature.HasValue && measurement.Temperature < threshold.MinTemperature.Value)                    
                    {
                        argumentedMessage.Properties.Add("TemperatureThresold", measurement.Temperature.ToString());
                        Logger.Log($"{nameof(measurement.Temperature)} threshold detected: {measurement.Temperature}");
                        hasThresold = true;
                    }

                    if (threshold.MaxPressure.HasValue && measurement.Pressure > threshold.MaxPressure.Value)
                    {
                        argumentedMessage.Properties.Add("PressureThresold", measurement.Pressure.ToString());
                        Logger.Log($"{nameof(measurement.Temperature)} threshold detected: {measurement.Temperature}");
                        hasThresold = true;
                    }

                    if (threshold.MinPressure.HasValue && measurement.Pressure < threshold.MinPressure.Value)
                    {
                        argumentedMessage.Properties.Add("PressureThresold", measurement.Pressure.ToString());
                        Logger.Log($"{nameof(measurement.Temperature)} threshold detected: {measurement.Temperature}");
                        hasThresold = true;
                    }

                    // if any thresold add "alert" property to message to enable filtering in IoT Hub
                    if (hasThresold)
                        argumentedMessage.Properties.Add("Alert", "1");

                    // add the previously properties too
                     foreach (KeyValuePair<string, string> prop in message.Properties)
                        argumentedMessage.Properties.Add(prop.Key, prop.Value);            

                    await deviceClient.SendEventAsync(outputName, argumentedMessage);   
                    Logger.Log($"Message sent to '{outputName}': HasThreshold: {hasThresold}");                                 
                }

                // Indicate that the message treatment is completed
                return MessageResponse.Completed;
            }
            catch (AggregateException ex)
            {
                Logger.Error("Error in sample", ex);                
                return MessageResponse.Abandoned;
            }
            catch (Exception ex)
            {
                Logger.Error("Error in sample", ex);
                // Indicate that the message treatment is not completed
                DeviceClient deviceClient = (DeviceClient)userContext;
                return MessageResponse.Abandoned;
            }
        }


        static double? TryGetDesiredPropertyDouble(TwinCollection desiredProperties, string propertyName)
        {
            if (desiredProperties.Contains(propertyName))
            {
                var raw = desiredProperties[propertyName]?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(raw))
                {
                    if (double.TryParse(raw,  out double value))
                    {
                        return value;                            
                    }
                }
            }

            return null;
        }

        // Updates the current threshold values from the desired properties
        static void UpdateThreshold(TwinCollection desiredProperties)
        {
            try
            {
                var updatedThreshold = new DeviceThreshold(threshold);
                
                
                var maxTemperatureThreshold = TryGetDesiredPropertyDouble(desiredProperties, "MaxTemperatureThreshold");
                if (maxTemperatureThreshold.HasValue && maxTemperatureThreshold.Value > 0d)
                {
                    updatedThreshold.MaxTemperature = maxTemperatureThreshold;
                    Logger.Log($"Threshold Update {nameof(updatedThreshold.MaxTemperature)}: {updatedThreshold.MaxTemperature}");
                }

                var minTemperatureThreshold = TryGetDesiredPropertyDouble(desiredProperties, "MinTemperatureThreshold");
                if (minTemperatureThreshold.HasValue)
                {
                    updatedThreshold.MinTemperature = minTemperatureThreshold;
                    Logger.Log($"Threshold Update {nameof(updatedThreshold.MinTemperature)}: {updatedThreshold.MinTemperature}");
                }

                var maxHumidityThreshold = TryGetDesiredPropertyDouble(desiredProperties, "MaxHumidityThreshold");
                if (maxHumidityThreshold.HasValue && maxHumidityThreshold.Value > 0d)
                {
                    updatedThreshold.MaxHumidity = maxHumidityThreshold;
                    Logger.Log($"Threshold Update {nameof(updatedThreshold.MaxHumidity)}: {updatedThreshold.MaxHumidity}");
                }

                var minHumidityThreshold = TryGetDesiredPropertyDouble(desiredProperties, "MinHumidityThreshold");
                if (minHumidityThreshold.HasValue)
                {
                    updatedThreshold.MinHumidity = minHumidityThreshold;
                    Logger.Log($"Threshold Update {nameof(updatedThreshold.MinHumidity)}: {updatedThreshold.MinHumidity}");
                }

                var maxPressureThreshold = TryGetDesiredPropertyDouble(desiredProperties, "MaxPressureThreshold");
                if (maxPressureThreshold.HasValue && maxPressureThreshold.Value > 0d)
                {
                    updatedThreshold.MaxPressure = maxPressureThreshold;
                    Logger.Log($"Threshold Update {nameof(updatedThreshold.MaxPressure)}: {updatedThreshold.MaxPressure}");
                }               

                var minPressureThreshold = TryGetDesiredPropertyDouble(desiredProperties, "MaxPressureThreshold");
                if (minPressureThreshold.HasValue)
                {
                    updatedThreshold.MinPressure = minPressureThreshold;
                    Logger.Log($"Threshold Update {nameof(updatedThreshold.MinPressure)}: {updatedThreshold.MinPressure}");
                }     

                threshold = updatedThreshold;

            }
            catch (Exception ex)
            {
                Logger.Error("Failed getting desired properties", ex);
            }
        }

        private static Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            Logger.Log("Desired properties changed");
            UpdateThreshold(desiredProperties);           
            return Task.CompletedTask;
        }

        private static Task<MethodResponse> HandleStatusMethod(MethodRequest methodRequest, object userContext)
        {
            Logger.Log($"Status method called");


            var responseBody = new Dictionary<string, object>()
            {
                { "startTime", startupTime },
                { "uptimeSeconds", DateTime.UtcNow.Subtract(startupTime).TotalSeconds },
            };


            var jsonResponseBody = JsonConvert.SerializeObject(responseBody);

            var res = new MethodResponse(Encoding.UTF8.GetBytes(jsonResponseBody), 200);

            Logger.Log($"Status method returned: {jsonResponseBody}");

            return Task.FromResult(res);
        }
    }

}
