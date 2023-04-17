import { CoreModule } from '@abp/ng.core';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { NgModule } from '@angular/core';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { NavbarComponent } from './navbar/navbar.component';

@NgModule({
  declarations: [
    NavbarComponent
  ],
  imports: [
    CoreModule,
    NgbDropdownModule,
    NgxValidateCoreModule
  ],
  exports: [
    CoreModule,
    NgbDropdownModule,
    NgxValidateCoreModule,
    NavbarComponent
  ],
  providers: []
})
export class SharedModule {}
