import { ErrorHandler, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AngularPlugin } from '@microsoft/applicationinsights-angularplugin-js';
import { ApplicationInsights, DistributedTracingModes } from '@microsoft/applicationinsights-web';
import { environment } from '../enviroments/enviroment';
import { shouldUseAppInsights } from './insights.check';

export type InsightsProperties = Record<string, string>;
export type InsightsMeasurements = Record<string, number>;

@Injectable()
export class Insights {
    private angularPlugin = new AngularPlugin();
    private initialized = false;
    private currentOperationId: string | null = null;
    private currentOperationParentId: string | null = null;
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
            shouldUseAppInsights() &&
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

            if (this.currentOperationId) {
                envelope.tags['ai.operation.id'] = this.currentOperationId;
            }

            if (this.currentOperationParentId) {
                envelope.tags['ai.operation.parentId'] = this.currentOperationParentId;
            }
        });

        this.appInsights.loadAppInsights();
    }

    private canTrack(): boolean {
        this.initializeTelemetry();
        return this.initialized;
    }
 
    // expose methods that can be used in components and services
    trackEvent(
        name: string,
        properties?: InsightsProperties,
        measurements?: InsightsMeasurements
    ): void {
        if (!this.canTrack()) {
            return;
        }

        this.appInsights.trackEvent({ name, properties, measurements });
    }

    trackTrace(
        message: string,
        properties?: InsightsProperties,
        measurements?: InsightsMeasurements
    ): void {
        if (!this.canTrack()) {
            return;
        }

        this.appInsights.trackTrace({ message, properties, measurements });
    }

    setOperationContext(operationId: string | null, operationParentId: string | null = null): void {
        this.currentOperationId = operationId;
        this.currentOperationParentId = operationParentId;
    }
}
