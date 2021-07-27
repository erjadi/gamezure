using System;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Microsoft.Extensions.Logging;
using IPVersion = Azure.ResourceManager.Network.Models.IPVersion;
using NetworkInterface = Azure.ResourceManager.Network.Models.NetworkInterface;
using NetworkProfile = Azure.ResourceManager.Compute.Models.NetworkProfile;

namespace Gamezure.VmPoolManager
{
    public class PoolManager
    {
        private readonly ILogger log;
        private readonly string subscriptionId;
        private readonly TokenCredential credential;
        private readonly ResourcesManagementClient resourceClient;
        private readonly ComputeManagementClient computeClient;
        private readonly NetworkManagementClient networkManagementClient;
        private readonly ResourceGroupsOperations resourceGroupsClient;
        private readonly VirtualMachinesOperations virtualMachinesClient;
        private readonly VirtualNetworksOperations virtualNetworksClient;

        public PoolManager(ILogger log, string subscriptionId, TokenCredential credential)
        {
            this.log = log;
            this.subscriptionId = subscriptionId;
            this.credential = credential;
            
            resourceClient = new ResourcesManagementClient(this.subscriptionId, this.credential);
            computeClient = new ComputeManagementClient(this.subscriptionId, this.credential);
            networkManagementClient = new NetworkManagementClient(this.subscriptionId, this.credential);
            
            resourceGroupsClient = resourceClient.ResourceGroups;
            virtualMachinesClient = computeClient.VirtualMachines;
            virtualNetworksClient = networkManagementClient.VirtualNetworks;
        }

        public PoolManager(ILogger log, string subscriptionId) : this(log, subscriptionId, new DefaultAzureCredential())
        {
        }
        
        public async Task<VirtualMachine> CreateVm(VmCreateParams vmCreateParams)
        {
            Response<ResourceGroup> rgResponse = await resourceGroupsClient.GetAsync(vmCreateParams.ResourceGroupName);
            ResourceGroup resourceGroup = rgResponse.Value;
            
            VirtualNetwork vnet = await EnsureVnet(vmCreateParams, resourceGroup);

            NetworkInterface nic = await CreateNetworkInterfaceAsync(resourceGroup, vmCreateParams, vnet);
            VirtualMachine vm = await CreateWindowsVm(resourceGroup, vmCreateParams, nic, virtualMachinesClient); 

            return vm;
        }

        public async Task<bool> GuardResourceGroup(string name)
        {
            bool exists = false;
            try
            {
                Response rgExists =
                    await resourceGroupsClient.CheckExistenceAsync(name);

                if (rgExists.Status == 204) // 204 - No Content
                {
                    exists = true;
                }
            }
            catch (RequestFailedException requestFailedException)
            {
                switch (requestFailedException.Status)
                {
                    case 403:   // 403 - Forbidden
                        log.LogError(requestFailedException,
                            "No permission to read a resource group with the name {RgName}", name);
                        break;
                    default:
                        log.LogError(requestFailedException, "Request failed");
                        break;
                }
            }

            return exists;
        }

        private async Task<VirtualNetwork> EnsureVnet(VmCreateParams vmCreateParams, ResourceGroup resourceGroup)
        {
            VirtualNetwork vnet;
            var vnetResponse = await this.virtualNetworksClient.GetAsync(resourceGroup.Name, vmCreateParams.VnetName);
            
            if (vnetResponse.Value is null)
            {
                
                vnet = await CreateVirtualNetwork(vmCreateParams, this.virtualNetworksClient, resourceGroup);
                if (vnet is null)
                {
                    throw new Exception($"Could not create vnet {vmCreateParams.VnetName} in resource group {resourceGroup.Name}");
                }
            }
            else
            {
                vnet = vnetResponse.Value;
            }

            return vnet;
        }

        public async Task<ResourceGroup> CreateResourceGroup(string resourceGroupName, string region)
        {
            var resourceGroupResponse = await resourceGroupsClient.CreateOrUpdateAsync(resourceGroupName, new ResourceGroup(region));
            return resourceGroupResponse.Value;
        }

        private static async Task<VirtualNetwork> CreateVirtualNetwork(VmCreateParams vmCreateParams,
            VirtualNetworksOperations virtualNetworksClient, ResourceGroup resourceGroup)
        {
            var vnet = new VirtualNetwork
            {
                Location = vmCreateParams.ResourceLocation,
                Subnets = { }
            };

            await virtualNetworksClient.StartCreateOrUpdateAsync(resourceGroup.Name, vmCreateParams.VnetName, vnet);
            return vnet;
        }

        public async Task<VirtualMachine> CreateWindowsVm(ResourceGroup resourceGroup, VmCreateParams vmCreateParams,
            NetworkInterface nic, VirtualMachinesOperations computeClientVirtualMachines)
        {
            // Create Windows VM

            var windowsVM = new VirtualMachine(resourceGroup.Location)
            {
                OsProfile = new OSProfile
                {
                    ComputerName = vmCreateParams.Name,
                    AdminUsername = vmCreateParams.UserName,
                    AdminPassword = vmCreateParams.UserPassword,
                },
                NetworkProfile = new NetworkProfile(),
                StorageProfile = new StorageProfile
                {
                    ImageReference = new ImageReference
                    {
                        Offer = "WindowsServer",
                        Publisher = "MicrosoftWindowsServer",
                        Sku = "2019-Datacenter",
                        Version = "latest"
                    },
                    // DataDisks = new List<DataDisk>()
                },
                HardwareProfile = new HardwareProfile { VmSize = VirtualMachineSizeTypes.StandardD3V2 },
            };
            
            
            windowsVM.NetworkProfile.NetworkInterfaces.Add(new NetworkInterfaceReference { Id = nic.Id });

            windowsVM = await (await computeClientVirtualMachines
                .StartCreateOrUpdateAsync(resourceGroup.Name, vmCreateParams.Name, windowsVM)).WaitForCompletionAsync();

            return windowsVM;
        }

        private async Task<NetworkInterface> CreateNetworkInterfaceAsync(ResourceGroup resourceGroup, VmCreateParams vmCreateParams, VirtualNetwork vnet)
        {
            var ipAddress = await PublicIpAddress(resourceGroup, vmCreateParams);
            var nic = await CreateNic(resourceGroup, vmCreateParams, vnet, ipAddress);

            return nic;
        }

        private async Task<NetworkInterface> CreateNic(ResourceGroup resourceGroup,
            VmCreateParams vmCreateParams, VirtualNetwork vnet, PublicIPAddress ipAddress)
        {
            // Create Network interface
            var networkInterfaceIpConfiguration = new NetworkInterfaceIPConfiguration
            {
                Name = "Primary",
                Primary = true,
                Subnet = new Subnet { Id = vnet.Subnets.First().Id },
                PrivateIPAllocationMethod = IPAllocationMethod.Dynamic,
                PublicIPAddress = new PublicIPAddress { Id = ipAddress.Id }
            };
            
            var nic = new NetworkInterface()
            {
                Location = resourceGroup.Location
            };
            nic.IpConfigurations.Add(networkInterfaceIpConfiguration);
            
            nic = await this.networkManagementClient.NetworkInterfaces
                .StartCreateOrUpdate(resourceGroup.Name, vmCreateParams.Name + "_nic", nic)
                .WaitForCompletionAsync();
            
            return nic;
        }

        private async Task<PublicIPAddress> PublicIpAddress(ResourceGroup resourceGroup,
            VmCreateParams vmCreateParams)
        {
            // Create IP Address
            var ipAddress = new PublicIPAddress()
            {
                PublicIPAddressVersion = IPVersion.IPv4,
                PublicIPAllocationMethod = IPAllocationMethod.Dynamic,
                Location = resourceGroup.Location,
            };


            ipAddress = await this.networkManagementClient.PublicIPAddresses
                .StartCreateOrUpdate(resourceGroup.Name, vmCreateParams.Name + "_ip", ipAddress)
                .WaitForCompletionAsync();
            return ipAddress;
        }

        public readonly struct VmCreateParams
        {
            public string Name { get; }
            public string UserName { get; }
            public string UserPassword { get; }
            public string VnetName { get; }
            public string ResourceGroupName { get; }
            public string ResourceLocation { get; }


            public VmCreateParams(string name, string userName, string userPassword, string vnetName, string resourceGroupName, string resourceLocation)
            {
                this.Name = name;
                this.UserName = userName;
                this.UserPassword = userPassword;
                this.VnetName = vnetName;
                this.ResourceGroupName = resourceGroupName;
                this.ResourceLocation = resourceLocation;
            }
        }
    }
}