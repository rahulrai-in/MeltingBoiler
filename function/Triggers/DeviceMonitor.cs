using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace SafeguardFunction.Triggers
{
    public static class DeviceMonitor
    {
        private static ILogger _logger;

        [FunctionName(nameof(DeviceMonitor))]
        public static async Task Run([EntityTrigger] IDurableEntityContext context, ILogger logger)
        {
            _logger = logger;
            switch (context.OperationName)
            {
                case "add-record":
                    AddRecord(context);
                    break;

                case "is-melting":
                    context.Return(IsMelting(context));
                    break;

                case "send-instruction":
                    await SendInstructionAsync(context);
                    break;

                case "reset":
                    context.SetState(null);
                    break;

                default:
                    throw new NotSupportedException(context.OperationName);
            }
        }

        private static void AddRecord(IDurableEntityContext context)
        {
            var recordedValues = context.GetState<Queue<KeyValuePair<DateTime, double>>>() ??
                                 new Queue<KeyValuePair<DateTime, double>>();
            var temperature = context.GetInput<KeyValuePair<DateTime, double>>();
            recordedValues.Enqueue(temperature);
            while (recordedValues.Count > 5 && recordedValues.TryDequeue(out _))
            {
            }

            context.SetState(recordedValues);
        }

        private static bool IsMelting(IDurableEntityContext context)
        {
            var recordedValues = context.GetState<Queue<KeyValuePair<DateTime, double>>>() ??
                                 new Queue<KeyValuePair<DateTime, double>>();
            return recordedValues.Any(kvp => kvp.Value >= 1000) ||
                   recordedValues.Average(kvp => kvp.Value) > 800 && recordedValues.Count == 5;
        }

        private static async Task SendInstructionAsync(IDurableEntityContext context)
        {
            if (context.GetInput<bool>())
            {
                var serviceClient = ServiceClient.CreateFromConnectionString(Environment.GetEnvironmentVariable("DeviceConnectionString"));
                var cloudToDeviceMethod = new CloudToDeviceMethod("command");
                cloudToDeviceMethod.SetPayloadJson("shutdown");
                var response = await serviceClient.InvokeDeviceMethodAsync("myboilercontroller", cloudToDeviceMethod);
                var jsonResult = response.GetPayloadAsJson();
                _logger.LogInformation($"Device response: {jsonResult}");
                // https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-direct-methods
            }
        }
    }
}