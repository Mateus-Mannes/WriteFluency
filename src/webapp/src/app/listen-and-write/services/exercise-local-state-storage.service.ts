import { Injectable, inject } from '@angular/core';
import { AuthSessionStore } from '../../auth/services/auth-session.store';

const stateKeyPrefix = 'wf.listen-write.state.v2';
const snapshotKeyPrefix = 'wf.listen-write.snapshot.v2';
const guestOwner = 'guest';

@Injectable({ providedIn: 'root' })
export class ExerciseLocalStateStorageService {
  private readonly authSessionStore = inject(AuthSessionStore);

  getCurrentStateKey(exerciseId: number | null): string | null {
    return this.buildCurrentKey(stateKeyPrefix, exerciseId);
  }

  getCurrentSnapshotKey(exerciseId: number | null): string | null {
    return this.buildCurrentKey(snapshotKeyPrefix, exerciseId);
  }

  getGuestStateKey(exerciseId: number): string {
    return this.buildKey(stateKeyPrefix, guestOwner, exerciseId);
  }

  getGuestSnapshotKey(exerciseId: number): string {
    return this.buildKey(snapshotKeyPrefix, guestOwner, exerciseId);
  }

  private buildCurrentKey(prefix: string, exerciseId: number | null): string | null {
    if (!exerciseId) {
      return null;
    }

    if (!this.authSessionStore.isAuthenticated()) {
      return this.buildKey(prefix, guestOwner, exerciseId);
    }

    const userId = this.authSessionStore.userId();
    if (!userId) {
      return null;
    }

    return this.buildKey(prefix, `user:${encodeURIComponent(userId)}`, exerciseId);
  }

  private buildKey(prefix: string, owner: string, exerciseId: number): string {
    return `${prefix}:${owner}:${exerciseId}`;
  }
}
