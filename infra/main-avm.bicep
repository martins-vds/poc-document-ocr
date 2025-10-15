targetScope = 'resourceGroup'

@description('The location for all resources')
param location string = resourceGroup().location

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@description('Name of the workload')
param workloadName string = 'documentocr'

@description('Tags to apply to all resources')
param tags object = {
  Environment: environmentName
  Workload: workloadName
  ManagedBy: 'Bicep-AVM'
}

// Generate unique resource names
var uniqueSuffix = uniqueString(resourceGroup().id, environmentName, workloadName)
var storageAccountName = 'st${uniqueSuffix}'
var documentIntelligenceName = 'di-${uniqueSuffix}'
var cosmosDbAccountName = 'cosmos-${uniqueSuffix}'
var functionAppName = 'func-${uniqueSuffix}'
var appServicePlanName = 'asp-${uniqueSuffix}'
var applicationInsightsName = 'appi-${uniqueSuffix}'
var logAnalyticsWorkspaceName = 'log-${uniqueSuffix}'
var vnetName = 'vnet-${uniqueSuffix}'

// ===================================
// Azure Verified Modules (AVM)
// Using modules from Bicep Public Registry (br/public:)
// 
// Module versions are pinned for stability. Update versions as needed:
// - Check latest versions at: https://azure.github.io/Azure-Verified-Modules/
// - Module format: br/public:avm/res/<provider>/<resource>:<version>
// - All modules are officially maintained by Microsoft
// ===================================

// Virtual Network using AVM
module vnet 'br/public:avm/res/network/virtual-network:0.7.1' = {
  name: 'vnet-deployment'
  params: {
    name: vnetName
    location: location
    addressPrefixes: [
      '10.0.0.0/16'
    ]
    subnets: [
      {
        name: 'function-integration-subnet'
        addressPrefix: '10.0.1.0/24'
        delegation: 'Microsoft.Web/serverFarms'
      }
      {
        name: 'private-endpoint-subnet'
        addressPrefix: '10.0.2.0/24'
        privateEndpointNetworkPolicies: 'Disabled'
      }
    ]
    tags: tags
  }
}

// Private DNS Zones using AVM
var privateDnsZones = [
  'privatelink.blob.${environment().suffixes.storage}'
  'privatelink.queue.${environment().suffixes.storage}'
  'privatelink.table.${environment().suffixes.storage}'
  'privatelink.documents.azure.com'
  'privatelink.cognitiveservices.azure.com'
  'privatelink.azurewebsites.net'
]

module privateDnsZone 'br/public:avm/res/network/private-dns-zone:0.8.0' = [
  for (zone, i) in privateDnsZones: {
    name: 'private-dns-zone-${i}'
    params: {
      name: zone
      location: 'global'
      virtualNetworkLinks: [
        {
          name: 'link-to-${vnetName}'
          virtualNetworkResourceId: vnet.outputs.resourceId
          registrationEnabled: false
        }
      ]
      tags: tags
    }
  }
]

// Log Analytics Workspace using AVM
module logAnalytics 'br/public:avm/res/operational-insights/workspace:0.12.0' = {
  name: 'log-analytics-deployment'
  params: {
    name: logAnalyticsWorkspaceName
    location: location
    skuName: 'PerGB2018'
    dataRetention: 30
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    tags: tags
  }
}

// Application Insights using AVM
module applicationInsights 'br/public:avm/res/insights/component:0.6.1' = {
  name: 'application-insights-deployment'
  params: {
    name: applicationInsightsName
    location: location
    workspaceResourceId: logAnalytics.outputs.resourceId
    kind: 'web'
    applicationType: 'web'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    tags: tags
  }
}

// Storage Account with Private Endpoints using AVM
module storage 'br/public:avm/res/storage/storage-account:0.27.1' = {
  name: 'storage-deployment'
  params: {
    name: storageAccountName
    location: location
    kind: 'StorageV2'
    skuName: 'Standard_LRS'
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
    blobServices: {
      containers: [
        {
          name: 'uploaded-pdfs'
          publicAccess: 'None'
        }
        {
          name: 'processed-documents'
          publicAccess: 'None'
        }
      ]
    }
    queueServices: {
      queues: [
        {
          name: 'pdf-processing-queue'
        }
      ]
    }
    tableServices: {}
    privateEndpoints: [
      {
        name: '${storageAccountName}-blob-pe'
        subnetResourceId: vnet.outputs.subnetResourceIds[1]
        privateDnsZoneGroup: {
          privateDnsZoneGroupConfigs: [
            {
              privateDnsZoneResourceId: privateDnsZone[0].outputs.resourceId
            }
          ]
        }
        service: 'blob'
      }
      {
        name: '${storageAccountName}-queue-pe'
        subnetResourceId: vnet.outputs.subnetResourceIds[1]
        privateDnsZoneGroup: {
          privateDnsZoneGroupConfigs: [
            {
              privateDnsZoneResourceId: privateDnsZone[1].outputs.resourceId
            }
          ]
        }
        service: 'queue'
      }
      {
        name: '${storageAccountName}-table-pe'
        subnetResourceId: vnet.outputs.subnetResourceIds[1]
        privateDnsZoneGroup: {
          privateDnsZoneGroupConfigs: [
            {
              privateDnsZoneResourceId: privateDnsZone[2].outputs.resourceId
            }
          ]
        }
        service: 'table'
      }
    ]
    tags: tags
  }
}

// Document Intelligence (Cognitive Services) using AVM
module documentIntelligence 'br/public:avm/res/cognitive-services/account:0.13.2' = {
  name: 'document-intelligence-deployment'
  params: {
    name: documentIntelligenceName
    location: location
    kind: 'FormRecognizer'
    customSubDomainName: documentIntelligenceName
    sku: 'S0'
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
    }
    privateEndpoints: [
      {
        name: '${documentIntelligenceName}-pe'
        subnetResourceId: vnet.outputs.subnetResourceIds[1]
        privateDnsZoneGroup: {
          privateDnsZoneGroupConfigs: [
            {
              privateDnsZoneResourceId: privateDnsZone[4].outputs.resourceId
            }
          ]
        }
        service: 'account'
      }
    ]
    tags: tags
  }
}

// Cosmos DB using AVM
module cosmosDb 'br/public:avm/res/document-db/database-account:0.16.0' = {
  name: 'cosmos-db-deployment'
  params: {
    name: cosmosDbAccountName
    location: location
    capabilitiesToAdd: [
      'EnableServerless'
    ]
    networkRestrictions: {
      publicNetworkAccess: 'Disabled'
    }
    sqlDatabases: [
      {
        name: 'DocumentOcrDb'
        containers: [
          {
            name: 'ProcessedDocuments'
            paths: [
              '/identifier'
            ]
            kind: 'Hash'
          }
        ]
      }
    ]
    privateEndpoints: [
      {
        name: '${cosmosDbAccountName}-pe'
        subnetResourceId: vnet.outputs.subnetResourceIds[1]
        privateDnsZoneGroup: {
          privateDnsZoneGroupConfigs: [
            {
              privateDnsZoneResourceId: privateDnsZone[3].outputs.resourceId
            }
          ]
        }
        service: 'Sql'
      }
    ]
    tags: tags
  }
}

// App Service Plan using AVM
module appServicePlan 'br/public:avm/res/web/serverfarm:0.5.0' = {
  name: 'app-service-plan-deployment'
  params: {
    name: appServicePlanName
    location: location
    kind: 'linux'
    reserved: true
    skuName: 'P1v3'
    skuCapacity: 1
    tags: tags
  }
}

// Azure Function App using AVM
module functionApp 'br/public:avm/res/web/site:0.19.3' = {
  name: 'function-app-deployment'
  params: {
    name: functionAppName
    location: location
    kind: 'functionapp,linux'
    serverFarmResourceId: appServicePlan.outputs.resourceId
    managedIdentities: {
      systemAssigned: true
    }
    publicNetworkAccess: 'Disabled'
    outboundVnetRouting: {
      allTraffic: true
    }
    virtualNetworkSubnetResourceId: vnet.outputs.subnetResourceIds[0]
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
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
          value: applicationInsights.outputs.connectionString
        }
        {
          name: 'DocumentIntelligence__Endpoint'
          value: documentIntelligence.outputs.endpoint
        }
        {
          name: 'CosmosDb__Endpoint'
          value: cosmosDb.outputs.endpoint
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: 'DocumentOcrDb'
        }
        {
          name: 'CosmosDb__ContainerName'
          value: 'ProcessedDocuments'
        }
      ]
    }
    privateEndpoints: [
      {
        name: '${functionAppName}-pe'
        subnetResourceId: vnet.outputs.subnetResourceIds[1]
        privateDnsZoneGroup: {
          privateDnsZoneGroupConfigs: [
            {
              privateDnsZoneResourceId: privateDnsZone[5].outputs.resourceId
            }
          ]
        }
        service: 'sites'
      }
    ]
    tags: tags
  }
}

// Role Assignments using custom module
// Note: AVM does not have a comprehensive role assignment module for complex scenarios
module roleAssignments 'modules/roleAssignments.bicep' = {
  name: 'role-assignments-deployment'
  params: {
    functionAppPrincipalId: functionApp.outputs.systemAssignedMIPrincipalId!
    storageAccountName: storageAccountName
    documentIntelligenceName: documentIntelligenceName
    cosmosDbAccountName: cosmosDbAccountName
  }
}

// Outputs
output storageAccountName string = storageAccountName
output documentIntelligenceName string = documentIntelligenceName
output documentIntelligenceEndpoint string = documentIntelligence.outputs.endpoint
output cosmosDbAccountName string = cosmosDbAccountName
output cosmosDbEndpoint string = cosmosDb.outputs.endpoint
output functionAppName string = functionAppName
output functionAppUrl string = functionApp.outputs.defaultHostname
output applicationInsightsName string = applicationInsightsName
output resourceGroupName string = resourceGroup().name
output vnetName string = vnet.outputs.name
output vnetId string = vnet.outputs.resourceId
