using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace SafeguardFunction.Triggers
{
    public static class AutoRequestApproval
    {
        [FunctionName(nameof(AutoRequestApproval))]
        public static async Task<bool> Run([ActivityTrigger] IDurableActivityContext context)
        {
            var (key, value) = context.GetInput<KeyValuePair<string, double>>();
            if (value < 1000)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
            }

            // boiler has reached critical temperature. shut off immediately.
            return true;
        }
    }
}