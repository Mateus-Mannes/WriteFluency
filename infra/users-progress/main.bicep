@description('Primary resource prefix used for progress service infrastructure resources.')
param namePrefix string = 'wf-users-progress'

@description('Azure region for the Americas deployment.')
param eastUsLocation string = 'eastus'

@description('Azure region for the APAC deployment.')
param southeastAsiaLocation string = 'southeastasia'

@description('Function App name in East US.')
param functionAppEastUsName string

@description('Function App name in Southeast Asia.')
param functionAppSoutheastAsiaName string

@description('Storage account name for the East US Function App.')
@minLength(3)
@maxLength(24)
param storageAccountEastUsName string

@description('Storage account name for the Southeast Asia Function App.')
@minLength(3)
@maxLength(24)
param storageAccountSoutheastAsiaName string

@description('Traffic Manager profile name.')
param trafficManagerProfileName string

@description('Traffic Manager DNS label. The resulting FQDN is <label>.trafficmanager.net.')
param trafficManagerDnsRelativeName string

@description('Flex Consumption runtime name.')
@allowed([
  'dotnet-isolated'
])
param functionAppRuntime string = 'dotnet-isolated'

@description('Flex Consumption runtime version.')
param functionAppRuntimeVersion string = '10'

@description('Maximum number of instances per Function App in Flex Consumption.')
@maxValue(1000)
param maximumInstanceCount int = 100

@description('Memory size (MB) for each Flex Consumption instance.')
@allowed([
  512
  2048
  4096
])
param instanceMemoryMB int = 2048

@description('Blob container name used by Function package deployment.')
param deploymentStorageContainerName string = 'app-package'

@description('East US virtual network name for users-progress service.')
param eastUsVnetName string = '${namePrefix}-vnet-eus'

@description('Southeast Asia virtual network name for users-progress service.')
param southeastAsiaVnetName string = '${namePrefix}-vnet-sea'

@description('East US VNet CIDR.')
param eastUsVnetAddressPrefix string = '10.60.0.0/16'

@description('Southeast Asia VNet CIDR.')
param southeastAsiaVnetAddressPrefix string = '10.61.0.0/16'

@description('East US Flex Function integration subnet name.')
param eastUsFunctionSubnetName string = 'functions-integration'

@description('Southeast Asia Flex Function integration subnet name.')
param southeastAsiaFunctionSubnetName string = 'functions-integration'

@description('East US Flex Function integration subnet CIDR.')
param eastUsFunctionSubnetPrefix string = '10.60.1.0/24'

@description('Southeast Asia Flex Function integration subnet CIDR.')
param southeastAsiaFunctionSubnetPrefix string = '10.61.1.0/24'

@description('Cosmos DB account name used by users-progress-service.')
param cosmosAccountName string

@description('Cosmos DB endpoint URL.')
param cosmosEndpoint string

@description('Cosmos database name.')
param cosmosDatabaseName string = 'wf-users-progress'

@description('Base progress container name (namespace suffix applied by app).')
param cosmosProgressContainer string = 'user_progress'

@description('Base attempts container name (namespace suffix applied by app).')
param cosmosAttemptsContainer string = 'user_attempts'

@description('Cosmos namespace used by this environment (prod|local).')
@allowed([
  'prod'
  'local'
])
param cosmosNamespace string = 'prod'

@description('Shared cookie scheme expected by users-progress-service.')
param sharedCookieScheme string = 'Identity.Application'

@description('Shared cookie name expected by users-progress-service.')
param sharedCookieName string = '.AspNetCore.Identity.Application'

@description('Shared Data Protection application name used by users-api and users-progress-service.')
param sharedDataProtectionApplicationName string = 'WriteFluency.SharedAuth'

@description('Users API runtime service principal object id for shared auth infrastructure access.')
param usersApiRuntimePrincipalObjectId string

@description('Storage account name for shared Data Protection key ring.')
@minLength(3)
@maxLength(24)
param sharedDataProtectionStorageAccountName string = 'wfusersdpprod01'

@description('Blob container name for shared Data Protection key ring.')
param sharedDataProtectionBlobContainerName string = 'keys'

@description('Blob name used for Data Protection key ring.')
param sharedDataProtectionBlobName string = 'keyring.xml'

@description('Key Vault name for shared Data Protection key wrapping.')
@minLength(3)
@maxLength(24)
param sharedDataProtectionKeyVaultName string = 'wf-users-dp-kv-prod'

@description('Key Vault key name for shared Data Protection key wrapping.')
param sharedDataProtectionKeyName string = 'dataprotection'

@description('Allowed origins for CSRF origin validation in users-progress-service.')
param corsAllowedOrigins array = [
  'https://writefluency.com'
]

@description('Application Insights connection string used by all users-progress Function Apps.')
param appInsightsConnectionString string

@description('Tags applied to resources.')
param tags object = {
  managedBy: 'github-actions'
  workload: 'users-progress-service'
}

var cosmosSqlDataContributorRoleDefinitionResourceId = '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
var storageBlobDataOwnerRoleDefinitionResourceId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
var storageBlobDataContributorRoleDefinitionResourceId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
var storageQueueDataContributorRoleDefinitionResourceId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var storageTableDataContributorRoleDefinitionResourceId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
var keyVaultCryptoUserRoleDefinitionResourceId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '12338af0-0e69-4776-bea7-57ae8d297424')
var eastUsFunctionSubnetResourceId = resourceId('Microsoft.Network/virtualNetworks/subnets', eastUsVnetName, eastUsFunctionSubnetName)
var southeastAsiaFunctionSubnetResourceId = resourceId('Microsoft.Network/virtualNetworks/subnets', southeastAsiaVnetName, southeastAsiaFunctionSubnetName)

resource flexPlanEastUs 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${namePrefix}-plan-eus'
  location: eastUsLocation
  kind: 'functionapp'
  tags: tags
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource flexPlanSoutheastAsia 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${namePrefix}-plan-sea'
  location: southeastAsiaLocation
  kind: 'functionapp'
  tags: tags
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource vnetEastUs 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: eastUsVnetName
  location: eastUsLocation
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        eastUsVnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: eastUsFunctionSubnetName
        properties: {
          addressPrefix: eastUsFunctionSubnetPrefix
          serviceEndpoints: [
            {
              service: 'Microsoft.AzureCosmosDB'
            }
          ]
          delegations: [
            {
              name: 'function-flex-delegation'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
    ]
  }
}

resource vnetSoutheastAsia 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: southeastAsiaVnetName
  location: southeastAsiaLocation
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        southeastAsiaVnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: southeastAsiaFunctionSubnetName
        properties: {
          addressPrefix: southeastAsiaFunctionSubnetPrefix
          serviceEndpoints: [
            {
              service: 'Microsoft.AzureCosmosDB'
            }
          ]
          delegations: [
            {
              name: 'function-flex-delegation'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
    ]
  }
}

resource storageEastUs 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: toLower(storageAccountEastUsName)
  location: eastUsLocation
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource storageSoutheastAsia 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: toLower(storageAccountSoutheastAsiaName)
  location: southeastAsiaLocation
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource deploymentContainerEastUs 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageEastUs.name}/default/${deploymentStorageContainerName}'
  properties: {
    publicAccess: 'None'
  }
}

resource deploymentContainerSoutheastAsia 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageSoutheastAsia.name}/default/${deploymentStorageContainerName}'
  properties: {
    publicAccess: 'None'
  }
}

resource sharedDataProtectionStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: toLower(sharedDataProtectionStorageAccountName)
  location: eastUsLocation
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource sharedDataProtectionContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${sharedDataProtectionStorage.name}/default/${sharedDataProtectionBlobContainerName}'
  properties: {
    publicAccess: 'None'
  }
}

resource sharedDataProtectionKeyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: sharedDataProtectionKeyVaultName
  location: eastUsLocation
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enabledForDiskEncryption: false
    enabledForDeployment: false
    enabledForTemplateDeployment: false
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Enabled'
  }
}

resource sharedDataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: sharedDataProtectionKeyVault
  name: sharedDataProtectionKeyName
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: [
      'wrapKey'
      'unwrapKey'
    ]
  }
}

var sharedDataProtectionBlobUri = 'https://${sharedDataProtectionStorage.name}.blob.${environment().suffixes.storage}/${sharedDataProtectionBlobContainerName}/${sharedDataProtectionBlobName}'
var sharedDataProtectionKeyIdentifier = '${sharedDataProtectionKeyVault.properties.vaultUri}keys/${sharedDataProtectionKeyName}'

var appSettingsBase = [
  {
    name: 'Cosmos__Endpoint'
    value: cosmosEndpoint
  }
  {
    name: 'Cosmos__DatabaseName'
    value: cosmosDatabaseName
  }
  {
    name: 'Cosmos__ProgressContainer'
    value: cosmosProgressContainer
  }
  {
    name: 'Cosmos__AttemptsContainer'
    value: cosmosAttemptsContainer
  }
  {
    name: 'Cosmos__Namespace'
    value: cosmosNamespace
  }
  {
    name: 'SharedAuthCookie__Scheme'
    value: sharedCookieScheme
  }
  {
    name: 'SharedAuthCookie__CookieName'
    value: sharedCookieName
  }
  {
    name: 'SharedDataProtection__ApplicationName'
    value: sharedDataProtectionApplicationName
  }
  {
    name: 'SharedDataProtection__BlobUri'
    value: sharedDataProtectionBlobUri
  }
  {
    name: 'SharedDataProtection__KeyIdentifier'
    value: sharedDataProtectionKeyIdentifier
  }
]

var corsAppSettings = [for (origin, index) in corsAllowedOrigins: {
  name: 'Cors__AllowedOrigins__${index}'
  value: origin
}]

var telemetryTuningAppSettings = [
  {
    name: 'AzureFunctionsJobHost__logging__logLevel__default'
    value: 'Warning'
  }
  {
    name: 'AzureFunctionsJobHost__logging__logLevel__Host'
    value: 'Warning'
  }
  {
    name: 'AzureFunctionsJobHost__logging__logLevel__Host.Aggregator'
    value: 'Error'
  }
  {
    name: 'AzureFunctionsJobHost__logging__logLevel__Host.Results'
    value: 'Information'
  }
  {
    name: 'AzureFunctionsJobHost__logging__logLevel__Function'
    value: 'Information'
  }
  {
    name: 'AzureFunctionsJobHost__logging__logLevel__Function.UsersProgressHealth'
    value: 'Error'
  }
  {
    name: 'AzureFunctionsJobHost__logging__logLevel__Function.UsersProgressHealth.User'
    value: 'Error'
  }
  {
    name: 'AzureFunctionsJobHost__logging__logLevel__Microsoft.AspNetCore.Mvc.StatusCodeResult'
    value: 'Warning'
  }
  {
    name: 'AzureFunctionsJobHost__logging__logLevel__Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService'
    value: 'Warning'
  }
  {
    name: 'AzureFunctionsJobHost__logging__logLevel__Microsoft.EntityFrameworkCore.Database.Command'
    value: 'Warning'
  }
  {
    name: 'AzureFunctionsJobHost__logging__applicationInsights__enableDependencyTracking'
    value: 'true'
  }
  {
    name: 'AzureFunctionsJobHost__logging__applicationInsights__enablePerformanceCountersCollection'
    value: 'true'
  }
  {
    name: 'ApplicationInsights__EnableDependencyTrackingTelemetryModule'
    value: 'true'
  }
  {
    name: 'ApplicationInsights__EnablePerformanceCounterCollectionModule'
    value: 'true'
  }
]

resource functionAppEastUs 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppEastUsName
  location: eastUsLocation
  kind: 'functionapp,linux'
  dependsOn: [
    deploymentContainerEastUs
    vnetEastUs
  ]
  identity: {
    type: 'SystemAssigned'
  }
  tags: tags
  properties: {
    serverFarmId: flexPlanEastUs.id
    httpsOnly: true
    clientAffinityEnabled: false
    virtualNetworkSubnetId: eastUsFunctionSubnetResourceId
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageEastUs.properties.primaryEndpoints.blob}${deploymentStorageContainerName}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      runtime: {
        name: functionAppRuntime
        version: functionAppRuntimeVersion
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumInstanceCount
        instanceMemoryMB: instanceMemoryMB
      }
    }
    siteConfig: {
      vnetRouteAllEnabled: true
      cors: {
        allowedOrigins: corsAllowedOrigins
        supportCredentials: true
      }
      appSettings: concat(appSettingsBase, corsAppSettings, telemetryTuningAppSettings, [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageEastUs.name
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
      ])
    }
  }
}

resource functionAppSoutheastAsia 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppSoutheastAsiaName
  location: southeastAsiaLocation
  kind: 'functionapp,linux'
  dependsOn: [
    deploymentContainerSoutheastAsia
    vnetSoutheastAsia
  ]
  identity: {
    type: 'SystemAssigned'
  }
  tags: tags
  properties: {
    serverFarmId: flexPlanSoutheastAsia.id
    httpsOnly: true
    clientAffinityEnabled: false
    virtualNetworkSubnetId: southeastAsiaFunctionSubnetResourceId
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageSoutheastAsia.properties.primaryEndpoints.blob}${deploymentStorageContainerName}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      runtime: {
        name: functionAppRuntime
        version: functionAppRuntimeVersion
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumInstanceCount
        instanceMemoryMB: instanceMemoryMB
      }
    }
    siteConfig: {
      vnetRouteAllEnabled: true
      cors: {
        allowedOrigins: corsAllowedOrigins
        supportCredentials: true
      }
      appSettings: concat(appSettingsBase, corsAppSettings, telemetryTuningAppSettings, [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageSoutheastAsia.name
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
      ])
    }
  }
}

resource functionStorageBlobAccessEastUs 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageEastUs
  name: guid(storageEastUs.id, functionAppEastUs.name, storageBlobDataOwnerRoleDefinitionResourceId)
  properties: {
    principalId: functionAppEastUs.identity.principalId
    roleDefinitionId: storageBlobDataOwnerRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource functionStorageQueueAccessEastUs 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageEastUs
  name: guid(storageEastUs.id, functionAppEastUs.name, storageQueueDataContributorRoleDefinitionResourceId)
  properties: {
    principalId: functionAppEastUs.identity.principalId
    roleDefinitionId: storageQueueDataContributorRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource functionStorageTableAccessEastUs 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageEastUs
  name: guid(storageEastUs.id, functionAppEastUs.name, storageTableDataContributorRoleDefinitionResourceId)
  properties: {
    principalId: functionAppEastUs.identity.principalId
    roleDefinitionId: storageTableDataContributorRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource functionStorageBlobAccessSoutheastAsia 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageSoutheastAsia
  name: guid(storageSoutheastAsia.id, functionAppSoutheastAsia.name, storageBlobDataOwnerRoleDefinitionResourceId)
  properties: {
    principalId: functionAppSoutheastAsia.identity.principalId
    roleDefinitionId: storageBlobDataOwnerRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource functionStorageQueueAccessSoutheastAsia 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageSoutheastAsia
  name: guid(storageSoutheastAsia.id, functionAppSoutheastAsia.name, storageQueueDataContributorRoleDefinitionResourceId)
  properties: {
    principalId: functionAppSoutheastAsia.identity.principalId
    roleDefinitionId: storageQueueDataContributorRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource functionStorageTableAccessSoutheastAsia 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageSoutheastAsia
  name: guid(storageSoutheastAsia.id, functionAppSoutheastAsia.name, storageTableDataContributorRoleDefinitionResourceId)
  properties: {
    principalId: functionAppSoutheastAsia.identity.principalId
    roleDefinitionId: storageTableDataContributorRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource sharedDataProtectionBlobAccessEastUs 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sharedDataProtectionStorage
  name: guid(sharedDataProtectionStorage.id, functionAppEastUs.name, storageBlobDataContributorRoleDefinitionResourceId)
  properties: {
    principalId: functionAppEastUs.identity.principalId
    roleDefinitionId: storageBlobDataContributorRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource sharedDataProtectionBlobAccessSoutheastAsia 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sharedDataProtectionStorage
  name: guid(sharedDataProtectionStorage.id, functionAppSoutheastAsia.name, storageBlobDataContributorRoleDefinitionResourceId)
  properties: {
    principalId: functionAppSoutheastAsia.identity.principalId
    roleDefinitionId: storageBlobDataContributorRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource sharedDataProtectionBlobAccessUsersApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sharedDataProtectionStorage
  name: guid(sharedDataProtectionStorage.id, usersApiRuntimePrincipalObjectId, storageBlobDataContributorRoleDefinitionResourceId)
  properties: {
    principalId: usersApiRuntimePrincipalObjectId
    roleDefinitionId: storageBlobDataContributorRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource sharedDataProtectionKeyVaultAccessEastUs 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sharedDataProtectionKeyVault
  name: guid(sharedDataProtectionKeyVault.id, functionAppEastUs.name, keyVaultCryptoUserRoleDefinitionResourceId)
  properties: {
    principalId: functionAppEastUs.identity.principalId
    roleDefinitionId: keyVaultCryptoUserRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource sharedDataProtectionKeyVaultAccessSoutheastAsia 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sharedDataProtectionKeyVault
  name: guid(sharedDataProtectionKeyVault.id, functionAppSoutheastAsia.name, keyVaultCryptoUserRoleDefinitionResourceId)
  properties: {
    principalId: functionAppSoutheastAsia.identity.principalId
    roleDefinitionId: keyVaultCryptoUserRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource sharedDataProtectionKeyVaultAccessUsersApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sharedDataProtectionKeyVault
  name: guid(sharedDataProtectionKeyVault.id, usersApiRuntimePrincipalObjectId, keyVaultCryptoUserRoleDefinitionResourceId)
  properties: {
    principalId: usersApiRuntimePrincipalObjectId
    roleDefinitionId: keyVaultCryptoUserRoleDefinitionResourceId
    principalType: 'ServicePrincipal'
  }
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-09-15' existing = {
  name: cosmosAccountName
}

resource cosmosRoleAssignmentEastUs 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-09-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, functionAppEastUs.name, cosmosSqlDataContributorRoleDefinitionResourceId)
  properties: {
    principalId: functionAppEastUs.identity.principalId
    roleDefinitionId: cosmosSqlDataContributorRoleDefinitionResourceId
    scope: cosmosAccount.id
  }
}

resource cosmosRoleAssignmentSoutheastAsia 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-09-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, functionAppSoutheastAsia.name, cosmosSqlDataContributorRoleDefinitionResourceId)
  properties: {
    principalId: functionAppSoutheastAsia.identity.principalId
    roleDefinitionId: cosmosSqlDataContributorRoleDefinitionResourceId
    scope: cosmosAccount.id
  }
}

resource trafficManagerProfile 'Microsoft.Network/trafficManagerProfiles@2022-04-01' = {
  name: trafficManagerProfileName
  location: 'global'
  tags: tags
  properties: {
    profileStatus: 'Enabled'
    trafficRoutingMethod: 'Performance'
    dnsConfig: {
      relativeName: trafficManagerDnsRelativeName
      ttl: 30
    }
    monitorConfig: {
      protocol: 'TCP'
      port: 443
      path: ''
      intervalInSeconds: 30
      timeoutInSeconds: 10
      toleratedNumberOfFailures: 3
    }
  }
}

resource trafficManagerEndpointEastUs 'Microsoft.Network/trafficManagerProfiles/azureEndpoints@2022-04-01' = {
  parent: trafficManagerProfile
  name: 'eastus-endpoint'
  properties: {
    targetResourceId: functionAppEastUs.id
    endpointStatus: 'Enabled'
    endpointLocation: eastUsLocation
  }
}

resource trafficManagerEndpointSoutheastAsia 'Microsoft.Network/trafficManagerProfiles/azureEndpoints@2022-04-01' = {
  parent: trafficManagerProfile
  name: 'southeastasia-endpoint'
  properties: {
    targetResourceId: functionAppSoutheastAsia.id
    endpointStatus: 'Enabled'
    endpointLocation: southeastAsiaLocation
  }
}

output functionAppEastUsDefaultHostName string = functionAppEastUs.properties.defaultHostName
output functionAppSoutheastAsiaDefaultHostName string = functionAppSoutheastAsia.properties.defaultHostName
output trafficManagerFqdn string = '${trafficManagerDnsRelativeName}.trafficmanager.net'
output trafficManagerProfileId string = trafficManagerProfile.id
output eastUsPrincipalId string = functionAppEastUs.identity.principalId
output southeastAsiaPrincipalId string = functionAppSoutheastAsia.identity.principalId
output sharedDataProtectionBlobUri string = sharedDataProtectionBlobUri
output sharedDataProtectionKeyIdentifier string = sharedDataProtectionKeyIdentifier
