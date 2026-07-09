import { ErrorHandler, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AngularPlugin } from '@microsoft/applicationinsights-angularplugin-js';
import { ApplicationInsights, DistributedTracingModes, ITelemetryItem } from '@microsoft/applicationinsights-web';
import { environment } from '../enviroments/enviroment';
import { shouldUseAppInsights } from './insights.check';

export type InsightsProperties = Record<string, string>;
export type InsightsMeasurements = Record<string, number>;
export interface InsightsExceptionOptions {
    properties?: InsightsProperties;
    measurements?: InsightsMeasurements;
    severityLevel?: number;
}

type ExceptionTelemetryDetails = {
    typeName?: string;
    message?: string;
    rawStack?: string;
};

type ExceptionTelemetryData = {
    exceptions?: ExceptionTelemetryDetails[];
    properties?: Record<string, unknown>;
};

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
                'localhost:5050',
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
            if (this.shouldDropNoiseException(envelope)) {
                return false;
            }

            if (!envelope.tags) {
                envelope.tags = {};
            }
            envelope.tags['ai.cloud.role'] = 'wf-webapp'; 
            envelope.tags['ai.cloud.roleInstance'] = 'angular-client'; 

            return true;
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

    trackException(
        error: unknown,
        options: InsightsExceptionOptions = {}
    ): void {
        if (!this.canTrack()) {
            return;
        }

        this.appInsights.trackException({
            exception: this.toError(error),
            severityLevel: options.severityLevel,
            properties: options.properties,
            measurements: options.measurements,
        });
    }

    private shouldDropNoiseException(envelope: ITelemetryItem): boolean {
        if (envelope.baseType !== 'ExceptionData') {
            return false;
        }

        const exceptionData = envelope.baseData as ExceptionTelemetryData | undefined;
        const exceptionDetails = exceptionData?.exceptions ?? [];
        const properties = exceptionData?.properties ?? {};
        const searchableText = [
            envelope.name,
            ...exceptionDetails.flatMap((exception) => [
                exception.typeName,
                exception.message,
                exception.rawStack,
            ]),
            ...Object.values(properties).map((value) => String(value)),
        ].filter(Boolean).join(' ');

        return (
            searchableText.includes('Failed to fetch dynamically imported module') ||
            searchableText.includes('ResizeObserver loop completed with undelivered notifications') ||
            searchableText.includes('ResizeObserver loop limit exceeded') ||
            searchableText.includes('NG0750') ||
            (
                properties['operation'] === 'load_user_progress' &&
                String(properties['http_status']) === '0'
            )
        );
    }

    private toError(error: unknown): Error {
        if (error instanceof Error) {
            return error;
        }

        if (typeof error === 'string') {
            return new Error(error);
        }

        try {
            return new Error(JSON.stringify(error));
        } catch {
            return new Error('Unknown error');
        }
    }

}
