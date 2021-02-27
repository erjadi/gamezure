using System;
using System.Linq;
using System.Threading.Tasks;
using Azure;
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

        public PoolManager(ILogger log)
        {
            this.log = log;
        }
        
        public async Task<VirtualMachine> CreateVm(VmCreateParams vmCreateParams)
        {
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            var credential = new DefaultAzureCredential();
            var resourceClient = new ResourcesManagementClient(subscriptionId, credential);
            var computeClient = new ComputeManagementClient(subscriptionId, credential);
            var networkManagementClient = new NetworkManagementClient(subscriptionId, credential);

            ResourceGroupsOperations resourceGroupsClient = resourceClient.ResourceGroups;
            VirtualMachinesOperations virtualMachinesClient = computeClient.VirtualMachines;
            VirtualNetworksOperations virtualNetworksClient = networkManagementClient.VirtualNetworks;
            
            Response<ResourceGroup> rgResponse = await resourceGroupsClient.GetAsync(vmCreateParams.ResourceGroupName);
            // TODO: check rgResponse for errors!
            ResourceGroup resourceGroup = rgResponse.Value;
            
            VirtualNetwork vnet = await EnsureVnet(vmCreateParams, virtualNetworksClient, resourceGroup);

            NetworkInterface nic = await CreateNetworkInterfaceAsync(networkManagementClient, resourceGroup, vmCreateParams, vnet);
            VirtualMachine vm = await CreateWindowsVm(resourceGroup, vmCreateParams, nic, virtualMachinesClient); 

            return vm;
        }

        private static async Task<VirtualNetwork> EnsureVnet(VmCreateParams vmCreateParams, VirtualNetworksOperations virtualNetworksClient, ResourceGroup resourceGroup)
        {
            VirtualNetwork vnet;
            var vnetResponse = await virtualNetworksClient.GetAsync(resourceGroup.Name, vmCreateParams.VnetName);
            
            if (vnetResponse.Value is null)
            {
                
                vnet = await CreateVirtualNetwork(vmCreateParams, virtualNetworksClient, resourceGroup);
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

        public async Task<NetworkInterface> CreateNetworkInterfaceAsync(NetworkManagementClient networkManagementClient, ResourceGroup resourceGroup, VmCreateParams vmCreateParams, VirtualNetwork vnet)
        {
            var ipAddress = await PublicIpAddress(networkManagementClient, resourceGroup, vmCreateParams);
            var nic = await CreateNic(networkManagementClient, resourceGroup, vmCreateParams, vnet, ipAddress);

            return nic;
        }

        private static async Task<NetworkInterface> CreateNic(NetworkManagementClient networkManagementClient, ResourceGroup resourceGroup,
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
            
            nic = await networkManagementClient.NetworkInterfaces
                .StartCreateOrUpdate(resourceGroup.Name, vmCreateParams.Name + "_nic", nic)
                .WaitForCompletionAsync();
            
            return nic;
        }

        private static async Task<PublicIPAddress> PublicIpAddress(NetworkManagementClient networkManagementClient, ResourceGroup resourceGroup,
            VmCreateParams vmCreateParams)
        {
            // Create IP Address
            var ipAddress = new PublicIPAddress()
            {
                PublicIPAddressVersion = IPVersion.IPv4,
                PublicIPAllocationMethod = IPAllocationMethod.Dynamic,
                Location = resourceGroup.Location,
            };


            ipAddress = await networkManagementClient.PublicIPAddresses
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