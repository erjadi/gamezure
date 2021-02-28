# Gamezure
Azure Game Streaming VM management

# Prerequisites
* An [Azure Subscription](https://azure.microsoft.com/en-us/solutions/gaming/)
* [An Azure Service Principal](https://docs.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal)
* [Terraform](https://terraform.io)
* [.NET Core 3.1](https://dot.net)

# Environment Variables
| Name     | Description    |
|----------|----------|
| `AZURE_SUBSCRIPTION_ID` | Azure Subscription ID; `id` property when executing `az account show` |
| `AZURE_TENANT_ID` | Azure Tenant ID; ; `tenantId` property when executing `az account show` |
| `TF_VAR_sp_client_id` | ID of the Azure Service Principal who should have permissions to change the VM pool |
