import { Component, ElementRef, ViewChild } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { VerificationComponent } from './verification/verification.component';
import { PropositionComponent } from './proposition/proposition.component';

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
  @ViewChild('proposition') proposition!: PropositionComponent;

  textAreaContent = '';

  verify() {
    const dialog = this._dialog.open(VerificationComponent, 
      {data: {originalText: this.proposition.propositionText, 
        userText: this.textAreaContent}});
  }

  audioPlayOrPause() {
    this.textarea.nativeElement.focus();
  }
}
