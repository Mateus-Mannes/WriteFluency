import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { userPageAuthGuard } from './user-page-auth.guard';

describe('userPageAuthGuard', () => {
  let authSessionStoreSpy: jasmine.SpyObj<AuthSessionStore>;
  let router: Router;

  beforeEach(() => {
    authSessionStoreSpy = jasmine.createSpyObj<AuthSessionStore>('AuthSessionStore', [
      'refreshSession',
      'isAuthenticated',
    ]);
    authSessionStoreSpy.refreshSession.and.returnValue(Promise.resolve());

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthSessionStore, useValue: authSessionStoreSpy },
        { provide: PLATFORM_ID, useValue: 'browser' },
      ],
    });

    router = TestBed.inject(Router);
  });

  it('should allow navigation when already authenticated', async () => {
    authSessionStoreSpy.isAuthenticated.and.returnValue(true);

    const result = await TestBed.runInInjectionContext(() => userPageAuthGuard({} as never, {} as never));

    expect(result).toBeTrue();
    expect(authSessionStoreSpy.refreshSession).not.toHaveBeenCalled();
  });

  it('should refresh session and allow navigation when authentication becomes valid', async () => {
    authSessionStoreSpy.isAuthenticated.and.returnValues(false, true);

    const result = await TestBed.runInInjectionContext(() => userPageAuthGuard({} as never, {} as never));

    expect(authSessionStoreSpy.refreshSession).toHaveBeenCalled();
    expect(result).toBeTrue();
  });

  it('should redirect to login when unauthenticated after refresh', async () => {
    authSessionStoreSpy.isAuthenticated.and.returnValues(false, false);

    const result = await TestBed.runInInjectionContext(() => userPageAuthGuard({} as never, {} as never));

    expect(authSessionStoreSpy.refreshSession).toHaveBeenCalled();
    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/auth/login');
  });
});
