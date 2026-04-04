import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ConfirmEmailComponent } from './confirm-email.component';
import { AuthApiService } from '../services/auth-api.service';

describe('ConfirmEmailComponent', () => {
  let component: ConfirmEmailComponent;
  let fixture: ComponentFixture<ConfirmEmailComponent>;
  let authApiServiceSpy: jasmine.SpyObj<AuthApiService>;

  beforeEach(async () => {
    authApiServiceSpy = jasmine.createSpyObj<AuthApiService>('AuthApiService', ['confirmEmail']);
    authApiServiceSpy.confirmEmail.and.returnValue(of({}));

    await TestBed.configureTestingModule({
      imports: [ConfirmEmailComponent],
      providers: [
        { provide: AuthApiService, useValue: authApiServiceSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {
                get: (key: string) => (key === 'userId' ? 'user-id' : key === 'code' ? 'code-value' : null),
              },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ConfirmEmailComponent);
    component = fixture.componentInstance;
  });

  it('should confirm email successfully', async () => {
    await component.ngOnInit();

    expect(authApiServiceSpy.confirmEmail).toHaveBeenCalledWith('user-id', 'code-value');
    expect(component.isSuccess()).toBeTrue();
    expect(component.message()).toBe('Your email is confirmed. You can now sign in.');
    expect(component.helperMessage()).toContain('go back there and continue');
  });

  it('should show friendly error when confirmation fails', async () => {
    authApiServiceSpy.confirmEmail.and.returnValue(throwError(() => ({ status: 400 })));

    await component.ngOnInit();

    expect(component.isSuccess()).toBeFalse();
    expect(component.message()).toBe('This confirmation link is invalid or expired. Please request a new confirmation email.');
    expect(component.helperMessage()).toBeNull();
  });

  it('should show invalid-link message when query parameters are missing', async () => {
    await TestBed.resetTestingModule()
      .configureTestingModule({
        imports: [ConfirmEmailComponent],
        providers: [
          { provide: AuthApiService, useValue: authApiServiceSpy },
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
      })
      .compileComponents();

    fixture = TestBed.createComponent(ConfirmEmailComponent);
    component = fixture.componentInstance;

    await component.ngOnInit();

    expect(authApiServiceSpy.confirmEmail).not.toHaveBeenCalled();
    expect(component.isSuccess()).toBeFalse();
    expect(component.message()).toBe('This confirmation link is invalid or incomplete. Please request a new confirmation email.');
  });
});
