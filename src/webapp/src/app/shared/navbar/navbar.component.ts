import { Component, ChangeDetectionStrategy, Optional, signal } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
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
  private static readonly loginRedirectUrl = 'http://localhost:4200/auth/login';
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
      this.redirectToLoginPage();
    }
  }

  protected redirectToLoginPage(): void {
    window.location.assign(NavbarComponent.loginRedirectUrl);
  }
}
