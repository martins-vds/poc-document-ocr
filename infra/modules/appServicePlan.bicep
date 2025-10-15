@description('The location for the App Service Plan')
param location string

@description('The name of the App Service Plan')
param appServicePlanName string

@description('Tags to apply to the resource')
param tags object

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'P1v3'
    tier: 'PremiumV3'
    size: 'P1v3'
    family: 'Pv3'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

output appServicePlanId string = appServicePlan.id
output appServicePlanName string = appServicePlan.name
