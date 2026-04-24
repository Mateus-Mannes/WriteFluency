import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { AuthApiService } from './auth-api.service';
import { AuthSessionStore } from './auth-session.store';

describe('AuthSessionStore', () => {
  let store: AuthSessionStore;
  let authApiServiceSpy: jasmine.SpyObj<AuthApiService>;

  beforeEach(() => {
    window.localStorage.removeItem('wf.auth.session.snapshot.v1');

    authApiServiceSpy = jasmine.createSpyObj<AuthApiService>('AuthApiService', [
      'session',
      'logout',
      'markListenWriteTutorialCompleted',
    ]);

    TestBed.configureTestingModule({
      providers: [
        AuthSessionStore,
        { provide: AuthApiService, useValue: authApiServiceSpy },
        { provide: PLATFORM_ID, useValue: 'browser' },
      ],
    });

    store = TestBed.inject(AuthSessionStore);
  });

  afterEach(() => {
    window.localStorage.removeItem('wf.auth.session.snapshot.v1');
  });

  it('should set authenticated state after successful refresh', async () => {
    const issuedAtUtc = new Date('2026-01-01T00:00:00.000Z').toISOString();
    const expiresAtUtc = new Date('2026-01-01T01:00:00.000Z').toISOString();

    authApiServiceSpy.session.and.returnValue(
      of({
        isAuthenticated: true,
        userId: 'abc',
        email: 'user@test.com',
        emailConfirmed: true,
        listenWriteTutorialCompleted: false,
        issuedAtUtc,
        expiresAtUtc,
      }),
    );

    await store.refreshSession();

    expect(store.state().isAuthenticated).toBeTrue();
    expect(store.state().hasReliableSessionState).toBeTrue();
    expect(store.state().email).toBe('user@test.com');
    expect(store.state().listenWriteTutorialCompleted).toBeFalse();
    expect(store.state().issuedAtUtc).toBe(issuedAtUtc);
    expect(store.state().expiresAtUtc).toBe(expiresAtUtc);
  });

  it('should clear authenticated state on unauthorized refresh', async () => {
    authApiServiceSpy.session.and.returnValue(
      of({
        isAuthenticated: true,
        userId: 'abc',
        email: 'user@test.com',
        emailConfirmed: true,
        listenWriteTutorialCompleted: false,
        issuedAtUtc: new Date('2026-01-01T00:00:00.000Z').toISOString(),
        expiresAtUtc: new Date('2026-01-01T01:00:00.000Z').toISOString(),
      }),
    );
    await store.refreshSession();

    authApiServiceSpy.session.and.returnValue(throwError(() => ({ status: 401 })));

    await store.refreshSession();

    expect(store.state().isAuthenticated).toBeFalse();
    expect(store.state().email).toBeNull();
    expect(store.state().hasReliableSessionState).toBeTrue();
    expect(store.state().listenWriteTutorialCompleted).toBeNull();
  });

  it('should mark session state as unreliable on non-auth refresh failure', async () => {
    authApiServiceSpy.session.and.returnValues(
      of({
        isAuthenticated: true,
        userId: 'abc',
        email: 'user@test.com',
        emailConfirmed: true,
        listenWriteTutorialCompleted: false,
        issuedAtUtc: new Date('2026-01-01T00:00:00.000Z').toISOString(),
        expiresAtUtc: new Date('2026-01-01T01:00:00.000Z').toISOString(),
      }),
      throwError(() => ({ status: 0 })),
    );

    await store.refreshSession();
    await store.refreshSession();

    expect(store.state().isAuthenticated).toBeTrue();
    expect(store.state().hasReliableSessionState).toBeFalse();
  });

  it('should clear session on logout', async () => {
    authApiServiceSpy.logout.and.returnValue(of({}));

    await store.logout();

    expect(authApiServiceSpy.logout).toHaveBeenCalled();
    expect(store.state().isAuthenticated).toBeFalse();
  });

  it('should restore cached session state from localStorage snapshot', () => {
    const issuedAtUtc = new Date('2026-01-01T00:00:00.000Z').toISOString();
    const expiresAtUtc = new Date('2026-01-01T01:00:00.000Z').toISOString();
    window.localStorage.setItem(
      'wf.auth.session.snapshot.v1',
      JSON.stringify({
        isAuthenticated: true,
        userId: 'snapshot-user',
        email: 'snapshot@test.com',
        emailConfirmed: true,
        listenWriteTutorialCompleted: true,
        issuedAtUtc,
        expiresAtUtc,
        cachedAtUtc: new Date('2026-01-01T00:10:00.000Z').toISOString(),
      }),
    );

    (store as unknown as { restoreSnapshot: () => void }).restoreSnapshot();

    expect(store.state().isAuthenticated).toBeTrue();
    expect(store.state().userId).toBe('snapshot-user');
    expect(store.state().email).toBe('snapshot@test.com');
    expect(store.state().listenWriteTutorialCompleted).toBeTrue();
    expect(store.state().hasReliableSessionState).toBeFalse();
    expect(store.state().issuedAtUtc).toBe(issuedAtUtc);
    expect(store.state().expiresAtUtc).toBe(expiresAtUtc);
  });

  it('should tolerate missing tutorial flag from older session responses', async () => {
    authApiServiceSpy.session.and.returnValue(
      of({
        isAuthenticated: true,
        userId: 'abc',
        email: 'user@test.com',
        emailConfirmed: true,
        issuedAtUtc: new Date('2026-01-01T00:00:00.000Z').toISOString(),
        expiresAtUtc: new Date('2026-01-01T01:00:00.000Z').toISOString(),
      }),
    );

    await store.refreshSession();

    expect(store.state().listenWriteTutorialCompleted).toBeNull();
    expect(store.state().hasReliableSessionState).toBeTrue();
  });

  it('should trigger background refresh shortly before session expiry', async () => {
    jasmine.clock().install();
    try {
      const now = new Date('2026-01-01T00:00:00.000Z');
      jasmine.clock().mockDate(now);

      authApiServiceSpy.session.and.returnValues(
        of({
          isAuthenticated: true,
          userId: 'abc',
          email: 'user@test.com',
          emailConfirmed: true,
          listenWriteTutorialCompleted: false,
          issuedAtUtc: now.toISOString(),
          expiresAtUtc: new Date(now.getTime() + 6 * 60 * 1000).toISOString(),
        }),
        of({
          isAuthenticated: true,
          userId: 'abc',
          email: 'user@test.com',
          emailConfirmed: true,
          listenWriteTutorialCompleted: false,
          issuedAtUtc: now.toISOString(),
          expiresAtUtc: new Date(now.getTime() + 30 * 60 * 1000).toISOString(),
        }),
      );

      await store.refreshSession();
      expect(authApiServiceSpy.session).toHaveBeenCalledTimes(1);

      jasmine.clock().tick(59 * 1000);
      expect(authApiServiceSpy.session).toHaveBeenCalledTimes(1);

      jasmine.clock().tick(2 * 1000);
      expect(authApiServiceSpy.session).toHaveBeenCalledTimes(2);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('should ignore stale refresh response when a newer refresh already completed', async () => {
    const staleSessionResponse = new Subject<{
      isAuthenticated: boolean;
      userId: string;
      email: string;
      emailConfirmed: boolean;
      listenWriteTutorialCompleted?: boolean;
      issuedAtUtc: string;
      expiresAtUtc: string;
    }>();
    const latestSessionResponse = new Subject<{
      isAuthenticated: boolean;
      userId: string;
      email: string;
      emailConfirmed: boolean;
      listenWriteTutorialCompleted?: boolean;
      issuedAtUtc: string;
      expiresAtUtc: string;
    }>();

    authApiServiceSpy.session.and.returnValues(
      staleSessionResponse.asObservable(),
      latestSessionResponse.asObservable(),
    );

    store.refreshSessionInBackground();
    const latestRefresh = store.refreshSession();

    latestSessionResponse.next({
      isAuthenticated: true,
      userId: 'new-user',
      email: 'new@test.com',
      emailConfirmed: true,
      listenWriteTutorialCompleted: true,
      issuedAtUtc: new Date('2026-01-01T02:00:00.000Z').toISOString(),
      expiresAtUtc: new Date('2026-01-01T03:00:00.000Z').toISOString(),
    });
    latestSessionResponse.complete();

    await latestRefresh;

    staleSessionResponse.next({
      isAuthenticated: true,
      userId: 'old-user',
      email: 'old@test.com',
      emailConfirmed: true,
      listenWriteTutorialCompleted: false,
      issuedAtUtc: new Date('2026-01-01T00:00:00.000Z').toISOString(),
      expiresAtUtc: new Date('2026-01-01T01:00:00.000Z').toISOString(),
    });
    staleSessionResponse.complete();

    expect(store.state().userId).toBe('new-user');
    expect(store.state().email).toBe('new@test.com');

    const snapshotRaw = window.localStorage.getItem('wf.auth.session.snapshot.v1');
    expect(snapshotRaw).not.toBeNull();
    const snapshot = JSON.parse(snapshotRaw as string) as { userId?: string; email?: string };
    expect(snapshot.userId).toBe('new-user');
    expect(snapshot.email).toBe('new@test.com');
  });

  it('should patch tutorial flag locally after background mark-completed succeeds', async () => {
    authApiServiceSpy.session.and.returnValue(
      of({
        isAuthenticated: true,
        userId: 'abc',
        email: 'user@test.com',
        emailConfirmed: true,
        listenWriteTutorialCompleted: false,
        issuedAtUtc: new Date('2026-01-01T00:00:00.000Z').toISOString(),
        expiresAtUtc: new Date('2026-01-01T01:00:00.000Z').toISOString(),
      }),
    );
    authApiServiceSpy.markListenWriteTutorialCompleted.and.returnValue(
      of({ listenWriteTutorialCompleted: true }),
    );

    await store.refreshSession();
    store.markListenWriteTutorialCompletedInBackground();
    await Promise.resolve();

    expect(authApiServiceSpy.markListenWriteTutorialCompleted).toHaveBeenCalledTimes(1);
    expect(store.state().listenWriteTutorialCompleted).toBeTrue();
  });

  it('should swallow failures from background mark-completed calls', async () => {
    authApiServiceSpy.session.and.returnValue(
      of({
        isAuthenticated: true,
        userId: 'abc',
        email: 'user@test.com',
        emailConfirmed: true,
        listenWriteTutorialCompleted: false,
        issuedAtUtc: new Date('2026-01-01T00:00:00.000Z').toISOString(),
        expiresAtUtc: new Date('2026-01-01T01:00:00.000Z').toISOString(),
      }),
    );
    authApiServiceSpy.markListenWriteTutorialCompleted.and.returnValue(
      throwError(() => ({ status: 500 })),
    );

    await store.refreshSession();
    expect(() => store.markListenWriteTutorialCompletedInBackground()).not.toThrow();
    await Promise.resolve();

    expect(authApiServiceSpy.markListenWriteTutorialCompleted).toHaveBeenCalledTimes(1);
    expect(store.state().listenWriteTutorialCompleted).toBeFalse();
  });
});
