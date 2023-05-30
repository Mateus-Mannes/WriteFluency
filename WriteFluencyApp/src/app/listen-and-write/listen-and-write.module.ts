import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ListenAndWriteComponent } from './listen-and-write.component';
import { SharedModule } from '../shared/shared.module';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { VerifyModalComponent } from './verify-modal/verify-modal.component';
import { MatDialogModule } from '@angular/material/dialog';
import {MatTooltipModule} from '@angular/material/tooltip';
import { PropositionComponent } from './proposition/proposition.component';
import { ListenAndWriteService } from './listen-and-write.service';

@NgModule({
  declarations: [
    ListenAndWriteComponent,
    VerifyModalComponent,
    PropositionComponent
  ],
  imports: [
    CommonModule,
    SharedModule,
    BrowserAnimationsModule,
    MatDialogModule,
    MatTooltipModule
  ],
  exports: [
    ListenAndWriteComponent
  ],
  providers: [ListenAndWriteService]
})
export class ListenAndWriteModule { }
