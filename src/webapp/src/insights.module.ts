import { ErrorHandler, NgModule } from '@angular/core';
import { ApplicationinsightsAngularpluginErrorService } from '@microsoft/applicationinsights-angularplugin-js';
import { Insights } from './insights.service';
 
@NgModule({
  providers: [
    Insights,
    {
      provide: ErrorHandler,
      useClass: ApplicationinsightsAngularpluginErrorService,
    },
  ],
})
export class InsightsModule {
    constructor(private readonly insights: Insights) {}
}