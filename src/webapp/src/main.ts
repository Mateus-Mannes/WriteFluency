import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { importProvidersFrom, provideZoneChangeDetection } from '@angular/core';
import { environment } from './enviroments/enviroment';
import { InsightsModule } from './telemetry/insights.module';
import { appRoutes } from './app/app.routes';

import './telemetry/instrument';
import { provideApi } from './api/listen-and-write';

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(appRoutes),
    provideHttpClient(withInterceptorsFromDi()),
    provideApi(environment.apiUrl),
    provideZoneChangeDetection(),
    ...(environment.production
      ? [importProvidersFrom(InsightsModule)]
      : []),
  ],
}).catch(err => console.error(err));