using System;
using System.IO;
using System.Threading.Tasks;
using Azure.ResourceManager.Compute.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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


            var vmCreateParams = new PoolManager.VmCreateParams(
                "gamezure-vm",
                "gamezure-user",
                Guid.NewGuid().ToString(),
                "gamezure-vnet",
                "rg-gamezure"
            );
            
            VirtualMachine vm = await new PoolManager(log).CreateVm(vmCreateParams);

            return new OkObjectResult(vm);
        }
    }
}