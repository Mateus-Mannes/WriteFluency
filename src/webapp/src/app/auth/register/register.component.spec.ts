import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { RegisterComponent } from './register.component';
import { AuthApiService } from '../services/auth-api.service';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;
  let authApiServiceSpy: jasmine.SpyObj<AuthApiService>;

  beforeEach(async () => {
    authApiServiceSpy = jasmine.createSpyObj<AuthApiService>('AuthApiService', ['register']);
    authApiServiceSpy.register.and.returnValue(of({}));

    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        { provide: AuthApiService, useValue: authApiServiceSpy },
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
    expect(component.successMessage()).toContain('Registration successful');
  });
});
