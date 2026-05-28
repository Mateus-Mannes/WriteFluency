import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../enviroments/enviroment';
import { BillingApiService } from './billing-api.service';

describe('BillingApiService', () => {
  let service: BillingApiService;
  let httpMock: HttpTestingController;
  let billingBaseUrl: string;

  beforeEach(() => {
    billingBaseUrl = `${environment.usersApiUrl.replace(/\/$/, '')}/users/billing`;

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [BillingApiService],
    });

    service = TestBed.inject(BillingApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should create checkout session with credentials', () => {
    service.createCheckoutSession().subscribe();

    const request = httpMock.expectOne(`${billingBaseUrl}/checkout-session`);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({
      status: 'checkout_created',
      checkoutUrl: 'https://checkout.stripe.test/session',
      plan: 'free',
      entitlementStatus: 'free',
      isPro: false,
      currentPeriodEndUtc: null,
      cancelAtPeriodEnd: false,
    });
  });

  it('should confirm checkout session with credentials', () => {
    service.confirmCheckoutSession('cs_test_123').subscribe();

    const request = httpMock.expectOne(`${billingBaseUrl}/checkout-session/confirm`);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({ sessionId: 'cs_test_123' });
    request.flush({
      plan: 'pro',
      entitlementStatus: 'pro_active',
      isPro: true,
      currentPeriodEndUtc: new Date('2030-01-01T00:00:00.000Z').toISOString(),
      cancelAtPeriodEnd: false,
    });
  });
});
