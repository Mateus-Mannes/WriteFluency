import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavbarComponent } from './navbar/navbar.component';
import { DropDownComponent } from './drop-down/drop-down.component';
import { ButtonComponent } from './button/button.component';



@NgModule({
  declarations: [
    NavbarComponent,
    DropDownComponent,
    ButtonComponent
  ],
  imports: [
    CommonModule
  ],
  exports: [
    NavbarComponent,
    DropDownComponent,
    ButtonComponent
  ]
})
export class SharedModule { }
