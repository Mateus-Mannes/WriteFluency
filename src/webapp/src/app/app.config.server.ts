import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { provideApi } from 'src/api/listen-and-write/provide-api';
import { environment } from '../enviroments/enviroment';

// Server-side API URL - uses Aspire service discovery in Kubernetes
// Aspire sets: services__wf-api__apihttp__0=wf-api:5000
// Falls back to build-time environment for local development
const aspireApiUrl = process.env['services__wf-api__apihttp__0'];
const serverApiUrl = aspireApiUrl ? `http://${aspireApiUrl}` : environment.apiUrl;

console.log(`[SSR] Using API URL: ${serverApiUrl}`);

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    // Override the API URL for server-side rendering
    provideApi(serverApiUrl)
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
