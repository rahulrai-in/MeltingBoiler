using MeltingBoiler.SafeguardFunction.Orchestrators;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace MeltingBoiler.SafeguardFunction.Clients
{
    public static class IoTHubClient
    {
        [FunctionName(nameof(IoTHubClient))]
        //public static async Task RunClient([IoTHubTrigger("messages/events", Connection = "")]EventData message, [OrchestrationClient] DurableOrchestrationClient starter, string functionName, ILogger log)
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "start/{temperature}/{deviceId}")]
            HttpRequestMessage request,
            [OrchestrationClient] IDurableOrchestrationClient client,
            double temperature,
            string deviceId,
            ILogger logger)
        {
            var instanceId = await client.StartNewAsync(nameof(SafetySequenceOrchestrator),
                new KeyValuePair<string, double>(deviceId, temperature));
            logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(request, instanceId);
        }
    }
}