import { Component, ChangeDetectionStrategy, DestroyRef, AfterViewInit, inject, PLATFORM_ID } from '@angular/core';
import { NavbarComponent } from './shared/navbar/navbar.component';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { CommonModule, ViewportScroller, isPlatformBrowser } from '@angular/common';
import { MatIconRegistry } from '@angular/material/icon';
import { filter } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
    selector: 'app-root',
    standalone: true,
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    imports: [
      CommonModule,
      RouterOutlet,
      NavbarComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements AfterViewInit {
  private matIconRegistry = inject(MatIconRegistry);
  private router = inject(Router);
  private viewportScroller = inject(ViewportScroller);
  private platformId = inject(PLATFORM_ID);
  private destroyRef = inject(DestroyRef);

  constructor() {
    this.matIconRegistry.setDefaultFontSetClass('material-symbols-outlined');
  }

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        setTimeout(() => {
          this.viewportScroller.scrollToPosition([0, 0]);
        }, 0);
      });
  }
}
