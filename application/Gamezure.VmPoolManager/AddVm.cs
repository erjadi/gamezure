using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Azure.Cosmos;
using Azure.ResourceManager.Compute.Models;
using Gamezure.VmPoolManager.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Gamezure.VmPoolManager
{
    public static class AddVm
    {
        [FunctionName("AddVm")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = null)]
            HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string poolId = req.Query["poolid"];
            if (string.IsNullOrWhiteSpace(poolId))
            {
                return new BadRequestObjectResult("Please pass a `poolid` as query parameter");
            }

            string connectionString = Environment.GetEnvironmentVariable("CosmosDb");
            var poolRepository = new PoolRepository(connectionString);

            ItemResponse<Pool> response = await poolRepository.Get(poolId);
            Pool pool = response.Value;
            string resourceGroupName = pool.ResourceGroupName;

            string subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            var poolManager = new PoolManager(log, subscriptionId);
            
            bool resourceGroupExists = await poolManager.GuardResourceGroup(resourceGroupName);
            if (!resourceGroupExists)
            {
                return new InternalServerErrorResult();
            }

            int vmCount = pool.DesiredVmCount;
            List<VirtualMachine> vms = new List<VirtualMachine>(vmCount);
            List<Task<VirtualMachine>> tasks = new List<Task<VirtualMachine>>(vmCount);
            
            for (int i = 0; i < vmCount; i++)
            {
                var vmCreateParams = new PoolManager.VmCreateParams(
                    $"gamezure-vm-{i}",
                    "gamezure-user",
                    Guid.NewGuid().ToString(),
                    "gamezure-vmpool-vnet",
                    resourceGroupName,
                    "westeurope"
                );
                
                var item = poolManager.CreateVm(vmCreateParams);
                tasks.Add(item);
            }

            foreach (Task<VirtualMachine> task in tasks)
            {
                vms.Add(await task);
            }

            return new OkObjectResult(vms);
        }
    }
}