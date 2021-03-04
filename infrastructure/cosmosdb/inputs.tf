variable "resource_group" {
  description = "The resource group"
}

variable "use_cosmosdb_free_tier" {
  type        = bool
  description = "Whether to use the CosmosDB free tier. You may only have one CosmosDB account per Azure subscription. For details, see https://docs.microsoft.com/en-us/azure/cosmos-db/how-pricing-works#try-azure-cosmos-db-for-free"
  default     = false
}

variable "prefix" {
  type        = string
  description = "Resource Name prefix (will be applied to all resource names except the resource group"
  default     = "gamezure"
}

variable "tags" {
  type    = map(string)
  default = {}
}
