import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../enviroments/enviroment';
import { SupportApiService } from './support-api.service';

describe('SupportApiService', () => {
  let service: SupportApiService;
  let httpMock: HttpTestingController;
  let supportRequestsUrl: string;

  beforeEach(() => {
    supportRequestsUrl = `${environment.usersApiUrl.replace(/\/$/, '')}/users/support/requests`;

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [SupportApiService],
    });

    service = TestBed.inject(SupportApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should post support requests with credentials enabled', () => {
    service.submitRequest({
      message: 'Please help.',
      replyEmail: 'guest@writefluency.test',
      sourceUrl: 'http://localhost:4200/support',
    }).subscribe();

    const request = httpMock.expectOne(supportRequestsUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({
      message: 'Please help.',
      replyEmail: 'guest@writefluency.test',
      sourceUrl: 'http://localhost:4200/support',
    });
    request.flush({ accepted: true });
  });
});
