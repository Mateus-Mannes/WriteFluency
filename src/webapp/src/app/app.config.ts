import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptorsFromDi } from '@angular/common/http';
import { ApplicationConfig, importProvidersFrom, provideZoneChangeDetection } from '@angular/core';
import { environment } from '../enviroments/enviroment';
import { InsightsModule } from '../telemetry/insights.module';
import { appRoutes } from './app.routes';
import { provideApi } from 'src/api/listen-and-write/provide-api';
import { shouldUseAppInsights } from 'src/telemetry/insights.check';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(appRoutes,
      withInMemoryScrolling({ scrollPositionRestoration: 'disabled' })
    ),
    provideHttpClient(withInterceptorsFromDi(), withFetch()),
    provideApi(environment.apiUrl),
    provideZoneChangeDetection(),
    ...(shouldUseAppInsights() && typeof window !== 'undefined'
      ? [importProvidersFrom(InsightsModule)]
      : []), 
    provideClientHydration(withEventReplay()),
  ],
}
