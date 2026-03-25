import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthApiService } from './auth-api.service';
import { AuthSessionStore } from './auth-session.store';

describe('AuthSessionStore', () => {
  let store: AuthSessionStore;
  let authApiServiceSpy: jasmine.SpyObj<AuthApiService>;

  beforeEach(() => {
    authApiServiceSpy = jasmine.createSpyObj<AuthApiService>('AuthApiService', ['session', 'logout']);

    TestBed.configureTestingModule({
      providers: [
        AuthSessionStore,
        { provide: AuthApiService, useValue: authApiServiceSpy },
        { provide: PLATFORM_ID, useValue: 'browser' },
      ],
    });

    store = TestBed.inject(AuthSessionStore);
  });

  it('should set authenticated state after successful refresh', async () => {
    authApiServiceSpy.session.and.returnValue(
      of({
        isAuthenticated: true,
        userId: 'abc',
        email: 'user@test.com',
        emailConfirmed: true,
      }),
    );

    await store.refreshSession();

    expect(store.state().isAuthenticated).toBeTrue();
    expect(store.state().email).toBe('user@test.com');
  });

  it('should clear authenticated state on unauthorized refresh', async () => {
    authApiServiceSpy.session.and.returnValue(
      of({
        isAuthenticated: true,
        userId: 'abc',
        email: 'user@test.com',
        emailConfirmed: true,
      }),
    );
    await store.refreshSession();

    authApiServiceSpy.session.and.returnValue(throwError(() => ({ status: 401 })));

    await store.refreshSession();

    expect(store.state().isAuthenticated).toBeFalse();
    expect(store.state().email).toBeNull();
  });

  it('should clear session on logout', async () => {
    authApiServiceSpy.logout.and.returnValue(of({}));

    await store.logout();

    expect(authApiServiceSpy.logout).toHaveBeenCalled();
    expect(store.state().isAuthenticated).toBeFalse();
  });
});
