using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Gamezure.VmPoolManager.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Gamezure.VmPoolManager
{
    public static class AddPool
    {
        [FunctionName("AddPool")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = null)]
            HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Pool pool = JsonConvert.DeserializeObject<Pool>(requestBody);
            
            string connectionString = Environment.GetEnvironmentVariable("CosmosDb");
            var poolRepository = new PoolRepository(connectionString);
            
            string subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            var poolManager = new PoolManager(log, subscriptionId);
            
            try
            {
                ItemResponse<Pool> response = await poolRepository.Save(pool);
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.Conflict)
            {
                return new ConflictResult();
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.BadRequest)
            {
                return new BadRequestObjectResult(e.ResponseBody);
            }
            catch (CosmosException e)
            {
                return new ObjectResult(e);
            }

            await poolManager.CreateResourceGroup(pool.ResourceGroupName, pool.Location);
            string vnetName = pool.Id + "vnet";
            await poolManager.EnsureVnet(pool.ResourceGroupName, pool.Location, vnetName);
            
            return new OkResult();
        }
    }
}