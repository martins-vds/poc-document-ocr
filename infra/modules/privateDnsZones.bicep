@description('Virtual network ID for linking')
param vnetId string

@description('Private DNS zone name for Blob storage')
param privateDnsZoneNameBlob string

@description('Private DNS zone name for Queue storage')
param privateDnsZoneNameQueue string

@description('Private DNS zone name for Table storage')
param privateDnsZoneNameTable string

@description('Private DNS zone name for Cosmos DB')
param privateDnsZoneNameCosmos string

@description('Private DNS zone name for Cognitive Services')
param privateDnsZoneNameCognitiveServices string

@description('Private DNS zone name for Azure Web Sites')
param privateDnsZoneNameSites string

@description('Tags to apply to resources')
param tags object

// Private DNS Zone for Blob Storage
resource privateDnsZoneBlob 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZoneNameBlob
  location: 'global'
  tags: tags
}

resource privateDnsZoneLinkBlob 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: privateDnsZoneBlob
  name: 'link-to-vnet-blob'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

// Private DNS Zone for Queue Storage
resource privateDnsZoneQueue 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZoneNameQueue
  location: 'global'
  tags: tags
}

resource privateDnsZoneLinkQueue 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: privateDnsZoneQueue
  name: 'link-to-vnet-queue'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

// Private DNS Zone for Table Storage
resource privateDnsZoneTable 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZoneNameTable
  location: 'global'
  tags: tags
}

resource privateDnsZoneLinkTable 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: privateDnsZoneTable
  name: 'link-to-vnet-table'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

// Private DNS Zone for Cosmos DB
resource privateDnsZoneCosmos 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZoneNameCosmos
  location: 'global'
  tags: tags
}

resource privateDnsZoneLinkCosmos 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: privateDnsZoneCosmos
  name: 'link-to-vnet-cosmos'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

// Private DNS Zone for Cognitive Services
resource privateDnsZoneCognitiveServices 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZoneNameCognitiveServices
  location: 'global'
  tags: tags
}

resource privateDnsZoneLinkCognitiveServices 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: privateDnsZoneCognitiveServices
  name: 'link-to-vnet-cognitive'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

// Private DNS Zone for Azure Web Sites (Function Apps)
resource privateDnsZoneSites 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZoneNameSites
  location: 'global'
  tags: tags
}

resource privateDnsZoneLinkSites 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: privateDnsZoneSites
  name: 'link-to-vnet-sites'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

output privateDnsZoneIdBlob string = privateDnsZoneBlob.id
output privateDnsZoneIdQueue string = privateDnsZoneQueue.id
output privateDnsZoneIdTable string = privateDnsZoneTable.id
output privateDnsZoneIdCosmos string = privateDnsZoneCosmos.id
output privateDnsZoneIdCognitiveServices string = privateDnsZoneCognitiveServices.id
output privateDnsZoneIdSites string = privateDnsZoneSites.id
