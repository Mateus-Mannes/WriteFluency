import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthApiService } from './auth-api.service';
import { environment } from '../../../enviroments/enviroment';

describe('AuthApiService', () => {
  let service: AuthApiService;
  let httpMock: HttpTestingController;
  let authBaseUrl: string;

  beforeEach(() => {
    authBaseUrl = `${environment.usersApiUrl.replace(/\/$/, '')}/users/auth`;

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthApiService],
    });

    service = TestBed.inject(AuthApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should call login endpoint with cookies enabled', () => {
    service.loginPassword('user@test.com', 'Passw0rd!').subscribe();

    const request = httpMock.expectOne((req) =>
      req.url === `${authBaseUrl}/login` && req.params.get('useCookies') === 'true',
    );

    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({});
  });

  it('should call password continue endpoint', () => {
    service.continueWithPassword('user@test.com', 'Passw0rd!').subscribe((response) => {
      expect(response.status).toBe('signed_in');
      expect(response.isNewUser).toBeFalse();
    });

    const request = httpMock.expectOne(`${authBaseUrl}/password/continue`);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({ email: 'user@test.com', password: 'Passw0rd!', sendEmail: true });
    request.flush({ status: 'signed_in', isNewUser: false });
  });

  it('should call password continue endpoint without sending email when requested', () => {
    service.continueWithPassword('user@test.com', 'Passw0rd!', false).subscribe((response) => {
      expect(response.status).toBe('confirmation_required');
      expect(response.isNewUser).toBeFalse();
    });

    const request = httpMock.expectOne(`${authBaseUrl}/password/continue`);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({ email: 'user@test.com', password: 'Passw0rd!', sendEmail: false });
    request.flush({ status: 'confirmation_required', isNewUser: false });
  });

  it('should call external providers endpoint', () => {
    service.externalProviders().subscribe();

    const request = httpMock.expectOne(`${authBaseUrl}/external/providers`);
    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBeTrue();
    request.flush([]);
  });

  it('should call passwordless verify endpoint and return new-user flag', () => {
    service.verifyOtp('user@test.com', '123456').subscribe((response) => {
      expect(response.isNewUser).toBeTrue();
    });

    const request = httpMock.expectOne(`${authBaseUrl}/passwordless/verify`);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({ email: 'user@test.com', code: '123456' });
    request.flush({ isNewUser: true });
  });

  it('should call tutorial mark-completed endpoint', () => {
    service.markListenWriteTutorialCompleted().subscribe();

    const request = httpMock.expectOne(`${authBaseUrl}/tutorial/listen-write/completed`);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({ listenWriteTutorialCompleted: true });
  });

  it('should call reset password endpoint', () => {
    service.resetPassword('user@test.com', 'reset-code', 'NewPassw0rd!').subscribe();

    const request = httpMock.expectOne(`${authBaseUrl}/resetPassword`);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({
      email: 'user@test.com',
      resetCode: 'reset-code',
      newPassword: 'NewPassw0rd!',
    });
    request.flush({});
  });

  it('should call password setup confirmation endpoint', () => {
    service.confirmPasswordSetup('setup-token').subscribe((response) => {
      expect(response.status).toBe('confirmed');
    });

    const request = httpMock.expectOne(`${authBaseUrl}/password/setup/confirm`);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({ token: 'setup-token' });
    request.flush({ status: 'confirmed' });
  });
});
