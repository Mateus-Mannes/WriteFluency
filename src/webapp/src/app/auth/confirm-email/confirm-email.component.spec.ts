import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
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
  });
});
