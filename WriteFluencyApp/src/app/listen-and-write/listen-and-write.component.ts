import { Component, ElementRef, ViewChild } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { VerificationComponent } from './verification/verification.component';
import { PropositionComponent } from './proposition/proposition.component';
import { AlertService } from '../shared/services/alert-service';

@Component({
  selector: 'app-listen-and-write',
  templateUrl: './listen-and-write.component.html',
  styleUrls: ['./listen-and-write.component.css']
})
export class ListenAndWriteComponent {

  constructor(
    private readonly _dialog: MatDialog,
    private readonly _alertSrvice: AlertService)
  { }

  @ViewChild('textarea') textarea!: ElementRef;
  @ViewChild('proposition') proposition!: PropositionComponent;

  textAreaContent = '';

  ngAfterViewInit() {
    this._alertSrvice.alert('Select a complexity level and a subject. Gerenate an audio proposition and try to write it ! Then verify your text.', 'info', 20000);
  }

  verify() {
    const dialog = this._dialog.open(VerificationComponent, 
      {data: {originalText: this.proposition.propositionText, 
        userText: this.textAreaContent}});
  }

  audioPlayOrPause() {
    this.textarea.nativeElement.focus();
  }
}
