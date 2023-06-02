import { Component, Inject, ViewEncapsulation } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TextPart } from '../entities/text-part';
import { TextComparision } from '../entities/text-comparision';

export interface VerificationData {
  originalText: string;
  userText: string;
  comparisions: TextComparision[];
}

@Component({
  selector: 'app-verification',
  templateUrl: './verification.component.html',
  styleUrls: ['./verification.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class VerificationComponent {

  originalText = this.data.originalText;
  userText = this.data.userText;
  textParts: TextPart[] = [];

  constructor(public dialogRef: MatDialogRef<VerificationComponent>,
    @Inject(MAT_DIALOG_DATA) public data: VerificationData) 
    {
      this.highlightUserText(data.comparisions);  
    }

  close(): void {
    this.dialogRef.close();
  }

  highlightUserText(comparisions: TextComparision[]) {
    let text = this.userText;   
    let lastEnd = 0;
    for (let comparision of comparisions) {
      let start = comparision.userTextRange.initialIndex ;
      let end = comparision.userTextRange.finalIndex + 1;
      let correction = comparision.originalText;
      this.textParts.push({ 
        text: text.slice(lastEnd, start), highlight: false });
      this.textParts.push({ text: text.slice(start, end), highlight: true, correction });
      lastEnd = end;
    }
    this.textParts.push({ text: text.slice(lastEnd), highlight: false });
  }
}
