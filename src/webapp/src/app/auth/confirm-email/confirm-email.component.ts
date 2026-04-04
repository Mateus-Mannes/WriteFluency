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
  readonly message = signal('Confirming your email...');
  readonly helperMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const code = this.route.snapshot.queryParamMap.get('code');

    if (!userId || !code) {
      this.isBusy.set(false);
      this.isSuccess.set(false);
      this.message.set('This confirmation link is invalid or incomplete. Please request a new confirmation email.');
      return;
    }

    try {
      await firstValueFrom(this.authApiService.confirmEmail(userId, code));
      this.isSuccess.set(true);
      this.message.set('Your email is confirmed. You can now sign in.');
      this.helperMessage.set('If you started sign-up on another device, go back there and continue.');
    } catch {
      this.isSuccess.set(false);
      this.message.set('This confirmation link is invalid or expired. Please request a new confirmation email.');
      this.helperMessage.set(null);
    } finally {
      this.isBusy.set(false);
    }
  }
}
