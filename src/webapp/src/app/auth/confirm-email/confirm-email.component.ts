import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { firstValueFrom } from 'rxjs';
import { AuthApiService } from '../services/auth-api.service';

@Component({
  selector: 'app-confirm-email',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatCardModule],
  templateUrl: './confirm-email.component.html',
  styleUrls: ['./confirm-email.component.scss'],
})
export class ConfirmEmailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authApiService = inject(AuthApiService);

  readonly isBusy = signal(true);
  readonly isSuccess = signal(false);
  readonly message = signal('Verifying confirmation link...');

  async ngOnInit(): Promise<void> {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const code = this.route.snapshot.queryParamMap.get('code');

    if (!userId || !code) {
      this.isBusy.set(false);
      this.isSuccess.set(false);
      this.message.set('Invalid confirmation link. Please request a new confirmation email.');
      return;
    }

    try {
      await firstValueFrom(this.authApiService.confirmEmail(userId, code));
      this.isSuccess.set(true);
      this.message.set('Email confirmed. You can now log in.');
    } catch {
      this.isSuccess.set(false);
      this.message.set('Email confirmation failed. The link may be invalid or expired.');
    } finally {
      this.isBusy.set(false);
    }
  }
}
