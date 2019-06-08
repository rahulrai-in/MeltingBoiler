using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SafeguardFunction.Orchestrators;
using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;

namespace SafeguardFunction.Clients
{
    public static class IoTHubClient
    {
        [FunctionName(nameof(IoTHubClient))]
        public static async Task RunClient(
            [IoTHubTrigger("messages/events", Connection = "IoTHubTriggerConnection")]
            EventData message,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger logger)
        {
            var messageResult = JsonConvert.DeserializeObject<dynamic>(Encoding.ASCII.GetString(message.Body.Array));
            var instanceId = await client.StartNewAsync(nameof(SafetySequenceOrchestrator),
                new KeyValuePair<string, double>("myboilercontroller", (double)messageResult.CurrentTemperature));
            logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }
    }
}