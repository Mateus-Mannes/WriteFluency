import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { AuthSession } from '../models/auth-session.model';
import { AuthApiService } from './auth-api.service';
import { AuthSessionStore } from './auth-session.store';

function authenticatedSession(overrides: Partial<AuthSession> = {}): AuthSession {
  return {
    isAuthenticated: true,
    userId: 'abc',
    email: 'user@test.com',
    emailConfirmed: true,
    listenWriteTutorialCompleted: false,
    plan: 'free',
    entitlementStatus: 'free',
    isPro: false,
    currentPeriodEndUtc: null,
    cancelAtPeriodEnd: false,
    issuedAtUtc: new Date('2026-01-01T00:00:00.000Z').toISOString(),
    expiresAtUtc: new Date('2026-01-01T01:00:00.000Z').toISOString(),
    ...overrides,
  };
}

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

  it('should expose free entitlement in initial state', () => {
    expect(store.state().plan).toBe('free');
    expect(store.state().entitlementStatus).toBe('free');
    expect(store.state().isPro).toBeFalse();
    expect(store.state().currentPeriodEndUtc).toBeNull();
    expect(store.state().cancelAtPeriodEnd).toBeFalse();
  });

  it('should set authenticated state after successful refresh', async () => {
    const issuedAtUtc = new Date('2026-01-01T00:00:00.000Z').toISOString();
    const expiresAtUtc = new Date('2026-01-01T01:00:00.000Z').toISOString();

    authApiServiceSpy.session.and.returnValue(
      of(authenticatedSession({
        plan: 'pro',
        entitlementStatus: 'pro_active',
        isPro: true,
        currentPeriodEndUtc: '2026-02-01T00:00:00.000Z',
        issuedAtUtc,
        expiresAtUtc,
      })),
    );

    await store.refreshSession();

    expect(store.state().isAuthenticated).toBeTrue();
    expect(store.state().hasReliableSessionState).toBeTrue();
    expect(store.state().email).toBe('user@test.com');
    expect(store.state().listenWriteTutorialCompleted).toBeFalse();
    expect(store.state().plan).toBe('pro');
    expect(store.state().entitlementStatus).toBe('pro_active');
    expect(store.state().isPro).toBeTrue();
    expect(store.state().currentPeriodEndUtc).toBe('2026-02-01T00:00:00.000Z');
    expect(store.state().cancelAtPeriodEnd).toBeFalse();
    expect(store.state().issuedAtUtc).toBe(issuedAtUtc);
    expect(store.state().expiresAtUtc).toBe(expiresAtUtc);
  });

  it('should clear authenticated state on unauthorized refresh', async () => {
    authApiServiceSpy.session.and.returnValue(
      of(authenticatedSession({ plan: 'pro', entitlementStatus: 'pro_active', isPro: true })),
    );
    await store.refreshSession();

    authApiServiceSpy.session.and.returnValue(throwError(() => ({ status: 401 })));

    await store.refreshSession();

    expect(store.state().isAuthenticated).toBeFalse();
    expect(store.state().email).toBeNull();
    expect(store.state().hasReliableSessionState).toBeTrue();
    expect(store.state().listenWriteTutorialCompleted).toBeNull();
    expect(store.state().plan).toBe('free');
    expect(store.state().entitlementStatus).toBe('free');
    expect(store.state().isPro).toBeFalse();
  });

  it('should mark session state as unreliable on non-auth refresh failure', async () => {
    authApiServiceSpy.session.and.returnValues(
      of(authenticatedSession()),
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
    expect(store.state().plan).toBe('free');
    expect(store.state().entitlementStatus).toBe('free');
    expect(store.state().isPro).toBeFalse();
  });

  it('should ignore in-flight refresh responses after logout clears the session', async () => {
    const staleSessionResponse = new Subject<AuthSession>();
    authApiServiceSpy.session.and.returnValue(staleSessionResponse.asObservable());
    authApiServiceSpy.logout.and.returnValue(of({}));

    store.refreshSessionInBackground();
    await store.logout();

    staleSessionResponse.next(authenticatedSession({
      userId: 'old-user',
      email: 'old@test.com',
    }));
    staleSessionResponse.complete();
    await Promise.resolve();

    expect(store.state().isAuthenticated).toBeFalse();
    expect(store.state().userId).toBeNull();
    expect(store.state().email).toBeNull();

    const snapshotRaw = window.localStorage.getItem('wf.auth.session.snapshot.v1');
    expect(snapshotRaw).not.toBeNull();
    const snapshot = JSON.parse(snapshotRaw as string) as { userId?: string | null; email?: string | null };
    expect(snapshot.userId).toBeNull();
    expect(snapshot.email).toBeNull();
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
        plan: 'pro',
        entitlementStatus: 'pro_canceling',
        isPro: true,
        currentPeriodEndUtc: '2026-02-01T00:00:00.000Z',
        cancelAtPeriodEnd: true,
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
    expect(store.state().plan).toBe('pro');
    expect(store.state().entitlementStatus).toBe('pro_canceling');
    expect(store.state().isPro).toBeTrue();
    expect(store.state().currentPeriodEndUtc).toBe('2026-02-01T00:00:00.000Z');
    expect(store.state().cancelAtPeriodEnd).toBeTrue();
    expect(store.state().hasReliableSessionState).toBeFalse();
    expect(store.state().issuedAtUtc).toBe(issuedAtUtc);
    expect(store.state().expiresAtUtc).toBe(expiresAtUtc);
  });

  it('should tolerate missing tutorial flag from older session responses', async () => {
    authApiServiceSpy.session.and.returnValue(
      of(authenticatedSession({ listenWriteTutorialCompleted: undefined })),
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
        of(authenticatedSession({
          issuedAtUtc: now.toISOString(),
          expiresAtUtc: new Date(now.getTime() + 6 * 60 * 1000).toISOString(),
        })),
        of(authenticatedSession({
          issuedAtUtc: now.toISOString(),
          expiresAtUtc: new Date(now.getTime() + 30 * 60 * 1000).toISOString(),
        })),
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
    const staleSessionResponse = new Subject<AuthSession>();
    const latestSessionResponse = new Subject<AuthSession>();

    authApiServiceSpy.session.and.returnValues(
      staleSessionResponse.asObservable(),
      latestSessionResponse.asObservable(),
    );

    store.refreshSessionInBackground();
    const latestRefresh = store.refreshSession();

    latestSessionResponse.next(authenticatedSession({
      userId: 'new-user',
      email: 'new@test.com',
      listenWriteTutorialCompleted: true,
      issuedAtUtc: new Date('2026-01-01T02:00:00.000Z').toISOString(),
      expiresAtUtc: new Date('2026-01-01T03:00:00.000Z').toISOString(),
    }));
    latestSessionResponse.complete();

    await latestRefresh;

    staleSessionResponse.next(authenticatedSession({
      userId: 'old-user',
      email: 'old@test.com',
      listenWriteTutorialCompleted: false,
      issuedAtUtc: new Date('2026-01-01T00:00:00.000Z').toISOString(),
      expiresAtUtc: new Date('2026-01-01T01:00:00.000Z').toISOString(),
    }));
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
      of(authenticatedSession()),
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
      of(authenticatedSession()),
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
