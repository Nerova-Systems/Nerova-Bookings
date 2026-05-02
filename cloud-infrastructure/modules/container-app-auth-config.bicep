param containerAppName string
param tenantId string
param clientId string = ''
param allowedExternalRedirectUrls string[] = []

resource containerApp 'Microsoft.App/containerApps@2025-10-02-preview' existing = {
  name: containerAppName
}

resource authConfig 'Microsoft.App/containerApps/authConfigs@2025-10-02-preview' = {
  parent: containerApp
  name: 'current'
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      unauthenticatedClientAction: 'RedirectToLoginPage'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          openIdIssuer: '${az.environment().authentication.loginEndpoint}${tenantId}/v2.0'
          clientId: clientId
        }
        validation: {
          allowedAudiences: empty(clientId) ? [] : ['api://${clientId}']
        }
      }
    }
    login: {
      preserveUrlFragmentsForLogins: false
      allowedExternalRedirectUrls: allowedExternalRedirectUrls
    }
  }
}
