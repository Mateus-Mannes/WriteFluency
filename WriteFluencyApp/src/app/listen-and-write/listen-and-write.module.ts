import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ListenAndWriteComponent } from './component/listen-and-write.component';
import { SharedModule } from '../shared/shared.module';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations'

@NgModule({
  declarations: [
    ListenAndWriteComponent
  ],
  imports: [
    CommonModule,
    SharedModule,
    BrowserAnimationsModule
  ],
  exports: [
    ListenAndWriteComponent
  ]
})
export class ListenAndWriteModule { }
