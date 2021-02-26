variable "rgName" {
  type = string
  description = "The resource group name"
  default = "rg-gamezure"
}

variable "location" {
  type = string
  description = "The location in which to create all resources"
  default = "westeurope"
}

variable "prefix" {
  type = string
  description = "Resource Name prefix (will be applied to all resource names except the resource group"
  default = "gamezure"
}

variable "tags" {
  type = list(string)
  default = []
}