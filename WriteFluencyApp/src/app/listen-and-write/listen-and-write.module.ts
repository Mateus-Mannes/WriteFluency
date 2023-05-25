import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ListenAndWriteComponent } from './component/listen-and-write.component';
import { SharedModule } from '../shared/shared.module';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { VerifyModalComponent } from './verify-modal/verify-modal.component';
import { MatDialogModule } from '@angular/material/dialog';

@NgModule({
  declarations: [
    ListenAndWriteComponent,
    VerifyModalComponent
  ],
  imports: [
    CommonModule,
    SharedModule,
    BrowserAnimationsModule,
    MatDialogModule
  ],
  exports: [
    ListenAndWriteComponent
  ]
})
export class ListenAndWriteModule { }
