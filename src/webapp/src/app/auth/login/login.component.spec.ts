import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthApiService } from '../services/auth-api.service';
import { AuthSessionStore } from '../services/auth-session.store';
import { GoogleAdsConversionService } from '../../core/services/google-ads-conversion.service';
import { GuestExerciseProgressTransferService } from '../../core/services/guest-exercise-progress-transfer.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authApiServiceSpy: jasmine.SpyObj<AuthApiService>;
  let authSessionStoreSpy: jasmine.SpyObj<AuthSessionStore>;
  let routerSpy: jasmine.SpyObj<Router>;
  let googleAdsConversionServiceSpy: jasmine.SpyObj<GoogleAdsConversionService>;
  let guestProgressTransferSpy: jasmine.SpyObj<GuestExerciseProgressTransferService>;

  beforeEach(async () => {
    authApiServiceSpy = jasmine.createSpyObj<AuthApiService>('AuthApiService', [
      'loginPassword',
      'register',
      'continueWithPassword',
      'requestOtp',
      'verifyOtp',
      'externalProviders',
    ]);
    authSessionStoreSpy = jasmine.createSpyObj<AuthSessionStore>('AuthSessionStore', ['refreshSession', 'userId']);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);
    googleAdsConversionServiceSpy = jasmine.createSpyObj<GoogleAdsConversionService>('GoogleAdsConversionService', ['trackSignup']);
    guestProgressTransferSpy = jasmine.createSpyObj<GuestExerciseProgressTransferService>(
      'GuestExerciseProgressTransferService',
      ['markAccountCreationStarted', 'resolveSuccessfulLogin'],
    );

    authApiServiceSpy.externalProviders.and.returnValue(of([]));
    authApiServiceSpy.loginPassword.and.returnValue(of({}));
    authApiServiceSpy.register.and.returnValue(of({}));
    authApiServiceSpy.continueWithPassword.and.returnValue(of({ status: 'signed_in', isNewUser: false }));
    authApiServiceSpy.requestOtp.and.returnValue(of({ message: 'Code sent.' }));
    authApiServiceSpy.verifyOtp.and.returnValue(of({ isNewUser: false }));
    authSessionStoreSpy.refreshSession.and.returnValue(Promise.resolve());
    authSessionStoreSpy.userId.and.returnValue('user-1');
    routerSpy.navigateByUrl.and.returnValue(Promise.resolve(true));
    window.sessionStorage.removeItem('wf.auth.post-login-return-url.v1');

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        { provide: AuthApiService, useValue: authApiServiceSpy },
        { provide: AuthSessionStore, useValue: authSessionStoreSpy },
        { provide: Router, useValue: routerSpy },
        { provide: GoogleAdsConversionService, useValue: googleAdsConversionServiceSpy },
        { provide: GuestExerciseProgressTransferService, useValue: guestProgressTransferSpy },
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

    expect(authApiServiceSpy.continueWithPassword).toHaveBeenCalledWith('user@test.com', 'Passw0rd!', true);
    expect(authSessionStoreSpy.refreshSession).toHaveBeenCalled();
    expect(guestProgressTransferSpy.resolveSuccessfulLogin).toHaveBeenCalledWith(
      'direct',
      false,
      'user-1',
    );
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

  it('should track signup conversion when OTP verification creates a new user', async () => {
    authApiServiceSpy.verifyOtp.and.returnValue(of({ isNewUser: true }));
    component.otpForm.patchValue({
      email: 'new@test.com',
    });
    await component.requestOtp();
    component.otpForm.patchValue({
      code: '123456',
    });

    await component.verifyOtp();

    expect(googleAdsConversionServiceSpy.trackSignup).toHaveBeenCalledWith('new@test.com');
    expect(guestProgressTransferSpy.resolveSuccessfulLogin).toHaveBeenCalledWith(
      'direct',
      true,
      'user-1',
    );
    expect(authSessionStoreSpy.refreshSession).toHaveBeenCalled();
    expect(routerSpy.navigateByUrl).toHaveBeenCalledWith('/user');
  });

  it('should not track signup conversion when OTP verification signs in an existing user', async () => {
    component.otpForm.patchValue({
      email: 'existing@test.com',
    });
    await component.requestOtp();
    component.otpForm.patchValue({
      code: '123456',
    });

    await component.verifyOtp();

    expect(googleAdsConversionServiceSpy.trackSignup).not.toHaveBeenCalled();
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
    authApiServiceSpy.continueWithPassword.and.returnValue(of({ status: 'confirmation_required', isNewUser: true }));
    component.passwordForm.setValue({
      email: 'new@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();

    expect(authApiServiceSpy.continueWithPassword).toHaveBeenCalledWith('new@test.com', 'Passw0rd!', true);
    expect(authApiServiceSpy.register).not.toHaveBeenCalled();
    expect(googleAdsConversionServiceSpy.trackSignup).toHaveBeenCalledWith('new@test.com');
    expect(guestProgressTransferSpy.markAccountCreationStarted).toHaveBeenCalledWith('direct');
    expect(component.passwordSuccessMessage()).toContain('Account created');
    expect(component.awaitingEmailConfirmation()).toBe('new@test.com');
    expect(component.passwordForm.controls.password.getRawValue()).toBe('Passw0rd!');
  });

  it('should retry login without re-registering when continuing after confirmation', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.continueWithPassword.and.returnValues(
      of({ status: 'confirmation_required', isNewUser: true }),
      of({ status: 'confirmation_required', isNewUser: false }),
    );
    component.passwordForm.setValue({
      email: 'new@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();
    await component.continueAfterConfirmation();

    expect(authApiServiceSpy.register).not.toHaveBeenCalled();
    expect(authApiServiceSpy.continueWithPassword.calls.argsFor(1)).toEqual(['new@test.com', 'Passw0rd!', false]);
    expect(component.passwordSuccessMessage()).toContain('already sent');
    expect(component.passwordMessageTone()).toBe('warning');
  });

  it('should complete login after confirmation and navigate home', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.continueWithPassword.and.returnValues(
      of({ status: 'confirmation_required', isNewUser: true }),
      of({ status: 'signed_in', isNewUser: false }),
    );
    component.passwordForm.setValue({
      email: 'new@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();
    await component.continueAfterConfirmation();

    expect(authApiServiceSpy.register).not.toHaveBeenCalled();
    expect(authSessionStoreSpy.refreshSession).toHaveBeenCalledTimes(1);
    expect(routerSpy.navigateByUrl).toHaveBeenCalledWith('/user');
    expect(component.awaitingEmailConfirmation()).toBeNull();
  });

  it('should show password policy validation errors when auto-registration fails', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.continueWithPassword.and.returnValue(throwError(() => ({
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
    authApiServiceSpy.continueWithPassword.and.returnValue(throwError(() => ({ status: 500 })));
    component.passwordForm.setValue({
      email: 'user@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();

    expect(authApiServiceSpy.register).not.toHaveBeenCalled();
    expect(component.passwordError()).toBe('Could not continue right now. Please try again.');
  });

  it('should show setup message when password is not set for an existing account', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.continueWithPassword.and.returnValue(of({ status: 'password_setup_required', isNewUser: false }));
    component.passwordForm.setValue({
      email: 'social@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();

    expect(component.passwordSuccessMessage()).toContain('confirm this email and add password sign-in');
    expect(component.passwordMessageTone()).toBe('success');
    expect(component.usePasswordLogin()).toBeTrue();
    expect(component.awaitingEmailConfirmation()).toBe('social@test.com');
    expect(googleAdsConversionServiceSpy.trackSignup).not.toHaveBeenCalled();
  });

  it('should show pending password setup check as warning', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.continueWithPassword.and.returnValues(
      of({ status: 'password_setup_required', isNewUser: false }),
      of({ status: 'password_setup_required', isNewUser: false }),
    );
    component.passwordForm.setValue({
      email: 'social@test.com',
      password: 'Passw0rd!',
    });

    await component.submitPasswordLogin();
    await component.continueAfterConfirmation();

    expect(authApiServiceSpy.continueWithPassword.calls.argsFor(1)).toEqual(['social@test.com', 'Passw0rd!', false]);
    expect(component.passwordSuccessMessage()).toContain('still not confirmed');
    expect(component.passwordMessageTone()).toBe('warning');
  });

  it('should show wrong password message without registering', async () => {
    component.toggleLoginMode();
    authApiServiceSpy.continueWithPassword.and.returnValue(of({ status: 'wrong_password', isNewUser: false }));
    component.passwordForm.setValue({
      email: 'existing@test.com',
      password: 'WrongPassw0rd!',
    });

    await component.submitPasswordLogin();

    expect(authApiServiceSpy.register).not.toHaveBeenCalled();
    expect(component.passwordError()).toBe('Wrong password. Try again or use email code instead.');
  });
});
