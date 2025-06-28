import { registerInstrumentations } from '@opentelemetry/instrumentation';
import {
    WebTracerProvider,
    BatchSpanProcessor,
} from '@opentelemetry/sdk-trace-web';
import { getWebAutoInstrumentations } from '@opentelemetry/auto-instrumentations-web';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { ZoneContextManager } from '@opentelemetry/context-zone';
import { environment } from './enviroments/enviroment';
 
let provider: WebTracerProvider;

if (environment.production) {
  // TODO: Add a collector that supports OTLP HTTP with json format (aspire dashboard doesn't supports it yet)
  provider = new WebTracerProvider({
    spanProcessors: [
      new BatchSpanProcessor(
          new OTLPTraceExporter({
              url: environment.traceUrl,
              headers: {}
          }),
      )
    ],
  });
}
else {
  provider = new WebTracerProvider();
}
  
provider.register({
  contextManager: new ZoneContextManager(),
});


registerInstrumentations({
    instrumentations: [
        getWebAutoInstrumentations({
            '@opentelemetry/instrumentation-document-load': {},
            '@opentelemetry/instrumentation-user-interaction': {},
            '@opentelemetry/instrumentation-fetch': {},
            '@opentelemetry/instrumentation-xml-http-request': {},
        }),
    ],
});