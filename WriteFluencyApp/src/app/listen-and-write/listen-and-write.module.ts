import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ListenAndWriteComponent } from './component/listen-and-write.component';
import { SharedModule } from '../shared/shared.module';

@NgModule({
  declarations: [
    ListenAndWriteComponent
  ],
  imports: [
    CommonModule,
    SharedModule
  ],
  exports: [
    ListenAndWriteComponent
  ]
})
export class ListenAndWriteModule { }
