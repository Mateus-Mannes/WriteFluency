import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
    selector: 'app-button',
    templateUrl: './button.component.html',
    styleUrls: ['./button.component.scss'],
})
export class ButtonComponent {
  @Input() disabled: boolean = false;
  @Output() onClick = new EventEmitter<void>();

  clickHandler() {
    this.onClick.emit();
  }
}
