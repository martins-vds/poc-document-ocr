@description('The location for Document Intelligence')
param location string

@description('The name of the Document Intelligence resource')
param documentIntelligenceName string

@description('Virtual network ID')
param vnetId string

@description('Private endpoint subnet ID')
param privateEndpointSubnetId string

@description('Private DNS zone ID for Cognitive Services')
param privateDnsZoneId string

@description('Tags to apply to the resource')
param tags object

resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: documentIntelligenceName
  location: location
  tags: tags
  kind: 'FormRecognizer'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: documentIntelligenceName
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
    }
  }
}

// Private Endpoint
resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = {
  name: '${documentIntelligenceName}-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${documentIntelligenceName}-psc'
        properties: {
          privateLinkServiceId: documentIntelligence.id
          groupIds: [
            'account'
          ]
        }
      }
    ]
  }
}

resource privateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-05-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: privateDnsZoneId
        }
      }
    ]
  }
}

output documentIntelligenceId string = documentIntelligence.id
output documentIntelligenceName string = documentIntelligence.name
output endpoint string = documentIntelligence.properties.endpoint
