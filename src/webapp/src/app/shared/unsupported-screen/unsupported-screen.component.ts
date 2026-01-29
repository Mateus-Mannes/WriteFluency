import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-unsupported-screen',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './unsupported-screen.component.html',
  styleUrl: './unsupported-screen.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnsupportedScreenComponent {

}
