@description('The location for the web app')
param location string

@description('The name of the web app')
param webAppName string

@description('The name of the App Service Plan')
param appServicePlanName string

@description('Virtual network ID')
param vnetId string

@description('Private endpoint subnet ID')
param privateEndpointSubnetId string

@description('Web app subnet ID')
param webAppSubnetId string

@description('Private DNS zone ID for sites')
param privateDnsZoneId string

@description('Cosmos DB endpoint')
param cosmosDbEndpoint string

@description('Cosmos DB database name')
param cosmosDbDatabaseName string

@description('Cosmos DB container name')
param cosmosDbContainerName string

@description('Storage account name')
param storageAccountName string

@description('Application Insights connection string')
param applicationInsightsConnectionString string

@description('Azure AD tenant ID')
param tenantId string

@description('Azure AD client ID for the web app')
param clientId string

@description('Azure AD domain')
param domain string

@description('Tags to apply to the resource')
param tags object

// App Service Plan (if not already deployed, but typically shared with Function App)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' existing = {
  name: appServicePlanName
}

// Web App
resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: webAppName
  location: location
  tags: tags
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    publicNetworkAccess: 'Disabled'
    virtualNetworkSubnetId: webAppSubnetId
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'AzureAd__Instance'
          value: 'https://login.microsoftonline.com/'
        }
        {
          name: 'AzureAd__Domain'
          value: domain
        }
        {
          name: 'AzureAd__TenantId'
          value: tenantId
        }
        {
          name: 'AzureAd__ClientId'
          value: clientId
        }
        {
          name: 'AzureAd__CallbackPath'
          value: '/signin-oidc'
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
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey='
        }
      ]
    }
  }
}

// Private Endpoint for Web App
resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = {
  name: '${webAppName}-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${webAppName}-psc'
        properties: {
          privateLinkServiceId: webApp.id
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

output webAppId string = webApp.id
output webAppName string = webApp.name
output webAppIdentityPrincipalId string = webApp.identity.principalId
output webAppDefaultHostName string = webApp.properties.defaultHostName
