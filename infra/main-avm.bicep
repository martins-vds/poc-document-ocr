targetScope = 'resourceGroup'

@description('The location for all resources')
param location string = resourceGroup().location

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@description('Name of the workload')
param workloadName string = 'documentocr'

@description('Azure AD tenant ID for web app authentication')
param tenantId string = subscription().tenantId

@description('Azure AD client ID for web app authentication')
param webAppClientId string

@description('Azure AD domain for web app authentication')
param azureAdDomain string

@description('Tags to apply to all resources')
param tags object = {
  Environment: environmentName
  Workload: workloadName
  ManagedBy: 'Bicep-AVM'
}

@description('Assign roles')
param assignRoles bool = false

@description('Id of the user or app to assign application roles')
param principalId string = ''

@description('Whether the deployment is running on GitHub Actions')
param runningOnGh string = ''

@description('Whether the deployment is running on Azure DevOps Pipeline')
param runningOnAdo string = ''

param allowedIps string = ''
var ipRules = reduce(
  filter(array(split(allowedIps, ';')), o => length(trim(o)) > 0),
  [],
  (cur, next) =>
    union(cur, [
      {
        value: next
      }
    ])
)

// Generate unique resource names
var uniqueSuffix = uniqueString(resourceGroup().id, environmentName, workloadName)
var storageAccountName = 'st${uniqueSuffix}'
var documentIntelligenceName = 'di-${uniqueSuffix}'
var cosmosDbAccountName = 'cosmos-${uniqueSuffix}'
var functionAppName = 'func-${uniqueSuffix}'
var webAppName = 'app-${uniqueSuffix}'
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
      {
        name: 'webapp-integration-subnet'
        addressPrefix: '10.0.3.0/24'
        delegation: 'Microsoft.Web/serverFarms'
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
    publicNetworkAccess: !empty(ipRules) ? 'Enabled' : 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      ipRules: map(ipRules, ipRule => {
        value: ipRule.?value
        action: 'Allow'
      })
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
    publicNetworkAccess: !empty(ipRules) ? 'Enabled' : 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      ipRules: ipRules
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
      publicNetworkAccess: !empty(ipRules) ? 'Enabled' : 'Disabled'
      ipRules: map(ipRules, ipRule => lastIndexOf(ipRule.?value, '/') == -1 ? '${ipRule.?value}/32' : ipRule.?value)
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
          {
            name: 'Operations'
            paths: [
              '/id'
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
    zoneRedundant: false
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
    publicNetworkAccess: !empty(ipRules) ? 'Enabled' : 'Disabled'
    outboundVnetRouting: {
      allTraffic: true
    }
    virtualNetworkSubnetResourceId: vnet.outputs.subnetResourceIds[0]
    storageAccountRequired: true
    httpsOnly: true
    configs: [
      {
        name: 'appsettings'
        applicationInsightResourceId: applicationInsights.outputs.resourceId
        storageAccountResourceId: storage.outputs.resourceId
        storageAccountUseIdentityAuthentication: true
      }
    ]
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      ipSecurityRestrictions: map(ipRules, ipRule => {
        ipAddress: lastIndexOf(ipRule.?value, '/') == -1 ? '${ipRule.?value}/32' : ipRule.?value
        action: 'Allow'
      })
      ipSecurityRestrictionsDefaultAction: 'Deny'
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
        {
          name: 'CosmosDb__OperationsContainerName'
          value: 'Operations'
        }
      ]
    }
    privateEndpoints: [
      {
        name: '${functionAppName}-pe'
        tags: tags
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
    diagnosticSettings: [
      {
        name: 'all'
        workspaceResourceId: logAnalytics.outputs.resourceId
        metricCategories: [
          {
            category: 'AllMetrics'
            enabled: true
          }
        ]
        logCategoriesAndGroups: [
          {
            category: 'FunctionAppLogs'
            enabled: true
          }
          {
            category: 'AppServiceAuthenticationLogs'
            enabled: true
          }
        ]
      }
    ]
    tags: union(tags, { 'azd-service-name': 'function' })
  }
}

// Web App using AVM
module webApp 'br/public:avm/res/web/site:0.19.3' = {
  name: 'web-app-deployment'
  params: {
    name: webAppName
    location: location
    kind: 'app'
    serverFarmResourceId: appServicePlan.outputs.resourceId
    managedIdentities: {
      systemAssigned: true
    }
    publicNetworkAccess: !empty(ipRules) ? 'Enabled' : 'Disabled'
    outboundVnetRouting: {
      allTraffic: true
    }
    virtualNetworkSubnetResourceId: vnet.outputs.subnetResourceIds[2]
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      ipSecurityRestrictions: map(ipRules, ipRule => {
        ipAddress: lastIndexOf(ipRule.?value, '/') == -1 ? '${ipRule.?value}/32' : ipRule.?value
        action: 'Allow'
      })
      ipSecurityRestrictionsDefaultAction: 'Deny'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.outputs.connectionString
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
          value: azureAdDomain
        }
        {
          name: 'AzureAd__TenantId'
          value: tenantId
        }
        {
          name: 'AzureAd__ClientId'
          value: webAppClientId
        }
        {
          name: 'AzureAd__CallbackPath'
          value: '/signin-oidc'
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
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccountName
        }
      ]
    }
    privateEndpoints: [
      {
        name: '${webAppName}-pe'
        tags: tags
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
    diagnosticSettings: [
      {
        name: 'all'
        workspaceResourceId: logAnalytics.outputs.resourceId
        metricCategories: [
          {
            category: 'AllMetrics'
            enabled: true
          }
        ]
        logCategoriesAndGroups: [
          {
            category: 'AppServiceHTTPLogs'
            enabled: true
          }
          {
            category: 'AppServiceConsoleLogs'
            enabled: true
          }
          {
            category: 'AppServiceAppLogs'
            enabled: true
          }
          {
            category: 'AppServiceAuthenticationLogs'
            enabled: true
          }
        ]
      }
    ]
    tags: union(tags, { 'azd-service-name': 'web' })
  }
}

// Role Assignments using custom module
// Note: AVM does not have a comprehensive role assignment module for complex scenarios
var principalType = empty(runningOnGh) && empty(runningOnAdo) ? 'User' : 'ServicePrincipal'

module userRoleAssignments 'modules/roleAssignments.bicep' = if(assignRoles && !empty(principalId)) {
  name: 'user-role-assignments-deployment'
  params: {
    principalId: principalId
    storageAccountName: storageAccountName
    documentIntelligenceName: documentIntelligenceName
    cosmosDbAccountName: cosmosDbAccountName
    principalType: principalType
  }
}

module systemRoleAssignments 'modules/roleAssignments.bicep' = if(assignRoles){
  name: 'system-role-assignments-deployment'
  params: {
    principalId: functionApp.outputs.systemAssignedMIPrincipalId!
    storageAccountName: storageAccountName
    documentIntelligenceName: documentIntelligenceName
    cosmosDbAccountName: cosmosDbAccountName
  }
}

module webAppRoleAssignments 'modules/roleAssignments.bicep' = if(assignRoles){
  name: 'web-app-role-assignments-deployment'
  params: {
    principalId: webApp.outputs.systemAssignedMIPrincipalId!
    storageAccountName: storageAccountName
    documentIntelligenceName: documentIntelligenceName
    cosmosDbAccountName: cosmosDbAccountName
  }
}

// Outputs
// Note: azd makes Bicep outputs available via 'azd env get-value <outputName>'
// Scripts use exact output names, not transformed versions
// All outputs follow AZURE_ prefix pattern for consistency

// Storage outputs
output AZURE_STORAGE_ACCOUNT_NAME string = storageAccountName

// Document Intelligence outputs  
output AZURE_DOCUMENTINTELLIGENCE_NAME string = documentIntelligenceName
output AZURE_DOCUMENTINTELLIGENCE_ENDPOINT string = documentIntelligence.outputs.endpoint

// Cosmos DB outputs
output AZURE_COSMOSDB_ACCOUNT_NAME string = cosmosDbAccountName
output AZURE_COSMOSDB_ENDPOINT string = cosmosDb.outputs.endpoint
output AZURE_COSMOSDB_DATABASE string = 'DocumentOcrDb'
output AZURE_COSMOSDB_CONTAINER string = 'ProcessedDocuments'
output AZURE_COSMOSDB_OPERATIONS_CONTAINER string = 'Operations'

// Azure AD outputs (for Web App authentication)
output AZURE_TENANT_ID string = tenantId
output AZURE_WEB_APP_CLIENT_ID string = webAppClientId
output AZURE_AD_DOMAIN string = azureAdDomain

// Function App outputs
output AZURE_FUNCTION_APP_NAME string = functionAppName
output AZURE_FUNCTION_APP_URL string = functionApp.outputs.defaultHostname

// Web App outputs
output AZURE_WEB_APP_NAME string = webAppName
output AZURE_WEB_APP_URL string = 'https://${webApp.outputs.defaultHostname}'

// Monitoring outputs
output AZURE_APPLICATION_INSIGHTS_NAME string = applicationInsightsName

// Infrastructure outputs
output AZURE_RESOURCE_GROUP_NAME string = resourceGroup().name
output AZURE_VNET_NAME string = vnet.outputs.name
output AZURE_VNET_ID string = vnet.outputs.resourceId
