import { enableProdMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { environment } from './enviroments/enviroment';

if (environment.production) {
  enableProdMode();
}

if (!environment.production) {
  void import('./telemetry/instrument');
}

bootstrapApplication(AppComponent, appConfig).catch(err => console.error(err));
