using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gamezure.VmPoolManager
{
    public class Pool
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        public string ResourceGroupName { get; set; }
        public int DesiredVmCount { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}