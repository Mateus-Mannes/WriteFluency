@description('Azure Cosmos DB account name. Must be globally unique, lowercase, and 3-44 chars.')
@minLength(3)
@maxLength(44)
param accountName string

@description('Cosmos DB SQL database name used for users progress tracking.')
param databaseName string = 'wf-users-progress'

@description('Shared throughput for the SQL database (manual RU/s).')
@minValue(400)
param databaseThroughput int = 500

@description('Ordered write/read regions for Cosmos DB multi-region writes.')
param failoverRegions array = [
  'East US'
  'Southeast Asia'
]

@description('Enable Cosmos DB free tier. Only one free-tier account is allowed per subscription.')
param enableFreeTier bool = true

@description('Account-wide throughput cap (RU/s). Use -1 to disable the cap.')
@minValue(-1)
param totalThroughputLimit int = 1000

@description('Periodic backup interval in minutes.')
@minValue(60)
@maxValue(1440)
param backupIntervalInMinutes int = 240

@description('Periodic backup retention in hours.')
@minValue(8)
@maxValue(720)
param backupRetentionIntervalInHours int = 8

@description('Periodic backup redundancy for account snapshots.')
@allowed([
  'Geo'
  'Local'
  'Zone'
])
param backupStorageRedundancy string = 'Geo'

@description('Public network access mode for Cosmos DB.')
@allowed([
  'Enabled'
  'Disabled'
  'SecuredByPerimeter'
])
param publicNetworkAccess string = 'Enabled'

@description('Subnet resource IDs allowed to access Cosmos DB through virtual network service endpoints.')
param allowedSubnetResourceIds array = []

@description('Public IP ranges allowed to access Cosmos DB. Leave empty to block direct public IP access.')
param allowedIpRanges array = []

@description('Resource ID of the Log Analytics workspace receiving Cosmos diagnostic logs. Leave empty to skip diagnostics.')
param logAnalyticsWorkspaceResourceId string = ''

@description('Diagnostic setting name for Cosmos DB logs.')
param diagnosticsSettingName string = 'cosmos-diagnostics'

@description('Email for Azure Monitor action group alerts (RU, throttling, latency, failures). Leave empty to skip alert creation.')
param alertEmailAddress string = ''

@description('Name for the Azure Monitor action group.')
param actionGroupName string = 'wf-cosmos-alerts'

@description('Short name for the Azure Monitor action group (max 12 chars).')
@maxLength(12)
param actionGroupShortName string = 'wfcosmos'

@description('Threshold (percent) for normalized RU alert.')
@minValue(1)
@maxValue(100)
param normalizedRuAlertThreshold int = 80

@description('Threshold (count/window) for throttled request alert (HTTP 429).')
@minValue(1)
param throttledRequestThreshold int = 10

@description('Threshold (ms) for server-side gateway latency alert.')
@minValue(1)
param serverSideLatencyGatewayThresholdMs int = 500

@description('Threshold (count/window) for server error alert (HTTP 5xx).')
@minValue(1)
param serverErrorRequestThreshold int = 10

@description('Monthly budget name for Cosmos costs.')
param budgetName string = 'wf-cosmos-monthly-budget'

@description('Monthly cost budget amount (billing currency). Leave email empty to skip budget creation.')
@minValue(1)
param monthlyBudgetAmount int = 1

@description('Email to receive budget notifications. Leave empty to skip budget creation.')
param budgetNotificationEmail string = ''

@description('Budget start date (must be first day of a month).')
param budgetStartDate string = utcNow('yyyy-MM-01T00:00:00Z')

@description('Tags applied to Cosmos and monitor resources.')
param tags object = {
  managedBy: 'github-actions'
  workload: 'users-progress-service'
  component: 'cosmos-progress-tracking'
}

var primaryAzureLocation = replace(toLower(failoverRegions[0]), ' ', '')
var containerNames = [
  'user_progress_prod'
  'user_attempts_prod'
  'user_progress_local'
  'user_attempts_local'
]
var hasDiagnosticsWorkspace = !empty(logAnalyticsWorkspaceResourceId)
var shouldCreateAlerts = !empty(alertEmailAddress)
var shouldCreateBudget = !empty(budgetNotificationEmail)

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-09-15' = {
  name: accountName
  location: primaryAzureLocation
  kind: 'GlobalDocumentDB'
  tags: tags
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [for (regionName, index) in failoverRegions: {
      locationName: regionName
      failoverPriority: index
      isZoneRedundant: false
    }]
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: backupIntervalInMinutes
        backupRetentionIntervalInHours: backupRetentionIntervalInHours
        backupStorageRedundancy: backupStorageRedundancy
      }
    }
    capacity: {
      totalThroughputLimit: totalThroughputLimit
    }
    minimalTlsVersion: 'Tls12'
    enableAutomaticFailover: true
    enableMultipleWriteLocations: true
    enableFreeTier: enableFreeTier
    disableLocalAuth: true
    disableKeyBasedMetadataWriteAccess: true
    publicNetworkAccess: publicNetworkAccess
    networkAclBypass: 'None'
    isVirtualNetworkFilterEnabled: length(allowedSubnetResourceIds) > 0
    virtualNetworkRules: [for subnetId in allowedSubnetResourceIds: {
      id: subnetId
      ignoreMissingVNetServiceEndpoint: false
    }]
    ipRules: [for ipRange in allowedIpRanges: {
      ipAddressOrRange: ipRange
    }]
  }
}

resource progressDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-09-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
    options: {
      throughput: databaseThroughput
    }
  }
}

resource progressContainers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-09-15' = [for containerName in containerNames: {
  parent: progressDatabase
  name: containerName
  properties: {
    resource: {
      id: containerName
      partitionKey: {
        kind: 'Hash'
        paths: [
          '/userId'
        ]
        version: 2
      }
    }
    options: {}
  }
}]

resource cosmosDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (hasDiagnosticsWorkspace) {
  name: diagnosticsSettingName
  scope: cosmosAccount
  properties: {
    workspaceId: logAnalyticsWorkspaceResourceId
    logAnalyticsDestinationType: 'Dedicated'
    logs: [
      {
        category: 'ControlPlaneRequests'
        enabled: true
      }
      {
        category: 'DataPlaneRequests'
        enabled: true
      }
      {
        category: 'QueryRuntimeStatistics'
        enabled: true
      }
      {
        category: 'PartitionKeyRUConsumption'
        enabled: true
      }
      {
        category: 'PartitionKeyStatistics'
        enabled: true
      }
    ]
  }
}

resource cosmosActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (shouldCreateAlerts) {
  name: actionGroupName
  location: 'global'
  tags: tags
  properties: {
    enabled: true
    groupShortName: actionGroupShortName
    emailReceivers: [
      {
        name: 'cosmos-alert-email'
        emailAddress: alertEmailAddress
        useCommonAlertSchema: true
      }
    ]
  }
}

resource normalizedRuAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (shouldCreateAlerts) {
  name: '${accountName}-normalized-ru-alert'
  location: 'global'
  tags: tags
  properties: {
    description: 'Cosmos normalized RU consumption is high.'
    severity: 2
    enabled: true
    scopes: [
      cosmosAccount.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'NormalizedRUConsumptionHigh'
          metricNamespace: 'Microsoft.DocumentDB/databaseAccounts'
          metricName: 'NormalizedRUConsumption'
          operator: 'GreaterThan'
          threshold: normalizedRuAlertThreshold
          timeAggregation: 'Maximum'
        }
      ]
    }
    actions: [
      {
        actionGroupId: cosmosActionGroup.id
      }
    ]
  }
}

resource throttlingAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (shouldCreateAlerts) {
  name: '${accountName}-throttling-alert'
  location: 'global'
  tags: tags
  properties: {
    description: 'Cosmos throttled requests (429) crossed the configured threshold.'
    severity: 2
    enabled: true
    scopes: [
      cosmosAccount.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'ThrottledRequestsHigh'
          metricNamespace: 'Microsoft.DocumentDB/databaseAccounts'
          metricName: 'TotalRequests'
          operator: 'GreaterThan'
          threshold: throttledRequestThreshold
          timeAggregation: 'Count'
          dimensions: [
            {
              name: 'StatusCode'
              operator: 'Include'
              values: [
                '429'
              ]
            }
          ]
        }
      ]
    }
    actions: [
      {
        actionGroupId: cosmosActionGroup.id
      }
    ]
  }
}

resource latencyAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (shouldCreateAlerts) {
  name: '${accountName}-latency-alert'
  location: 'global'
  tags: tags
  properties: {
    description: 'Cosmos server-side gateway latency is above the configured threshold.'
    severity: 3
    enabled: true
    scopes: [
      cosmosAccount.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'ServerSideLatencyGatewayHigh'
          metricNamespace: 'Microsoft.DocumentDB/databaseAccounts'
          metricName: 'ServerSideLatencyGateway'
          operator: 'GreaterThan'
          threshold: serverSideLatencyGatewayThresholdMs
          timeAggregation: 'Average'
        }
      ]
    }
    actions: [
      {
        actionGroupId: cosmosActionGroup.id
      }
    ]
  }
}

resource failuresAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (shouldCreateAlerts) {
  name: '${accountName}-server-errors-alert'
  location: 'global'
  tags: tags
  properties: {
    description: 'Cosmos server-side failures (HTTP 500/503) crossed the configured threshold.'
    severity: 1
    enabled: true
    scopes: [
      cosmosAccount.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'ServerErrorsHigh'
          metricNamespace: 'Microsoft.DocumentDB/databaseAccounts'
          metricName: 'TotalRequests'
          operator: 'GreaterThan'
          threshold: serverErrorRequestThreshold
          timeAggregation: 'Count'
          dimensions: [
            {
              name: 'StatusCode'
              operator: 'Include'
              values: [
                '500'
                '503'
              ]
            }
          ]
        }
      ]
    }
    actions: [
      {
        actionGroupId: cosmosActionGroup.id
      }
    ]
  }
}

resource cosmosBudget 'Microsoft.Consumption/budgets@2024-08-01' = if (shouldCreateBudget) {
  name: budgetName
  properties: {
    amount: monthlyBudgetAmount
    category: 'Cost'
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
    }
    notifications: {
      actual80: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [
          budgetNotificationEmail
        ]
        contactGroups: shouldCreateAlerts ? [
          cosmosActionGroup.id
        ] : []
      }
      actual100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [
          budgetNotificationEmail
        ]
        contactGroups: shouldCreateAlerts ? [
          cosmosActionGroup.id
        ] : []
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [
          budgetNotificationEmail
        ]
        contactGroups: shouldCreateAlerts ? [
          cosmosActionGroup.id
        ] : []
      }
    }
  }
}

output accountId string = cosmosAccount.id
output endpoint string = cosmosAccount.properties.documentEndpoint
output configuredDatabaseName string = progressDatabase.name
output configuredContainerNames array = [for containerName in containerNames: containerName]
output alertActionGroupId string = shouldCreateAlerts ? cosmosActionGroup.id : ''
