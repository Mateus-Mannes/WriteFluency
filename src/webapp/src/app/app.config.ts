import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptorsFromDi } from '@angular/common/http';
import { ApplicationConfig, importProvidersFrom, provideZoneChangeDetection } from '@angular/core';
import { environment } from '../enviroments/enviroment';
import { InsightsModule } from '../telemetry/insights.module';
import { appRoutes } from './app.routes';
import { provideApi } from 'src/api/listen-and-write/provide-api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(appRoutes),
    provideHttpClient(withInterceptorsFromDi(), withFetch()),
    provideApi(environment.apiUrl),
    provideZoneChangeDetection(),
    ...(environment.production && typeof window !== 'undefined'
      ? [importProvidersFrom(InsightsModule)]
      : []), 
    provideClientHydration(withEventReplay()),
  ],
}
