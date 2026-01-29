import { registerInstrumentations } from '@opentelemetry/instrumentation';
import {
    WebTracerProvider,
    BatchSpanProcessor,
} from '@opentelemetry/sdk-trace-web';
import { getWebAutoInstrumentations } from '@opentelemetry/auto-instrumentations-web';
import { ZoneContextManager } from '@opentelemetry/context-zone';
import { environment } from '../enviroments/enviroment';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions';

if (!environment.production) {
  let provider: WebTracerProvider;

  provider = new WebTracerProvider({
    resource: resourceFromAttributes({
      [ATTR_SERVICE_NAME]: 'webapp'
    }),
    spanProcessors: [
      new BatchSpanProcessor(
          new OTLPTraceExporter({
              url: environment.otlpEndpoint,
              headers: {}
          }),
      )
    ],
  });
    
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
}