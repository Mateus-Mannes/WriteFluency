import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-button',
  templateUrl: './button.component.html',
  styleUrls: ['./button.component.css']
})
export class ButtonComponent {
  @Input() disabled: boolean = false;
  @Output() onClick = new EventEmitter<void>();

  clickHandler() {
    this.onClick.emit();
  }
}
