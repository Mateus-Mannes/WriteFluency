import { Component, ElementRef, Input, ViewChild } from '@angular/core';

@Component({
    selector: 'app-drop-down',
    templateUrl: './drop-down.component.html',
    styleUrls: ['./drop-down.component.css'],
    standalone: false
})
export class DropDownComponent {

  @Input() options: string[] = [];
  @ViewChild('label') label!: ElementRef;
  selectedOption!: string;

  public OnOptionSelected(option: any): void {
    this.selectedOption = option;
    this.label.nativeElement.innerHTML = option;
  }
}
