using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace SafeguardFunction.Triggers
{
    public static class SendApprovalRequest
    {

        [FunctionName(nameof(SendApprovalRequest))]
        public static async Task Run([ActivityTrigger]string instanceId, ILogger logger)
        {
            // Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            string approvalRequestUrl = Environment.GetEnvironmentVariable("Slack:ApprovalUrl", EnvironmentVariableTarget.Process);
            // string approvalMessageTemplate = Environment.GetEnvironmentVariable("Slack:ApprovalMessageTemplate", EnvironmentVariableTarget.Process);
            string approvalMessageTemplate = "{\"text\":\"*Alert!!* Simulated Sensor is reporting critical temperatures.\",\"attachments\":[{\"text\":\"Shut down *Boiler1*?\",\"fallback\":\"You are unable to choose an option\",\"callback_id\":\"" + instanceId + "\",\"color\":\"#3AA3E3\",\"attachment_type\":\"default\",\"actions\":[{\"name\":\"approve\",\"text\":\"YES\",\"type\":\"button\",\"value\":\"true\"},{\"name\":\"approve\",\"text\":\"NO\",\"type\":\"button\",\"value\":\"false\"}]}]}";
            string approvalMessage = approvalMessageTemplate;
            string resultContent;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(approvalRequestUrl);
                var content = new StringContent(approvalMessage, UnicodeEncoding.UTF8, "application/json");
                var result = await client.PostAsync(approvalRequestUrl, content);
                resultContent = await result.Content.ReadAsStringAsync();
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    throw new HttpRequestException(resultContent);
                }
            }

            logger.LogInformation($"Message sent to Slack!");
        }
    }
}