import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { ProgressItemResponse, ProgressSummaryResponse } from './models/user-progress.model';
import { UserProgressApiService } from './services/user-progress-api.service';

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
    } catch {
      this.summary.set(null);
      this.items.set([]);
      this.error.set('Could not load progress right now. Please try again.');
    } finally {
      this.isLoading.set(false);
    }
  }

  formatPercent(value: number | null): string {
    if (value === null || Number.isNaN(value)) {
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
