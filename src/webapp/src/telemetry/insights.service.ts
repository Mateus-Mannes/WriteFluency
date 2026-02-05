import { ErrorHandler, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { environment } from '../enviroments/enviroment';

type MinimalAppInsights = {
    addTelemetryInitializer: (cb: (envelope: { tags?: Record<string, string> }) => void) => void;
    loadAppInsights: () => void;
    trackEvent: (event: { name: string }) => void;
    trackTrace: (trace: { message: string }) => void;
};

@Injectable({ providedIn: 'root' })
export class Insights {
    private initialized = false;
    private initializePromise: Promise<void> | null = null;
    private appInsights: MinimalAppInsights | null = null;

    constructor(private router: Router) {
        if (typeof window === 'undefined' || !environment.production) {
            return;
        }

        // Defer telemetry startup so it does not compete with LCP-critical resources.
        window.setTimeout(() => {
            void this.initializeTelemetry().catch(() => undefined);
        }, 4000);
    }

    private initializeTelemetry(): Promise<void> {
        if (typeof window === 'undefined' || !environment.production || this.initialized) {
            return Promise.resolve();
        }

        if (!this.initializePromise) {
            this.initializePromise = this.loadAndStartTelemetry().catch((error) => {
                this.initializePromise = null;
                throw error;
            });
        }

        return this.initializePromise;
    }

    private async loadAndStartTelemetry(): Promise<void> {
        const [{ AngularPlugin }, { ApplicationInsights, DistributedTracingModes }] = await Promise.all([
            import('@microsoft/applicationinsights-angularplugin-js'),
            import('@microsoft/applicationinsights-web'),
        ]);

        const angularPlugin = new AngularPlugin();
        const appInsights = new ApplicationInsights({
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
                extensions: [angularPlugin],
                extensionConfig: {
                    [angularPlugin.identifier]: {
                        router: this.router,
                        errorServices: [new ErrorHandler()],
                    },
                },
            },
        });

        appInsights.addTelemetryInitializer((envelope: { tags?: Record<string, string> }) => {
            envelope.tags ??= {};
            envelope.tags['ai.cloud.role'] = 'wf-webapp';
            envelope.tags['ai.cloud.roleInstance'] = 'angular-client';
        });

        appInsights.loadAppInsights();
        this.appInsights = appInsights as MinimalAppInsights;
        this.initialized = true;
    }
 
    // expose methods that can be used in components and services
    trackEvent(name: string): void {
        if (!environment.production) {
            return;
        }

        if (this.appInsights) {
            this.appInsights.trackEvent({ name });
            return;
        }

        void this.initializeTelemetry()
            .then(() => this.appInsights?.trackEvent({ name }))
            .catch(() => undefined);
    }
 
    trackTrace(message: string): void {
        if (!environment.production) {
            return;
        }

        if (this.appInsights) {
            this.appInsights.trackTrace({ message });
            return;
        }

        void this.initializeTelemetry()
            .then(() => this.appInsights?.trackTrace({ message }))
            .catch(() => undefined);
    }
}
