using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Gamezure.VmPoolManager.Repository
{
    public class PoolRepository
    {
        private readonly CosmosClient client;

        private readonly Container container;

        public PoolRepository(string connectionString)
        {
            var clientOptions = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            };
            this.client = new CosmosClient(connectionString, clientOptions);
            
            this.container = client.GetContainer("gamezure-db", "vmpool");
        }

        public Task<ItemResponse<Pool>> Save(Pool pool)
        {
            return this.container.CreateItemAsync(pool);
        }

        public Task<ItemResponse<Pool>> Get(string poolId)
        {
            return this.container.ReadItemAsync<Pool>(poolId, new PartitionKey(poolId));
        }
    }
}