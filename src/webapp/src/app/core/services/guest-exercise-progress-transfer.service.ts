import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';

const transferStorageKey = 'wf.auth.post-login-complete-sync.v2';
const resultsSaveSource = 'results_save_cta';
const transferLifetimeMs = 60 * 60 * 1000;

interface GuestExerciseProgressTransfer {
  exerciseId: number;
  requestedAtUtc: string;
  accountCreationStarted: boolean;
  authorizedUserId: string | null;
}

@Injectable({ providedIn: 'root' })
export class GuestExerciseProgressTransferService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);

  request(exerciseId: number | null): void {
    if (!exerciseId) {
      return;
    }

    this.write({
      exerciseId,
      requestedAtUtc: new Date().toISOString(),
      accountCreationStarted: false,
      authorizedUserId: null,
    });
  }

  markAccountCreationStarted(source: string): void {
    const transfer = this.read();
    if (!transfer || source !== resultsSaveSource) {
      return;
    }

    this.write({
      ...transfer,
      accountCreationStarted: true,
    });
  }

  resolveSuccessfulLogin(source: string, isNewUser: boolean, userId: string | null): void {
    const transfer = this.read();
    if (!transfer) {
      return;
    }

    const canTransfer =
      source === resultsSaveSource
      && Boolean(userId)
      && (isNewUser || transfer.accountCreationStarted);

    if (!canTransfer) {
      this.clear();
      return;
    }

    this.write({
      ...transfer,
      authorizedUserId: userId,
    });
  }

  consumeAuthorizedTransfer(exerciseId: number, userId: string | null): boolean {
    const transfer = this.read();
    if (
      !transfer
      || !userId
      || transfer.exerciseId !== exerciseId
      || transfer.authorizedUserId !== userId
    ) {
      return false;
    }

    this.clear();
    return true;
  }

  clear(): void {
    if (!this.isBrowser) {
      return;
    }

    try {
      window.sessionStorage.removeItem(transferStorageKey);
    } catch {
      // Session storage is optional for this handoff.
    }
  }

  private read(): GuestExerciseProgressTransfer | null {
    if (!this.isBrowser) {
      return null;
    }

    try {
      const rawValue = window.sessionStorage.getItem(transferStorageKey);
      if (!rawValue) {
        return null;
      }

      const parsed = JSON.parse(rawValue) as Partial<GuestExerciseProgressTransfer>;
      if (
        typeof parsed.exerciseId !== 'number'
        || !Number.isFinite(parsed.exerciseId)
        || typeof parsed.requestedAtUtc !== 'string'
      ) {
        this.clear();
        return null;
      }

      const requestedAtMs = Date.parse(parsed.requestedAtUtc);
      if (!Number.isFinite(requestedAtMs) || Date.now() - requestedAtMs > transferLifetimeMs) {
        this.clear();
        return null;
      }

      return {
        exerciseId: parsed.exerciseId,
        requestedAtUtc: parsed.requestedAtUtc,
        accountCreationStarted: parsed.accountCreationStarted === true,
        authorizedUserId: typeof parsed.authorizedUserId === 'string'
          ? parsed.authorizedUserId
          : null,
      };
    } catch {
      this.clear();
      return null;
    }
  }

  private write(transfer: GuestExerciseProgressTransfer): void {
    if (!this.isBrowser) {
      return;
    }

    try {
      window.sessionStorage.setItem(transferStorageKey, JSON.stringify(transfer));
    } catch {
      // Session storage is optional for this handoff.
    }
  }
}
