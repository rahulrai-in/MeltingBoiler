using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace DataGenerator
{
    public class Program
    {
        static async Task Main()
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", true, true).Build();
            while (true)
            {
                try
                {
                    Console.WriteLine("Enter a command: normal, critical, melt, shutdown");
                    var command = Console.ReadLine();
                    var serviceClient = ServiceClient.CreateFromConnectionString(config["DeviceConnectionString"]);
                    var cloudToDeviceMethod = new CloudToDeviceMethod("command")
                    {
                        ConnectionTimeout = TimeSpan.FromSeconds(5),
                        ResponseTimeout = TimeSpan.FromSeconds(5)
                    };
                    cloudToDeviceMethod.SetPayloadJson(JsonConvert.SerializeObject(new { command = command }));
                    var response = await serviceClient.InvokeDeviceMethodAsync("myboilercontroller", "controller", cloudToDeviceMethod);
                    var jsonResult = response.GetPayloadAsJson();
                    Console.WriteLine(jsonResult);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
