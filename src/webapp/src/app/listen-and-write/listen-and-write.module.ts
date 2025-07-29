import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ListenAndWriteComponent } from './listen-and-write.component';
import { SharedModule } from '../shared/shared.module';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialogModule } from '@angular/material/dialog';
import {MatTooltipModule} from '@angular/material/tooltip';
import { PropositionComponent } from './proposition/proposition.component';
import { ListenAndWriteService } from './listen-and-write.service';
import { VerificationComponent } from './verification/verification.component';
import { FormsModule } from '@angular/forms';
import { BrowserModule } from '@angular/platform-browser';

@NgModule({
  declarations: [
    ListenAndWriteComponent,
    PropositionComponent,
    VerificationComponent
  ],
  imports: [
    CommonModule,
    SharedModule,
    BrowserAnimationsModule,
    MatDialogModule,
    MatTooltipModule,
    BrowserModule,
    FormsModule
  ],
  exports: [
    ListenAndWriteComponent
  ],
  providers: [ListenAndWriteService]
})
export class ListenAndWriteModule { }
