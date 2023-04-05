import { CoreModule } from '@abp/ng.core';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { NgModule } from '@angular/core';
import { NgxValidateCoreModule } from '@ngx-validate/core';

@NgModule({
  declarations: [],
  imports: [
    CoreModule,
    NgbDropdownModule,
    NgxValidateCoreModule
  ],
  exports: [
    CoreModule,
    NgbDropdownModule,
    NgxValidateCoreModule
  ],
  providers: []
})
export class SharedModule {}
