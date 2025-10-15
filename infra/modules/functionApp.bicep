@description('The location for the Function App')
param location string

@description('The name of the Function App')
param functionAppName string

@description('App Service Plan ID')
param appServicePlanId string

@description('Storage account name')
param storageAccountName string

@description('Application Insights connection string')
param applicationInsightsConnectionString string

@description('Virtual network ID')
param vnetId string

@description('Integration subnet ID')
param integrationSubnetId string

@description('Private endpoint subnet ID')
param privateEndpointSubnetId string

@description('Private DNS zone ID for sites')
param privateDnsZoneId string

@description('Document Intelligence endpoint')
param documentIntelligenceEndpoint string

@description('Cosmos DB endpoint')
param cosmosDbEndpoint string

@description('Cosmos DB database name')
param cosmosDbDatabaseName string

@description('Cosmos DB container name')
param cosmosDbContainerName string

@description('Tags to apply to the resource')
param tags object

resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    reserved: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: true
      vnetRouteAllEnabled: true
      publicNetworkAccess: 'Disabled'
      appSettings: [
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccountName
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        {
          name: 'DocumentIntelligence__Endpoint'
          value: documentIntelligenceEndpoint
        }
        {
          name: 'CosmosDb__Endpoint'
          value: cosmosDbEndpoint
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: cosmosDbDatabaseName
        }
        {
          name: 'CosmosDb__ContainerName'
          value: cosmosDbContainerName
        }
      ]
    }
    httpsOnly: true
    virtualNetworkSubnetId: integrationSubnetId
    vnetContentShareEnabled: false
  }
}

// Private Endpoint for Function App
resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = {
  name: '${functionAppName}-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${functionAppName}-psc'
        properties: {
          privateLinkServiceId: functionApp.id
          groupIds: [
            'sites'
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

output functionAppId string = functionApp.id
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output functionAppPrincipalId string = functionApp.identity.principalId
