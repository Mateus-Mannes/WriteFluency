import { Component, ChangeDetectionStrategy, signal, HostListener } from '@angular/core';
import { NavbarComponent } from './shared/navbar/navbar.component';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { UnsupportedScreenComponent } from './shared/unsupported-screen/unsupported-screen.component';

@Component({
    selector: 'app-root',
    standalone: true,
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    imports: [
      CommonModule,
      RouterOutlet,
      NavbarComponent,
      UnsupportedScreenComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  // Minimum supported width (desktop only - requires keyboard shortcuts)
  private readonly MIN_SUPPORTED_WIDTH = 1300;
  
  // Signal to track if screen is supported
  isScreenSupported = signal(true);

  constructor() {
    // Check initial screen size
    this.checkScreenSize();
  }

  @HostListener('window:resize')
  onResize() {
    this.checkScreenSize();
  }

  private checkScreenSize() {
    const isSupported = window.innerWidth >= this.MIN_SUPPORTED_WIDTH;
    this.isScreenSupported.set(isSupported);
  }
}
