import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavbarComponent } from './navbar/navbar.component';
import { DropDownComponent } from './drop-down/drop-down.component';



@NgModule({
  declarations: [
    NavbarComponent,
    DropDownComponent
  ],
  imports: [
    CommonModule
  ],
  exports: [
    NavbarComponent,
    DropDownComponent
  ]
})
export class SharedModule { }
