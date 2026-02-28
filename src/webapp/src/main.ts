import { enableProdMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { environment } from './enviroments/enviroment';
import { shouldUseAppInsights } from './telemetry/insights.check';

if (environment.production) {
  enableProdMode();
}

if (!shouldUseAppInsights()) {
  void import('./telemetry/instrument');
}

bootstrapApplication(AppComponent, appConfig).catch(err => console.error(err));
