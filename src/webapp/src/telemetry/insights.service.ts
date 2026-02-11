import { ErrorHandler, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AngularPlugin } from '@microsoft/applicationinsights-angularplugin-js';
import { ApplicationInsights, DistributedTracingModes } from '@microsoft/applicationinsights-web';
import { environment } from '../enviroments/enviroment';
 
@Injectable()
export class Insights {
    private angularPlugin = new AngularPlugin();
    private initialized = false;
    private appInsights = new ApplicationInsights({
        config: {
            instrumentationKey: environment.instrumentationKey,
            distributedTracingMode: DistributedTracingModes.AI_AND_W3C,
            enableAutoRouteTracking: true,
            enableCorsCorrelation: true,
            // Prefer modern lifecycle events to avoid deprecated unload listeners.
            disablePageUnloadEvents: ['unload', 'beforeunload'],
            correlationHeaderDomains: [
                'writefluency.com',
                'api.writefluency.com',
                'writefluency.com:8080',
                'localhost:5000',
            ],
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
        if (!this.shouldEnableTelemetry()) {
            return;
        }

        this.initializeTelemetry();
    }

    private shouldEnableTelemetry(): boolean {
        return (
            typeof window !== 'undefined' &&
            environment.production &&
            Boolean(environment.instrumentationKey)
        );
    }

    private initializeTelemetry(): void {
        if (!this.shouldEnableTelemetry()) {
            return;
        }

        if (this.initialized) {
            return;
        }

        this.initialized = true;

        this.appInsights.addTelemetryInitializer((envelope) => {
            if(!envelope.tags) {
                envelope.tags = {};
            }
            envelope.tags['ai.cloud.role'] = 'wf-webapp'; 
            envelope.tags['ai.cloud.roleInstance'] = 'angular-client'; 
        });

        this.appInsights.loadAppInsights();
    }
 
    // expose methods that can be used in components and services
    trackEvent(name: string): void {
        this.initializeTelemetry();
        this.appInsights.trackEvent({ name });
    }
 
    trackTrace(message: string): void {
        this.initializeTelemetry();
        this.appInsights.trackTrace({ message });
    }
}
