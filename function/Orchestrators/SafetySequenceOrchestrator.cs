using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SafeguardFunction.Core;
using SafeguardFunction.Triggers;

namespace SafeguardFunction.Orchestrators
{
    public static class SafetySequenceOrchestrator
    {
        [FunctionName(nameof(SafetySequenceOrchestrator))]
        public static async Task Run([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            try
            {
                var (key, value) = context.GetInput<KeyValuePair<string, double>>();
                var deviceId = new EntityId(nameof(DeviceMonitor), key);
                using (context.LockAsync(deviceId))
                {
                    await context.CallEntityAsync(deviceId, Constants.ActorOperationAddRecord,
                        new KeyValuePair<DateTime, double>(context.CurrentUtcDateTime, value));
                    var isMelting = await context.CallEntityAsync<bool>(deviceId, Constants.ActorOperationIsMelting);
                    if (isMelting)
                    {
                        // safety sequence
                        await context.CallActivityWithRetryAsync(nameof(SendApprovalRequest), Policies.Retry,
                            context.InstanceId);
                        var automaticApprovalTask = context.CallActivityWithRetryAsync<bool>(
                            nameof(AutoRequestApproval),
                            Policies.Retry,
                            new KeyValuePair<string, double>(key, value));
                        var humanInterventionTask =
                            context.WaitForExternalEvent(Constants.ManualApproval, TimeSpan.FromMinutes(2), true);
                        if (humanInterventionTask == await Task.WhenAny(humanInterventionTask, automaticApprovalTask))
                        {
                            await context.CallEntityAsync(deviceId, Constants.ActorOperationSendInstruction,
                                humanInterventionTask.Result);
                        }
                        else
                        {
                            await context.CallEntityAsync(deviceId, Constants.ActorOperationSendInstruction, true);
                        }

                        await context.CallEntityAsync(deviceId, Constants.ActorOperationReset);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, e.ToString());
            }
        }
    }
}