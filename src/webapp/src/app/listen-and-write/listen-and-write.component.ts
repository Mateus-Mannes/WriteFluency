import { Component, ElementRef, ViewChild } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { VerificationComponent } from './verification/verification.component';
import { PropositionComponent } from './proposition/proposition.component';
import { AlertService } from '../shared/services/alert-service';
import { ListenAndWriteService } from './listen-and-write.service';

@Component({
    selector: 'app-listen-and-write',
    templateUrl: './listen-and-write.component.html',
    styleUrls: ['./listen-and-write.component.css'],
    standalone: false
})
export class ListenAndWriteComponent {

  constructor(
    private readonly _dialog: MatDialog,
    private readonly _alertSrvice: AlertService,
    private readonly _service: ListenAndWriteService)
  { }

  @ViewChild('textarea') textarea!: ElementRef;
  @ViewChild('proposition') proposition!: PropositionComponent;

  textAreaContent = '';
  loading = false;

  ngAfterViewInit() {
    this._alertSrvice.alert('Select a complexity level and a subject. Generate an audio proposition and try to write it ! Then verify your text.', 'info', 20000);
  }

  verify() {
    this.loading = true;
    this._service.compareTexts(this.proposition.propositionText, this.textAreaContent)
      .subscribe({
        next: (result) => {
          this.loading = false;
          this._dialog.open(VerificationComponent, 
            { data: {originalText: this.proposition.propositionText, 
              userText: this.textAreaContent,
              comparisions: result }}); },
        error: () => {
          this._alertSrvice.alert('An error occured while comparing texts. Please try again later.', 'danger');
          this.loading = false; }
      });
  }

  audioPlayOrPause() {
    this.textarea.nativeElement.focus();
  }
}
