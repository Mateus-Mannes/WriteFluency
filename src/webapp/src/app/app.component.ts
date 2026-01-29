import { Component, ChangeDetectionStrategy, signal, HostListener, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { NavbarComponent } from './shared/navbar/navbar.component';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { UnsupportedScreenComponent } from './shared/unsupported-screen/unsupported-screen.component';
import { BrowserService } from './core/services/browser.service';

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
  private readonly MIN_SUPPORTED_WIDTH = 1100;
  
  // Signal to track if screen is supported
  isScreenSupported = signal(true);

  private platformId = inject(PLATFORM_ID);
  private browserService = inject(BrowserService);

  constructor() {
    // Check initial screen size only in browser
    if (isPlatformBrowser(this.platformId)) {
      this.checkScreenSize();
    }
  }

  @HostListener('window:resize')
  onResize() {
    this.checkScreenSize();
  }

  private checkScreenSize() {
    if (isPlatformBrowser(this.platformId)) {
      const isSupported = this.browserService.getWindowWidth() >= this.MIN_SUPPORTED_WIDTH;
      this.isScreenSupported.set(isSupported);
    }
  }
}
