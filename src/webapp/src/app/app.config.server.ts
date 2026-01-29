import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { provideApi } from 'src/api/listen-and-write/provide-api';
import { environment } from '../enviroments/enviroment';

// Server-side API URL - uses environment variables if available (for Kubernetes internal service)
// Falls back to build-time environment for local development
const serverApiUrl = process.env['API_URL'] || environment.apiUrl;

console.log(`[SSR] Using API URL: ${serverApiUrl}`);

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    // Override the API URL for server-side rendering
    provideApi(serverApiUrl)
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
