# Configure the Azure provider
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">= 2.48"
    }
  }

  backend "azurerm" {}
}

provider "azurerm" {
  features {}
}

locals {
  management_prefix = "${var.prefix}-management"
  vmpool_prefix = "${var.prefix}-vmpool"
}

resource "azurerm_resource_group" "rg_management" {
  name     = "${var.prefix}-management-rg"
  location = var.location
}

resource "azurerm_resource_group" "rg_vmpool" {
  name     = "${local.vmpool_prefix}-rg"
  location = var.location
}

resource "azurerm_role_assignment" "example" {
  scope                = azurerm_resource_group.rg_vmpool.id
  role_definition_name = "Contributor"
  principal_id         = var.sp_client_id
}

resource "azurerm_virtual_network" "network_vmpool" {
  name                = "${local.vmpool_prefix}-vnet"
  address_space       = ["10.0.0.0/16"]
  location            = azurerm_resource_group.rg_vmpool.location
  resource_group_name = azurerm_resource_group.rg_vmpool.name
}

resource "azurerm_subnet" "subnet_vmpool" {
  name                 = "${local.vmpool_prefix}-subnet"
  resource_group_name  = azurerm_resource_group.rg_vmpool.name
  virtual_network_name = azurerm_virtual_network.network_vmpool.name
  address_prefixes     = ["10.0.2.0/24"]
}
