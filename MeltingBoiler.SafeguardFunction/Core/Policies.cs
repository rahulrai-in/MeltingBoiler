using Microsoft.Azure.WebJobs;
using System;

namespace MeltingBoiler.SafeguardFunction.Core
{
    public static class Policies
    {
        public static RetryOptions Retry => new RetryOptions(TimeSpan.FromMinutes(1), 10) { BackoffCoefficient = 2.0 };
    }
}