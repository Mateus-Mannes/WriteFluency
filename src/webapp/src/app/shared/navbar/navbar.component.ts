import { Component, ChangeDetectionStrategy, Optional } from '@angular/core';
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

  async onLogout(): Promise<void> {
    await this.authSessionStore.logout();
    this.onNavClick('logout', '/auth/login');
  }
}
