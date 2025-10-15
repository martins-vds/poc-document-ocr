targetScope = 'resourceGroup'

@description('The location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, test, prod)')
@maxLength(8)
param environmentName string = 'dev'

@description('Name of the workload')
param workloadName string = 'documentocr'

@description('Tags to apply to all resources')
param tags object = {
  Environment: environmentName
  Workload: workloadName
  ManagedBy: 'Bicep'
}

// Generate unique resource names
var uniqueSuffix = uniqueString(resourceGroup().id)
var storageAccountName = 'st${workloadName}${uniqueSuffix}'
var documentIntelligenceName = 'di-${workloadName}-${environmentName}'
var cosmosDbAccountName = 'cosmos-${workloadName}-${uniqueSuffix}'
var functionAppName = 'func-${workloadName}-${environmentName}-${uniqueSuffix}'
var appServicePlanName = 'asp-${workloadName}-${environmentName}'
var applicationInsightsName = 'appi-${workloadName}-${environmentName}'
var logAnalyticsWorkspaceName = 'log-${workloadName}-${environmentName}'
var vnetName = 'vnet-${workloadName}-${environmentName}'
var privateDnsZoneNameBlob = 'privatelink.blob.${environment().suffixes.storage}'
var privateDnsZoneNameQueue = 'privatelink.queue.${environment().suffixes.storage}'
var privateDnsZoneNameTable = 'privatelink.table.${environment().suffixes.storage}'
var privateDnsZoneNameCosmos = 'privatelink.documents.azure.com'
var privateDnsZoneNameCognitiveServices = 'privatelink.cognitiveservices.azure.com'
var privateDnsZoneNameSites = 'privatelink.azurewebsites.net'

// Virtual Network and Subnets
module vnet 'modules/vnet.bicep' = {
  name: 'vnet-deployment'
  params: {
    location: location
    vnetName: vnetName
    tags: tags
  }
}

// Private DNS Zones
module privateDnsZones 'modules/privateDnsZones.bicep' = {
  name: 'private-dns-zones-deployment'
  params: {
    vnetId: vnet.outputs.vnetId
    privateDnsZoneNameBlob: privateDnsZoneNameBlob
    privateDnsZoneNameQueue: privateDnsZoneNameQueue
    privateDnsZoneNameTable: privateDnsZoneNameTable
    privateDnsZoneNameCosmos: privateDnsZoneNameCosmos
    privateDnsZoneNameCognitiveServices: privateDnsZoneNameCognitiveServices
    privateDnsZoneNameSites: privateDnsZoneNameSites
    tags: tags
  }
}

// Log Analytics Workspace
module logAnalytics 'modules/logAnalytics.bicep' = {
  name: 'log-analytics-deployment'
  params: {
    location: location
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
    tags: tags
  }
}

// Application Insights
module applicationInsights 'modules/applicationInsights.bicep' = {
  name: 'application-insights-deployment'
  params: {
    location: location
    applicationInsightsName: applicationInsightsName
    logAnalyticsWorkspaceId: logAnalytics.outputs.logAnalyticsWorkspaceId
    tags: tags
  }
}

// Storage Account with Private Endpoints
module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    location: location
    storageAccountName: storageAccountName
    vnetId: vnet.outputs.vnetId
    privateEndpointSubnetId: vnet.outputs.privateEndpointSubnetId
    privateDnsZoneIdBlob: privateDnsZones.outputs.privateDnsZoneIdBlob
    privateDnsZoneIdQueue: privateDnsZones.outputs.privateDnsZoneIdQueue
    privateDnsZoneIdTable: privateDnsZones.outputs.privateDnsZoneIdTable
    tags: tags
  }
}

// Document Intelligence with Private Endpoint
module documentIntelligence 'modules/documentIntelligence.bicep' = {
  name: 'document-intelligence-deployment'
  params: {
    location: location
    documentIntelligenceName: documentIntelligenceName
    vnetId: vnet.outputs.vnetId
    privateEndpointSubnetId: vnet.outputs.privateEndpointSubnetId
    privateDnsZoneId: privateDnsZones.outputs.privateDnsZoneIdCognitiveServices
    tags: tags
  }
}

// Cosmos DB with Private Endpoint
module cosmosDb 'modules/cosmosDb.bicep' = {
  name: 'cosmos-db-deployment'
  params: {
    location: location
    cosmosDbAccountName: cosmosDbAccountName
    vnetId: vnet.outputs.vnetId
    privateEndpointSubnetId: vnet.outputs.privateEndpointSubnetId
    privateDnsZoneId: privateDnsZones.outputs.privateDnsZoneIdCosmos
    tags: tags
  }
}

// App Service Plan (Linux)
module appServicePlan 'modules/appServicePlan.bicep' = {
  name: 'app-service-plan-deployment'
  params: {
    location: location
    appServicePlanName: appServicePlanName
    tags: tags
  }
}

// Azure Function App with Private Endpoint
module functionApp 'modules/functionApp.bicep' = {
  name: 'function-app-deployment'
  params: {
    location: location
    functionAppName: functionAppName
    appServicePlanId: appServicePlan.outputs.appServicePlanId
    storageAccountName: storage.outputs.storageAccountName
    applicationInsightsConnectionString: applicationInsights.outputs.connectionString
    vnetId: vnet.outputs.vnetId
    integrationSubnetId: vnet.outputs.functionIntegrationSubnetId
    privateEndpointSubnetId: vnet.outputs.privateEndpointSubnetId
    privateDnsZoneId: privateDnsZones.outputs.privateDnsZoneIdSites
    documentIntelligenceEndpoint: documentIntelligence.outputs.endpoint
    cosmosDbEndpoint: cosmosDb.outputs.endpoint
    cosmosDbDatabaseName: 'DocumentOcrDb'
    cosmosDbContainerName: 'ProcessedDocuments'
    tags: tags
  }
}

// Role Assignments at Resource Level
module roleAssignments 'modules/roleAssignments.bicep' = {
  name: 'role-assignments-deployment'
  params: {
    functionAppPrincipalId: functionApp.outputs.functionAppPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    documentIntelligenceName: documentIntelligence.outputs.documentIntelligenceName
    cosmosDbAccountName: cosmosDb.outputs.cosmosDbAccountName
  }
}

// Outputs
output storageAccountName string = storage.outputs.storageAccountName
output documentIntelligenceName string = documentIntelligence.outputs.documentIntelligenceName
output documentIntelligenceEndpoint string = documentIntelligence.outputs.endpoint
output cosmosDbAccountName string = cosmosDb.outputs.cosmosDbAccountName
output cosmosDbEndpoint string = cosmosDb.outputs.endpoint
output functionAppName string = functionApp.outputs.functionAppName
output functionAppUrl string = functionApp.outputs.functionAppUrl
output applicationInsightsName string = applicationInsights.outputs.applicationInsightsName
output resourceGroupName string = resourceGroup().name
