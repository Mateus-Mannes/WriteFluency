import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { provideApi } from 'src/api/listen-and-write/provide-api';
import { environment } from '../enviroments/enviroment';

// Server-side API URL - uses Aspire service discovery in Kubernetes
// Aspire sets: services__wf-api__http__0=http://wf-api:8080
// Falls back to build-time environment for local development
const aspireApiUrl = process.env['services__wf-api__http__0'];
const serverApiUrl = aspireApiUrl || environment.apiUrl;

console.log(`[SSR] Using API URL: ${serverApiUrl}`);

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    // Override the API URL for server-side rendering
    provideApi(serverApiUrl)
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
