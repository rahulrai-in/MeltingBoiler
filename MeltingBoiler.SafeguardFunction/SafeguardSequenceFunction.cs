using Microsoft.Azure.Devices;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MeltingBoiler.SafeguardFunction
{
    public static class SafeguardSequenceFunction
    {
        private const string TimeoutQueryStringKey = "timeout";
        /* Notes
        1. send reading to aggregator
        2. request interventio
        3. shut down boiler
       https://github.com/marcduiker/demos-azure-durable-functions
         */

        private static HttpClient client = new HttpClient();

        private static TimeSpan GetTimeSpan(HttpRequestMessage request, string queryParameterName)
        {
            var queryParameterStringValue = request.RequestUri.ParseQueryString()[queryParameterName];
            if (string.IsNullOrEmpty(queryParameterStringValue))
            {
                return TimeSpan.Zero;
            }

            return TimeSpan.FromSeconds(double.Parse(queryParameterStringValue));
        }


        [FunctionName("FunctionClient")]
        //public static async Task RunClient([IoTHubTrigger("messages/events", Connection = "")]EventData message, [OrchestrationClient] DurableOrchestrationClient starter, string functionName, ILogger log)
        public static async Task<HttpResponseMessage> RecordTemperature(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "start/{temperature}/{deviceId}")]
            HttpRequestMessage request,
            [OrchestrationClient] IDurableOrchestrationClient client,
            double temperature,
            string deviceId,
            ILogger logger)
        {
            // Function input comes from the request content.
            var instanceId = await client.StartNewAsync(nameof(Orchestrator),
                new KeyValuePair<string, double>(deviceId, temperature));
            var timeoutTime = GetTimeSpan(request, TimeoutQueryStringKey);
            logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            Thread.Sleep(timeoutTime);
            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(
                request,
                instanceId);
        }

        [FunctionName(nameof(Orchestrator))]
        public static async Task Orchestrator([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var arg = context.GetInput<KeyValuePair<string, double>>();
            var deviceId = new EntityId(nameof(DeviceMonitor), arg.Key);
            using (context.LockAsync(deviceId))
            {
                await context.CallEntityAsync(deviceId, "add-record", new KeyValuePair<DateTime, double>(context.CurrentUtcDateTime, arg.Value));
                var isMelting = await context.CallEntityAsync<bool>(deviceId, "is-melting");
                if (isMelting)
                {
                    // safety sequence
                    var automaticApprovalTask = context.CallActivityAsync<bool>(nameof(AutomaticApprovalRequest), new KeyValuePair<string, double>(arg.Key, arg.Value));
                    var humanInterventionTask = context.WaitForExternalEvent<bool>($"ManualApproval", TimeSpan.FromMinutes(2), true);
                    if (humanInterventionTask == await Task.WhenAny(humanInterventionTask, automaticApprovalTask))
                    {
                        await context.CallActivityAsync(nameof(TurnOffBoilerActivity), humanInterventionTask.Result);
                    }
                    else
                    {
                        await context.CallActivityAsync(nameof(TurnOffBoilerActivity), true);
                    }
                }
            }
        }

        [FunctionName(nameof(AutomaticApprovalRequest))]
        public static async Task<bool> AutomaticApprovalRequest([ActivityTrigger]IDurableActivityContext context)
        {
            var (key, value) = context.GetInput<KeyValuePair<string, double>>();
            if (value < 1000)
            {
                Thread.Sleep(TimeSpan.FromMinutes(5));
            }

            return true;
        }

        [FunctionName(nameof(DeviceMonitor))]
        public static async Task DeviceMonitor([EntityTrigger] IDurableEntityContext ctx)
        {
            /*
             * Create more actors
               Send messages to other actors
               Designate what to do with the next message
             */


            void AddRecord()
            {
                var recordedValues = ctx.GetState<Queue<KeyValuePair<DateTime, double>>>() ??
                                     new Queue<KeyValuePair<DateTime, double>>();
                var temperature = ctx.GetInput<KeyValuePair<DateTime, double>>();
                recordedValues.Enqueue(temperature);
                while (recordedValues.Count > 5 && recordedValues.TryDequeue(out _))
                {
                    ctx.SetState(recordedValues);
                }
            }

            bool IsMelting()
            {
                var recordedValues = ctx.GetState<Queue<KeyValuePair<DateTime, double>>>() ??
                                     new Queue<KeyValuePair<DateTime, double>>();
                return recordedValues.Average(kvp => kvp.Value) > 800 && recordedValues.Count == 5;
            }

            switch (ctx.OperationName)
            {
                case "add-record":
                    AddRecord();
                    break;
                case "is-melting":
                    ctx.Return(IsMelting());
                    break;
                case "reset":
                    ctx.SetState(null);
                    break;
                default:
                    throw new NotSupportedException(ctx.OperationName);
            }
        }

        public static async Task TurnOffBoilerActivity([ActivityTrigger]IDurableActivityContext context)
        {
            var (key, value) = context.GetInput<KeyValuePair<string, bool>>();
            if (value)
            {
                var serviceClient = ServiceClient.CreateFromConnectionString(Environment.GetEnvironmentVariable("IoTHubConnectionString"));
                var commandMessage = new Message(Encoding.ASCII.GetBytes("TurnOff"));
                await serviceClient.SendAsync(key, commandMessage);
            }
        }

        [FunctionName("ApprovalQueueProcessor")]
        public static async Task Run([HttpTrigger("approval-queue")] string instanceId,
            [OrchestrationClient] IDurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(instanceId, "ManualApproval", true);
        }
    }
}