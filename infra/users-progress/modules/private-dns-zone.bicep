@description('Private DNS zone name.')
param privateDnsZoneName string = 'privatelink.documents.azure.com'

@description('Virtual network resource ID linked to the private DNS zone.')
param virtualNetworkResourceId string

@description('Virtual network link name in the private DNS zone.')
param virtualNetworkLinkName string

@description('Tags applied to DNS resources.')
param tags object = {}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: privateDnsZoneName
  location: 'global'
  tags: tags
}

resource privateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZone
  name: virtualNetworkLinkName
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetworkResourceId
    }
  }
}

output privateDnsZoneId string = privateDnsZone.id
