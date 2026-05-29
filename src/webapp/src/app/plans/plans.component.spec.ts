import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { PlansComponent } from './plans.component';
import { AuthSessionState } from '../auth/models/auth-session.model';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { CheckoutSessionResponse } from '../user/models/billing.model';
import { BillingApiService } from '../user/services/billing-api.service';
import { BrowserService } from '../core/services/browser.service';
import { Insights } from '../../telemetry/insights.service';

describe('PlansComponent', () => {
  let component: PlansComponent;
  let fixture: ComponentFixture<PlansComponent>;
  let billingApiSpy: jasmine.SpyObj<BillingApiService>;
  let browserServiceSpy: jasmine.SpyObj<BrowserService>;
  let insightsSpy: jasmine.SpyObj<Insights>;
  let router: Router;
  let authSessionStoreMock: {
    state: ReturnType<typeof signal<AuthSessionState>>;
    refreshSession: jasmine.Spy<() => Promise<void>>;
  };

  beforeEach(async () => {
    billingApiSpy = jasmine.createSpyObj<BillingApiService>('BillingApiService', [
      'createCheckoutSession',
      'createPortalSession',
    ]);
    browserServiceSpy = jasmine.createSpyObj<BrowserService>('BrowserService', ['navigateTo']);
    insightsSpy = jasmine.createSpyObj<Insights>('Insights', ['trackException']);
    authSessionStoreMock = {
      state: signal({
        isAuthenticated: true,
        userId: 'user-123',
        email: 'user@test.com',
        emailConfirmed: true,
        listenWriteTutorialCompleted: true,
        plan: 'free',
        entitlementStatus: 'free',
        isPro: false,
        currentPeriodEndUtc: null,
        cancelAtPeriodEnd: false,
        hasReliableSessionState: true,
        issuedAtUtc: new Date().toISOString(),
        expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        isLoading: false,
        error: null,
      }),
      refreshSession: jasmine.createSpy('refreshSession').and.resolveTo(),
    };

    billingApiSpy.createCheckoutSession.and.returnValue(of({
      status: 'checkout_created',
      checkoutUrl: 'https://checkout.stripe.test/session',
      plan: 'free',
      entitlementStatus: 'free',
      isPro: false,
      currentPeriodEndUtc: null,
      cancelAtPeriodEnd: false,
    }));
    billingApiSpy.createPortalSession.and.returnValue(of({
      portalUrl: 'https://billing.stripe.test/session',
    }));

    await TestBed.configureTestingModule({
      imports: [PlansComponent],
      providers: [
        provideRouter([]),
        { provide: AuthSessionStore, useValue: authSessionStoreMock },
        { provide: BillingApiService, useValue: billingApiSpy },
        { provide: BrowserService, useValue: browserServiceSpy },
        { provide: Insights, useValue: insightsSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(PlansComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should render initial Pro CTA', () => {
    const root: HTMLElement = fixture.nativeElement;

    expect(root.textContent).toContain('Pro Monthly');
    expect(root.textContent).toContain('Subscribe to Pro');
  });

  it('should show a portal management CTA for Pro users', () => {
    authSessionStoreMock.state.set({
      ...authSessionStoreMock.state(),
      plan: 'pro',
      entitlementStatus: 'pro_active',
      isPro: true,
    });
    fixture.detectChanges();

    const cta = fixture.nativeElement.querySelector('.plan-cta') as HTMLButtonElement;

    expect(fixture.nativeElement.textContent).toContain('You are already Pro');
    expect(cta.textContent).toContain('Manage subscription');
    expect(cta.disabled).toBeFalse();
    expect(billingApiSpy.createCheckoutSession).not.toHaveBeenCalled();
  });

  it('should create portal session for Pro users and redirect to Stripe portal', fakeAsync(() => {
    authSessionStoreMock.state.set({
      ...authSessionStoreMock.state(),
      plan: 'pro',
      entitlementStatus: 'pro_active',
      isPro: true,
    });

    void component.startCheckout();
    flushMicrotasks();

    expect(billingApiSpy.createCheckoutSession).not.toHaveBeenCalled();
    expect(billingApiSpy.createPortalSession).toHaveBeenCalledTimes(1);
    expect(browserServiceSpy.navigateTo).toHaveBeenCalledWith('https://billing.stripe.test/session');

    tick(8000);
  }));

  it('should create checkout session, redirect to Stripe, and keep loading for 8 seconds', fakeAsync(() => {
    void component.startCheckout();
    flushMicrotasks();

    expect(billingApiSpy.createCheckoutSession).toHaveBeenCalledTimes(1);
    expect(browserServiceSpy.navigateTo).toHaveBeenCalledWith('https://checkout.stripe.test/session');
    expect(component.isCheckoutStarting()).toBeTrue();

    tick(7999);
    expect(component.isCheckoutStarting()).toBeTrue();

    tick(1);
    expect(component.isCheckoutStarting()).toBeFalse();
  }));

  it('should disable CTA and prevent duplicate checkout requests while loading', async () => {
    const checkoutResponse = new Subject<CheckoutSessionResponse>();
    billingApiSpy.createCheckoutSession.and.returnValue(checkoutResponse);

    void component.startCheckout();
    fixture.detectChanges();

    const cta = fixture.nativeElement.querySelector('.plan-cta') as HTMLButtonElement;
    expect(component.isCheckoutStarting()).toBeTrue();
    expect(cta.disabled).toBeTrue();

    await component.startCheckout();

    expect(billingApiSpy.createCheckoutSession).toHaveBeenCalledTimes(1);
  });

  it('should send unauthenticated users to login', async () => {
    authSessionStoreMock.state.set({
      ...authSessionStoreMock.state(),
      isAuthenticated: false,
    });

    await component.startCheckout();

    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], {
      queryParams: {
        returnUrl: '/plans',
        source: 'plans_checkout',
      },
    });
    expect(billingApiSpy.createCheckoutSession).not.toHaveBeenCalled();
  });

  it('should show recoverable error and keep loading for 8 seconds when checkout creation fails', fakeAsync(() => {
    const checkoutError = new HttpErrorResponse({ status: 503, statusText: 'Unavailable' });
    billingApiSpy.createCheckoutSession.and.returnValue(throwError(() => checkoutError));

    void component.startCheckout();
    flushMicrotasks();
    fixture.detectChanges();

    expect(component.checkoutError()).toBe('Could not start checkout right now. Please try again.');
    expect(component.isCheckoutStarting()).toBeTrue();
    expect(insightsSpy.trackException).toHaveBeenCalledWith(checkoutError, jasmine.objectContaining({
      properties: jasmine.objectContaining({
        area: 'billing',
        operation: 'plans_start_checkout',
      }),
    }));

    tick(8000);
    expect(component.isCheckoutStarting()).toBeFalse();
  }));
});
