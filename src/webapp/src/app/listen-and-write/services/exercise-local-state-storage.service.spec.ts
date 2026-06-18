import { TestBed } from '@angular/core/testing';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { ExerciseLocalStateStorageService } from './exercise-local-state-storage.service';

describe('ExerciseLocalStateStorageService', () => {
  let service: ExerciseLocalStateStorageService;
  let authSessionStore: jasmine.SpyObj<AuthSessionStore>;

  beforeEach(() => {
    authSessionStore = jasmine.createSpyObj<AuthSessionStore>(
      'AuthSessionStore',
      ['isAuthenticated', 'userId'],
    );
    authSessionStore.isAuthenticated.and.returnValue(false);
    authSessionStore.userId.and.returnValue(null);

    TestBed.configureTestingModule({
      providers: [
        ExerciseLocalStateStorageService,
        { provide: AuthSessionStore, useValue: authSessionStore },
      ],
    });

    service = TestBed.inject(ExerciseLocalStateStorageService);
  });

  it('should scope logged-out state to the guest owner', () => {
    expect(service.getCurrentStateKey(12)).toBe('wf.listen-write.state.v2:guest:12');
    expect(service.getCurrentSnapshotKey(12)).toBe('wf.listen-write.snapshot.v2:guest:12');
  });

  it('should scope authenticated state to the current user ID', () => {
    authSessionStore.isAuthenticated.and.returnValue(true);
    authSessionStore.userId.and.returnValue('user/a');

    expect(service.getCurrentStateKey(12)).toBe('wf.listen-write.state.v2:user:user%2Fa:12');
    expect(service.getCurrentSnapshotKey(12)).toBe('wf.listen-write.snapshot.v2:user:user%2Fa:12');
  });

  it('should not write authenticated state into the guest scope when user ID is unavailable', () => {
    authSessionStore.isAuthenticated.and.returnValue(true);
    authSessionStore.userId.and.returnValue(null);

    expect(service.getCurrentStateKey(12)).toBeNull();
    expect(service.getCurrentSnapshotKey(12)).toBeNull();
  });
});
