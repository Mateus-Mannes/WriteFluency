import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { ProgressItemResponse, ProgressSummaryResponse } from './models/user-progress.model';
import { UserProgressApiService } from './services/user-progress-api.service';
import { Insights } from '../../telemetry/insights.service';

@Component({
  selector: 'app-user',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatCardModule, MatProgressSpinnerModule],
  templateUrl: './user.component.html',
  styleUrls: ['./user.component.scss'],
})
export class UserComponent implements OnInit {
  private readonly authSessionStore = inject(AuthSessionStore);
  private readonly userProgressApi = inject(UserProgressApiService);
  private readonly router = inject(Router);
  private readonly insights = inject(Insights, { optional: true });

  readonly authState = this.authSessionStore.state;

  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly summary = signal<ProgressSummaryResponse | null>(null);
  readonly items = signal<ProgressItemResponse[]>([]);
  readonly hasItems = computed(() => this.items().length > 0);

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  async reload(): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);

    try {
      const [summary, items] = await Promise.all([
        firstValueFrom(this.userProgressApi.summary()),
        firstValueFrom(this.userProgressApi.items()),
      ]);

      this.summary.set(summary);
      this.items.set(items);
    } catch (error) {
      this.summary.set(null);
      this.items.set([]);
      const statusCode = this.getStatusCode(error);
      const errorKind = this.getErrorKind(error, statusCode);

      if (statusCode === 401) {
        this.authSessionStore.invalidateSession();
        this.insights?.trackException(error, {
          properties: {
            area: 'user_progress',
            operation: 'load_user_progress',
            error_kind: errorKind,
            http_status: String(statusCode),
          },
          measurements: {
            http_status: statusCode,
          },
        });

        const redirected = await this.router.navigate(['/auth/login'], {
          queryParams: {
            returnUrl: '/user',
            source: 'user_progress_session_expired',
          },
        });

        if (!redirected) {
          this.error.set('Your session expired. Please log in again.');
        }

        return;
      }

      this.error.set('Could not load progress right now. Please try again.');
      this.insights?.trackException(error, {
        properties: {
          area: 'user_progress',
          operation: 'load_user_progress',
          error_kind: errorKind,
          http_status: statusCode === null ? 'unknown' : String(statusCode),
        },
        measurements: {
          http_status: statusCode ?? 0,
        },
      });
    } finally {
      this.isLoading.set(false);
    }
  }

  private getStatusCode(error: unknown): number | null {
    if (error instanceof HttpErrorResponse && Number.isFinite(error.status)) {
      return error.status;
    }

    return null;
  }

  private getErrorKind(error: unknown, statusCode: number | null): string {
    if (statusCode === 401) {
      return 'unauthorized';
    }

    if (error instanceof Error && error.name === 'TimeoutError') {
      return 'timeout';
    }

    return 'request_failure';
  }

  formatPercent(value: number | null | undefined): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }

    return `${(value * 100).toFixed(1)}%`;
  }

  formatDuration(totalSeconds: number | null | undefined): string {
    if (totalSeconds === null || totalSeconds === undefined || Number.isNaN(totalSeconds)) {
      return '0:00';
    }

    const normalizedSeconds = Math.max(0, Math.floor(totalSeconds));
    const hours = Math.floor(normalizedSeconds / 3600);
    const minutes = Math.floor((normalizedSeconds % 3600) / 60);
    const seconds = normalizedSeconds % 60;

    if (hours > 0) {
      return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
    }

    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  }

  formatWordProgress(currentWordCount: number | null | undefined, originalWordCount: number | null | undefined): string {
    const written = currentWordCount === null || currentWordCount === undefined || Number.isNaN(currentWordCount)
      ? 0
      : Math.max(0, Math.floor(currentWordCount));

    const total = originalWordCount === null || originalWordCount === undefined || Number.isNaN(originalWordCount)
      ? '—'
      : String(Math.max(0, Math.floor(originalWordCount)));

    return `${written}/${total}`;
  }

  statusLabel(status: string): string {
    if (status === 'completed') {
      return 'Completed';
    }

    if (status === 'in_progress') {
      return 'In progress';
    }

    return 'Unavailable';
  }

  trackByExerciseId(_index: number, item: ProgressItemResponse): number {
    return item.exerciseId;
  }
}
