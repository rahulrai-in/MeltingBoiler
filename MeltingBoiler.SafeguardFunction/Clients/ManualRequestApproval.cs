using MeltingBoiler.SafeguardFunction.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MeltingBoiler.SafeguardFunction.Clients
{
    public static class ManualRequestApproval
    {
        [FunctionName(nameof(ManualRequestApproval))]
        public static async Task Run(
            [HttpTrigger(Constants.Post, Route = Constants.ManualApprovalRoute)]
            HttpRequest request,
            [OrchestrationClient] IDurableOrchestrationClient client, string instanceId, ILogger logger)
        {
            var content = await new StreamReader(request.Body).ReadToEndAsync();
            await client.RaiseEventAsync(instanceId, Constants.ManualApproval, Convert.ToBoolean(content));
            logger.LogInformation("Raised Manual Approval event for {InstanceId} with value {Value}", instanceId,
                Convert.ToBoolean(content));
        }
    }
}