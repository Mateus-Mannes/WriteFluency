import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { importProvidersFrom, provideZoneChangeDetection } from '@angular/core';
import { environment } from './enviroments/enviroment.prod';
import { InsightsModule } from './telemetry/insights.module';
import { appRoutes } from './app/app.routes';

import './telemetry/instrument';

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(appRoutes),
    provideHttpClient(withInterceptorsFromDi()),
    provideZoneChangeDetection(),
    ...(environment.production
      ? [importProvidersFrom(InsightsModule)]
      : []),
  ],
}).catch(err => console.error(err));