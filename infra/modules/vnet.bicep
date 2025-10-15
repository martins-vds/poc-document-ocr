@description('The location for the virtual network')
param location string

@description('The name of the virtual network')
param vnetName string

@description('Tags to apply to the virtual network')
param tags object

resource vnet 'Microsoft.Network/virtualNetworks@2023-05-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'function-integration-subnet'
        properties: {
          addressPrefix: '10.0.1.0/24'
          delegations: [
            {
              name: 'delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
          serviceEndpoints: []
          privateEndpointNetworkPolicies: 'Enabled'
        }
      }
      {
        name: 'webapp-integration-subnet'
        properties: {
          addressPrefix: '10.0.3.0/24'
          delegations: [
            {
              name: 'delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
          serviceEndpoints: []
          privateEndpointNetworkPolicies: 'Enabled'
        }
      }
      {
        name: 'private-endpoint-subnet'
        properties: {
          addressPrefix: '10.0.2.0/24'
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
        }
      }
    ]
  }
}

output vnetId string = vnet.id
output vnetName string = vnet.name
output functionIntegrationSubnetId string = '${vnet.id}/subnets/function-integration-subnet'
output webAppIntegrationSubnetId string = '${vnet.id}/subnets/webapp-integration-subnet'
output privateEndpointSubnetId string = '${vnet.id}/subnets/private-endpoint-subnet'
