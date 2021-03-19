using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Cosmos;
using Gamezure.VmPoolManager.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
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
            
            ItemResponse<Pool> response = await poolRepository.Save(pool);

            return new OkObjectResult(response.Value);
        }
    }
}