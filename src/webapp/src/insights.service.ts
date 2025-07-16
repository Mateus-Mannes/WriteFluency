import { ErrorHandler, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AngularPlugin } from '@microsoft/applicationinsights-angularplugin-js';
import { ApplicationInsights } from '@microsoft/applicationinsights-web';
import { environment } from './enviroments/enviroment';
 
@Injectable()
export class Insights {
    private angularPlugin = new AngularPlugin();
    private appInsights = new ApplicationInsights({
        config: {
            instrumentationKey: environment.instrumentationKey,
            extensions: [this.angularPlugin],
            extensionConfig: {
                [this.angularPlugin.identifier]: {
                    router: this.router,
                    errorServices: [new ErrorHandler()],
                },
            },
        },
    });
 
    constructor(private router: Router) {
        this.appInsights.loadAppInsights();
    }
 
    // expose methods that can be used in components and services
    trackEvent(name: string): void {
        this.appInsights.trackEvent({ name });
    }
 
    trackTrace(message: string): void {
        this.appInsights.trackTrace({ message });
    }
}