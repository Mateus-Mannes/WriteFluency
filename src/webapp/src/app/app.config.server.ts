import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';

// SSR now uses the same external API URL as the browser (from environment)
// CoreDNS rewrite rule in Kubernetes will resolve api.writefluency.com to internal service
// This ensures Angular transfer cache works correctly (no duplicate HTTP requests)

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes))
    // No API URL override - uses the same provideApi from appConfig
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
