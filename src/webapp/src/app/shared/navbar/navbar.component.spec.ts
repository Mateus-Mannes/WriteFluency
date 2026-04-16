import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { AuthSessionState } from '../../auth/models/auth-session.model';

import { NavbarComponent } from './navbar.component';
import { AuthSessionStore } from '../../auth/services/auth-session.store';

describe('NavbarComponent', () => {
  let component: NavbarComponent;
  let fixture: ComponentFixture<NavbarComponent>;
  let authSessionStoreMock: AuthSessionStore;
  let logoutSpy: jasmine.Spy;
  let authState: ReturnType<typeof signal<AuthSessionState>>;
  let isAuthenticatedSignal: ReturnType<typeof signal<boolean>>;

  beforeEach(async () => {
    authState = signal<AuthSessionState>({
      isAuthenticated: true,
      userId: 'user-1',
      email: 'user@test.com',
      emailConfirmed: true,
      issuedAtUtc: null,
      expiresAtUtc: null,
      isLoading: false,
      error: null,
    });

    isAuthenticatedSignal = signal<boolean>(true);

    logoutSpy = jasmine.createSpy('logout').and.callFake(async () => {
      authState.update((state) => ({
        ...state,
        isAuthenticated: false,
      }));
      isAuthenticatedSignal.set(false);
    });

    authSessionStoreMock = {
      logout: logoutSpy,
      isAuthenticated: isAuthenticatedSignal,
      email: signal<string | null>('user@test.com'),
      state: authState,
    } as unknown as AuthSessionStore;

    await TestBed.configureTestingModule({
      imports: [NavbarComponent],
      providers: [
        { provide: AuthSessionStore, useValue: authSessionStoreMock },
        {
          provide: ActivatedRoute,
          useValue: {
            params: of({}),
            queryParams: of({})
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NavbarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should open confirmation modal on logout click', () => {
    component.onLogout();

    expect(component.isLogoutConfirmationOpen()).toBeTrue();
    expect(logoutSpy).not.toHaveBeenCalled();
  });

  it('should close confirmation modal on cancel', () => {
    component.onLogout();
    expect(component.isLogoutConfirmationOpen()).toBeTrue();

    component.dismissLogoutConfirmation();

    expect(component.isLogoutConfirmationOpen()).toBeFalse();
    expect(logoutSpy).not.toHaveBeenCalled();
  });

  it('should call logout and redirect when confirmation is accepted', async () => {
    component.onLogout();

    const redirectSpy = spyOn(
      component as unknown as { redirectToLoginPage: () => void },
      'redirectToLoginPage'
    );

    await component.confirmLogout();

    expect(logoutSpy).toHaveBeenCalled();
    expect(redirectSpy).toHaveBeenCalled();
    expect(component.isLogoutConfirmationOpen()).toBeFalse();
  });

  it('should not redirect when logout fails to clear authentication state', async () => {
    logoutSpy.and.returnValue(Promise.resolve());
    authState.update((state) => ({
      ...state,
      isAuthenticated: true,
    }));
    isAuthenticatedSignal.set(true);

    component.onLogout();

    const redirectSpy = spyOn(
      component as unknown as { redirectToLoginPage: () => void },
      'redirectToLoginPage'
    );

    await component.confirmLogout();

    expect(logoutSpy).toHaveBeenCalled();
    expect(redirectSpy).not.toHaveBeenCalled();
  });
});
