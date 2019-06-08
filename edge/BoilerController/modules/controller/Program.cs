namespace controller
{
    using System;
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

    class Program
    {
        static int minTemperature = 100;
        static int maxTemperature = 700;
        static bool isReset = true;

        static void Main(string[] args)
        {
            Init().Wait();

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
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetInputMessageHandlerAsync("command", CommandHandler, ioTHubModuleClient);
            await PublishMessages(ioTHubModuleClient);
        }

        /// <summary>
        /// Handles command sent to edge device.
        /// </summary>
        /// <param name="message">Message sent as command.</param>
        /// <param name="userContext">Module client.</param>
        /// <returns></returns>
        private static async Task<MessageResponse> CommandHandler(Message message, object userContext)
        {
            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException(nameof(userContext));
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            switch (messageString.ToLowerInvariant())
            {
                case "normal":
                    minTemperature = 100;
                    maxTemperature = 700;
                    break;
                case "critical":
                    minTemperature = 800;
                    maxTemperature = 900;
                    break;
                case "melt":
                    minTemperature = 1000;
                    maxTemperature = 1500;
                    break;
                case "shutdown":
                    minTemperature = 0;
                    maxTemperature = 20;
                    break;
            }

            isReset = true;
            return await Task.FromResult(MessageResponse.Completed);
        }

        /// <summary>
        /// Publish message within min and max range.
        /// </summary>
        /// <param name="userContext">The Module Client.</param>
        static async Task PublishMessages(ModuleClient moduleClient)
        {
            if (moduleClient == null)
            {
                throw new InvalidOperationException(nameof(moduleClient));
            }

            var counter = minTemperature;
            while (true)
            {
                while (counter < maxTemperature && !isReset)
                {
                    counter += 10;
                    await SendMessage();
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                while (counter > minTemperature && !isReset)
                {
                    counter -= 10;
                    await SendMessage();
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                if (isReset)
                {
                    counter = minTemperature;
                    isReset = false;
                }
            }

            async Task SendMessage()
            {
                var temperatureValue = new { CurrentTemperature = counter };
                var message = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(temperatureValue)));
                message.Properties.Add("Time", DateTime.UtcNow.Ticks.ToString());
                await moduleClient.SendEventAsync("output1", message);
                Console.WriteLine($"Sent Message: {JsonConvert.SerializeObject(temperatureValue)}");
            }
        }
    }
}
