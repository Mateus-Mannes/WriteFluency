import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { RegisterComponent } from './register.component';
import { AuthApiService } from '../services/auth-api.service';
import { GoogleAdsConversionService } from '../../core/services/google-ads-conversion.service';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;
  let authApiServiceSpy: jasmine.SpyObj<AuthApiService>;
  let googleAdsConversionServiceSpy: jasmine.SpyObj<GoogleAdsConversionService>;

  beforeEach(async () => {
    authApiServiceSpy = jasmine.createSpyObj<AuthApiService>('AuthApiService', ['register']);
    googleAdsConversionServiceSpy = jasmine.createSpyObj<GoogleAdsConversionService>('GoogleAdsConversionService', ['trackSignup']);
    authApiServiceSpy.register.and.returnValue(of({}));

    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        { provide: AuthApiService, useValue: authApiServiceSpy },
        { provide: GoogleAdsConversionService, useValue: googleAdsConversionServiceSpy },
        provideRouter([]),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should show mismatch error when passwords differ', async () => {
    component.registerForm.setValue({
      email: 'user@test.com',
      password: 'Passw0rd!',
      confirmPassword: 'DiffPass!',
    });

    await component.submit();

    expect(component.errorMessage()).toBe('Password and confirmation must match.');
    expect(authApiServiceSpy.register).not.toHaveBeenCalled();
  });

  it('should register user and show success message', async () => {
    component.registerForm.setValue({
      email: 'user@test.com',
      password: 'Passw0rd!',
      confirmPassword: 'Passw0rd!',
    });

    await component.submit();

    expect(authApiServiceSpy.register).toHaveBeenCalledWith('user@test.com', 'Passw0rd!');
    expect(googleAdsConversionServiceSpy.trackSignup).toHaveBeenCalledWith('user@test.com');
    expect(component.successMessage()).toContain('Registration successful');
  });
});
