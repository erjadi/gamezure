using System.Threading.Tasks;
using Azure.Cosmos;

namespace Gamezure.VmPoolManager.Repository
{
    public class PoolRepository
    {
        private readonly CosmosClient client;
        private readonly CosmosContainer container;

        public PoolRepository(string connectionString)
        {
            this.client = new CosmosClient(connectionString);
            this.container = client.GetContainer("gamezure-db", "vmpool");
        }

        public Task<ItemResponse<Pool>> Save(Pool pool)
        {
            return this.container.CreateItemAsync(pool);
        }
    }
}