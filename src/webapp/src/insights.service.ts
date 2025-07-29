import { ErrorHandler, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AngularPlugin } from '@microsoft/applicationinsights-angularplugin-js';
import { ApplicationInsights, DistributedTracingModes } from '@microsoft/applicationinsights-web';
import { environment } from './enviroments/enviroment';
 
@Injectable()
export class Insights {
    private angularPlugin = new AngularPlugin();
    private appInsights = new ApplicationInsights({
        config: {
            instrumentationKey: environment.instrumentationKey,
            distributedTracingMode: DistributedTracingModes.AI_AND_W3C,
            enableAutoRouteTracking: true,
            enableCorsCorrelation: true,
            correlationHeaderDomains: ['writefluency.com', 'writefluency.com:8080'],
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
        this.appInsights.addTelemetryInitializer((envelope) => {
            if(!envelope.tags) {
                envelope.tags = {};
            }
            envelope.tags['ai.cloud.role'] = 'wf-webapp'; 
            envelope.tags['ai.cloud.roleInstance'] = 'angular-client'; 
        });
    }
 
    // expose methods that can be used in components and services
    trackEvent(name: string): void {
        this.appInsights.trackEvent({ name });
    }
 
    trackTrace(message: string): void {
        this.appInsights.trackTrace({ message });
    }
}