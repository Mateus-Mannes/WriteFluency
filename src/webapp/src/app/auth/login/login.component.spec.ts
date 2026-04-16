import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthApiService } from '../services/auth-api.service';
import { AuthSessionStore } from '../services/auth-session.store';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authApiServiceSpy: jasmine.SpyObj<AuthApiService>;
  let authSessionStoreSpy: jasmine.SpyObj<AuthSessionStore>;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    authApiServiceSpy = jasmine.createSpyObj<AuthApiService>('AuthApiService', [
      'loginPassword',
      'register',
      'requestOtp',
      'verifyOtp',
      'externalProviders',
    ]);
    authSessionStoreSpy = jasmine.createSpyObj<AuthSessionStore>('AuthSessionStore', ['refreshSession']);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);

    authApiServiceSpy.externalProviders.and.returnValue(of([]));
    authApiServiceSpy.loginPassword.and.returnValue(of({}));
    authApiServiceSpy.register.and.returnValue(of({}));
    authApiServiceSpy.requestOtp.and.returnValue(of({ message: 'Code sent.' }));
    authApiServiceSpy.verifyOtp.and.returnValue(of({}));
    authSessionStoreSpy.refreshSession.and.returnValue(Promise.resolve());
    routerSpy.navigateByUrl.and.returnValue(Promise.resolve(true));
    window.sessionStorage.removeItem('wf.auth.post-login-return-url.v1');

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        { provide: AuthApiService, useValue: authApiServiceSpy },
        { provide: AuthSessionStore, useValue: authSessionStoreSpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {
                get: () => null,
              },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should login with password and update session', async () => {
    component.passwordForm.setValue({
      email: 'user@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();

    expect(authApiServiceSpy.loginPassword).toHaveBeenCalledWith('user@test.com', 'Passw0rd!');
    expect(authSessionStoreSpy.refreshSession).toHaveBeenCalled();
    expect(routerSpy.navigateByUrl).toHaveBeenCalledWith('/user');
  });

  it('should render OTP request message', async () => {
    component.otpForm.patchValue({ email: 'user@test.com' });

    await component.requestOtp();
    fixture.detectChanges();

    expect(component.otpRequestMessage()).toBe('If this email is eligible, we sent a 6-digit sign-in code.');
    expect(component.otpPhase()).toBe('verify');
  });

  it('should render OTP error when verify fails', async () => {
    authApiServiceSpy.verifyOtp.and.returnValue(throwError(() => ({ status: 401 })));

    component.otpForm.patchValue({
      email: 'user@test.com',
    });
    await component.requestOtp();
    component.otpForm.patchValue({
      code: '123456',
    });

    await component.verifyOtp();
    fixture.detectChanges();

    expect(component.otpError()).toBe('That code did not work. 4 attempt(s) left.');
  });

  it('should return to OTP request phase after max verify attempts', async () => {
    authApiServiceSpy.verifyOtp.and.returnValue(throwError(() => ({ status: 401 })));
    component.otpForm.patchValue({
      email: 'user@test.com',
    });

    await component.requestOtp();
    component.otpForm.patchValue({ code: '123456' });

    for (let attempt = 0; attempt < 5; attempt += 1) {
      await component.verifyOtp();
    }

    expect(component.otpPhase()).toBe('request');
    expect(component.otpError()).toBe('Too many incorrect attempts. Request a new code.');
  });

  it('should register automatically when password login fails for first-time user', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.loginPassword.and.returnValue(throwError(() => ({ status: 401 })));
    component.passwordForm.setValue({
      email: 'new@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();

    expect(authApiServiceSpy.loginPassword).toHaveBeenCalledWith('new@test.com', 'Passw0rd!');
    expect(authApiServiceSpy.register).toHaveBeenCalledWith('new@test.com', 'Passw0rd!');
    expect(component.passwordSuccessMessage()).toContain('Account created');
    expect(component.awaitingEmailConfirmation()).toBe('new@test.com');
    expect(component.passwordForm.controls.password.getRawValue()).toBe('Passw0rd!');
  });

  it('should retry login without re-registering when continuing after confirmation', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.loginPassword.and.returnValues(
      throwError(() => ({ status: 401 })),
      throwError(() => ({ status: 401 })),
    );
    component.passwordForm.setValue({
      email: 'new@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();
    await component.continueAfterConfirmation();

    expect(authApiServiceSpy.register).toHaveBeenCalledTimes(1);
    expect(component.passwordError()).toBe('Not confirmed yet. Confirm your email and click "Continue after confirmation".');
  });

  it('should complete login after confirmation and navigate home', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.loginPassword.and.returnValues(
      throwError(() => ({ status: 401 })),
      of({}),
    );
    component.passwordForm.setValue({
      email: 'new@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();
    await component.continueAfterConfirmation();

    expect(authApiServiceSpy.register).toHaveBeenCalledTimes(1);
    expect(authSessionStoreSpy.refreshSession).toHaveBeenCalledTimes(1);
    expect(routerSpy.navigateByUrl).toHaveBeenCalledWith('/user');
    expect(component.awaitingEmailConfirmation()).toBeNull();
  });

  it('should show password policy validation errors when auto-registration fails', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.loginPassword.and.returnValue(throwError(() => ({ status: 401 })));
    authApiServiceSpy.register.and.returnValue(throwError(() => ({
      status: 400,
      error: {
        errors: {
          PasswordRequiresNonAlphanumeric: ['Passwords must have at least one non alphanumeric character.'],
          PasswordRequiresLower: ["Passwords must have at least one lowercase ('a'-'z')."],
          PasswordRequiresUpper: ["Passwords must have at least one uppercase ('A'-'Z')."],
        },
      },
    })));
    component.passwordForm.setValue({
      email: 'new@test.com',
      password: 'password',
    });

    await component.submitPasswordLogin();

    expect(component.passwordError()).toContain('non alphanumeric');
    expect(component.passwordError()).toContain("lowercase ('a'-'z')");
    expect(component.passwordError()).toContain("uppercase ('A'-'Z')");
    expect(component.passwordError()).toContain('\n');
  });

  it('should not auto-register when password login fails with non-401 error', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.loginPassword.and.returnValue(throwError(() => ({ status: 500 })));
    component.passwordForm.setValue({
      email: 'user@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();

    expect(authApiServiceSpy.register).not.toHaveBeenCalled();
    expect(component.passwordError()).toBe('Could not sign in right now. Please try again.');
  });
});
