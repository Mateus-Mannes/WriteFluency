import { TestBed } from '@angular/core/testing';
import { BrowserService } from '../../core/services/browser.service';
import { ProReviewPendingRequestService } from './pro-review-pending-request.service';

describe('ProReviewPendingRequestService', () => {
  let service: ProReviewPendingRequestService;
  let browserService: jasmine.SpyObj<BrowserService>;
  let sessionValue: string | null;

  beforeEach(() => {
    sessionValue = null;
    browserService = jasmine.createSpyObj<BrowserService>(
      'BrowserService',
      ['getSessionItem', 'setSessionItem', 'removeSessionItem']);
    browserService.getSessionItem.and.callFake(() => sessionValue);
    browserService.setSessionItem.and.callFake((_key, value) => {
      sessionValue = value;
    });
    browserService.removeSessionItem.and.callFake(() => {
      sessionValue = null;
    });

    TestBed.configureTestingModule({
      providers: [
        ProReviewPendingRequestService,
        { provide: BrowserService, useValue: browserService },
      ],
    });

    service = TestBed.inject(ProReviewPendingRequestService);
  });

  it('stores and consumes a pending review request for the same exercise', () => {
    service.save(42, 'draft answer', '/english-writing-exercise/42');

    const request = service.consumeForExercise(42);

    expect(request?.exerciseId).toBe(42);
    expect(request?.draftUserText).toBe('draft answer');
    expect(request?.returnUrl).toBe('/english-writing-exercise/42');
    expect(request?.source).toBe('pro_review_login_cta');
    expect(browserService.removeSessionItem).toHaveBeenCalled();
  });

  it('does not consume a request for a different exercise', () => {
    service.save(42, 'draft answer', '/english-writing-exercise/42');

    const request = service.consumeForExercise(99);

    expect(request).toBeNull();
    expect(sessionValue).not.toBeNull();
  });

  it('clears expired requests', () => {
    sessionValue = JSON.stringify({
      exerciseId: 42,
      draftUserText: 'draft answer',
      returnUrl: '/english-writing-exercise/42',
      createdAtUtc: '2026-07-08T00:00:00.000Z',
      expiresAtUtc: '2000-01-01T00:00:00.000Z',
      source: 'pro_review_login_cta',
    });

    const request = service.consumeForExercise(42);

    expect(request).toBeNull();
    expect(browserService.removeSessionItem).toHaveBeenCalled();
  });
});
