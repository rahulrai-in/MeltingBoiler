using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SafeguardFunction.Core;

namespace SafeguardFunction.Clients
{
    public static class ManualRequestApproval
    {
        [FunctionName(nameof(ManualRequestApproval))]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, Constants.Post, Route = Constants.ManualApprovalRoute)]
            HttpRequestMessage request,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger logger)
        {
            var formData = await request.Content.ReadAsFormDataAsync();
            var payload = formData.Get("payload");
            dynamic response = JsonConvert.DeserializeObject(payload);
            string instanceId = response.callback_id;
            await client.RaiseEventAsync(instanceId, Constants.ManualApproval,
                Convert.ToBoolean(response.actions[0].value));
            logger.LogInformation("Raised Manual Approval event for {InstanceId} with value {Value}", instanceId,
                Convert.ToBoolean((string) response.actions[0].value));
        }
    }
}