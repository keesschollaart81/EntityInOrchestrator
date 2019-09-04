using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading;

namespace Company.Function
{
    public static class EntityInOrchestrator
    {
        [FunctionName(nameof(HttpTrigger))]
        public static async Task<IActionResult> HttpTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpTriggerArgs args,
            [DurableClient] IDurableOrchestrationClient durableOrchestrationClient,
            ILogger log)
        {
            var orchestrationId = await durableOrchestrationClient.StartNewAsync(nameof(Orchestration), args);

            log.LogInformation("Started orchestration with ID = '{orchestrationId}'.", orchestrationId);
            var response = durableOrchestrationClient.CreateHttpManagementPayload(orchestrationId);

            return new OkObjectResult(response);
        }

        [FunctionName(nameof(Orchestration))]
        public static async Task Orchestration(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            var args = context.GetInput<HttpTriggerArgs>();

            using (var cts = new CancellationTokenSource())
            {
                await context.WaitForExternalEvent("WakeUp", TimeSpan.FromSeconds(30));

                var entityId = new EntityId(nameof(DeviceEntity), args.DeviceId);
                var onlineStatusTask = await context.CallEntityAsync<string>(entityId, nameof(DeviceEntity.Getonline));
            }

            context.ContinueAsNew(args, false);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DeviceEntity
    {
        [JsonProperty]
        public bool Online { get; set; }

        [FunctionName(nameof(DeviceEntity))]
        public static async Task HandleEntityOperation(
            [EntityTrigger] IDurableEntityContext context,
            ILogger logger)
        {
            await context.DispatchAsync<DeviceEntity>();
        }

        public string Getonline()
        {
            return $"{this.Online}";
        }
    }

    public class HttpTriggerArgs
    {
        public string DeviceId { get; set; }
    }

}
