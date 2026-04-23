import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, ChangeDetectionStrategy, Optional, PLATFORM_ID, inject, signal } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Insights } from '../../../telemetry/insights.service';
import { AuthSessionStore } from '../../auth/services/auth-session.store';

@Component({
  selector: 'app-navbar',
  standalone: true,
  templateUrl: './navbar.component.html',
  styleUrls: ['./navbar.component.scss'],
  imports: [
    CommonModule,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    RouterLink,
    RouterLinkActive
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavbarComponent {
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);
  readonly isLogoutConfirmationOpen = signal(false);

  constructor(
    @Optional() private insights: Insights | null,
    protected authSessionStore: AuthSessionStore
  ) { }

  onNavClick(target: string, route: string): void {
    this.insights?.trackEvent('navbar_click', {
      target,
      route
    });
  }

  onLogout(): void {
    if (this.authSessionStore.state().isLoading) {
      return;
    }

    this.isLogoutConfirmationOpen.set(true);
  }

  dismissLogoutConfirmation(): void {
    this.isLogoutConfirmationOpen.set(false);
  }

  async confirmLogout(): Promise<void> {
    this.isLogoutConfirmationOpen.set(false);

    await this.authSessionStore.logout();

    if (!this.authSessionStore.state().isAuthenticated) {
      this.onNavClick('logout', '/auth/login');
      await this.redirectToLoginPage();
    }
  }

  protected async redirectToLoginPage(): Promise<void> {
    const loginRedirectPath = this.getLoginRedirectPath();

    try {
      const navigationCompleted = await this.router.navigateByUrl(loginRedirectPath);
      if (navigationCompleted || !this.isBrowser) {
        return;
      }
    } catch {
      if (!this.isBrowser) {
        return;
      }
    }

    window.location.assign(loginRedirectPath);
  }

  protected getLoginRedirectPath(): string {
    return '/auth/login';
  }
}
