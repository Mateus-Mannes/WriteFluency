import { Component, ElementRef, ViewChild } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { VerificationComponent } from './verification/verification.component';

@Component({
  selector: 'app-listen-and-write',
  templateUrl: './listen-and-write.component.html',
  styleUrls: ['./listen-and-write.component.css']
})
export class ListenAndWriteComponent {

  constructor(
    private readonly _dialog: MatDialog)
  { }

  @ViewChild('textarea') textarea!: ElementRef;

  verify() {
    const dialog = this._dialog.open(VerificationComponent, this.textarea.nativeElement.value);
  }
}
