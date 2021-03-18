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

            // string name = req.Query["name"];
            //
            // string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            // dynamic data = JsonConvert.DeserializeObject(requestBody);
            // name = name ?? data?.name;
            //
            // return name != null
            //     ? (ActionResult) new OkObjectResult($"Hello, {name}")
            //     : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
            
            string resourceGroupName = "gamezure-vmpool-rg";


            string connectionString = Environment.GetEnvironmentVariable("CosmosDb");
            var poolRepository = new PoolRepository(connectionString);
            
            var pool = new Pool
            {
                Id = Guid.NewGuid().ToString(),
                ResourceGroupName = resourceGroupName
            };

            ItemResponse<Pool> response = await poolRepository.Save(pool);


            string subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            var poolManager = new PoolManager(log, subscriptionId);
            
            bool resourceGroupExists = await poolManager.GuardResourceGroup(resourceGroupName);
            if (!resourceGroupExists)
            {
                return new InternalServerErrorResult();
            }

            int vmCount = 3;
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