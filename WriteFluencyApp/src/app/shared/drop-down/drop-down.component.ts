import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-drop-down',
  templateUrl: './drop-down.component.html',
  styleUrls: ['./drop-down.component.css']
})
export class DropDownComponent {

  @Input() label: string = '';
  @Input() options: string[] = [];

  public OnOptionSelected(option: any): void {
    this.label = option;
  }
}
