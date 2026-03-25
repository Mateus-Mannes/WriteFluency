import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { HTTP_INTERCEPTORS, provideHttpClient, withFetch, withInterceptorsFromDi } from '@angular/common/http';
import { ApplicationConfig, importProvidersFrom, inject, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
import { environment } from '../enviroments/enviroment';
import { InsightsModule } from '../telemetry/insights.module';
import { appRoutes } from './app.routes';
import { provideApi } from 'src/api/listen-and-write/provide-api';
import { shouldUseAppInsights } from 'src/telemetry/insights.check';
import { SessionCorrelationInterceptor } from './core/interceptors/session-correlation.interceptor';
import { AuthSessionStore } from './auth/services/auth-session.store';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(appRoutes,
      withInMemoryScrolling({ scrollPositionRestoration: 'disabled' })
    ),
    provideHttpClient(withInterceptorsFromDi(), withFetch()),
    { provide: HTTP_INTERCEPTORS, useClass: SessionCorrelationInterceptor, multi: true },
    provideAppInitializer(() => {
      const authSessionStore = inject(AuthSessionStore);
      return authSessionStore.initialize();
    }),
    provideApi(environment.apiUrl),
    provideZoneChangeDetection(),
    ...(shouldUseAppInsights() && typeof window !== 'undefined'
      ? [importProvidersFrom(InsightsModule)]
      : []), 
    provideClientHydration(withEventReplay()),
  ],
}
