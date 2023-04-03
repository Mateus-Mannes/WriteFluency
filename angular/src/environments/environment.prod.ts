import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

export const environment = {
  production: true,
  application: {
    baseUrl,
    name: 'WriteFluency',
    logoUrl: '',
  },
  oAuthConfig: {
    issuer: 'https://localhost:44366/',
    redirectUri: baseUrl,
    clientId: 'WriteFluency_App',
    responseType: 'code',
    scope: 'offline_access WriteFluency',
    requireHttps: true
  },
  apis: {
    default: {
      url: 'https://localhost:44366',
      rootNamespace: 'WriteFluency',
    },
  },
} as Environment;
