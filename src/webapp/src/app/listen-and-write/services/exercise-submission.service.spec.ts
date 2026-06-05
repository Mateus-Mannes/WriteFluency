import { HttpErrorResponse } from '@angular/common/http';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { TextComparisonsService } from 'src/api/listen-and-write';
import { BrowserService } from '../../core/services/browser.service';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import { ExerciseSubmissionService } from './exercise-submission.service';

describe('ExerciseSubmissionService', () => {
  let service: ExerciseSubmissionService;
  let textComparisonsServiceMock: jasmine.SpyObj<TextComparisonsService>;
  let browserServiceMock: jasmine.SpyObj<BrowserService>;
  let trackingMock: jasmine.SpyObj<ExerciseSessionTrackingService>;
  let originalGtag: unknown;

  beforeEach(() => {
    originalGtag = (globalThis as typeof globalThis & { gtag?: unknown }).gtag;
    delete (globalThis as typeof globalThis & { gtag?: unknown }).gtag;

    textComparisonsServiceMock = jasmine.createSpyObj<TextComparisonsService>(
      'TextComparisonsService',
      ['apiTextComparisonCompareTextsPost'],
    );
    browserServiceMock = jasmine.createSpyObj<BrowserService>(
      'BrowserService',
      ['isBrowserEnvironment'],
    );
    trackingMock = jasmine.createSpyObj<ExerciseSessionTrackingService>(
      'ExerciseSessionTrackingService',
      ['trackEvent', 'markSubmitted', 'trackTextChanged'],
    );

    browserServiceMock.isBrowserEnvironment.and.returnValue(true);

    TestBed.configureTestingModule({
      providers: [
        ExerciseSubmissionService,
        {
          provide: TextComparisonsService,
          useValue: textComparisonsServiceMock,
        },
        {
          provide: BrowserService,
          useValue: browserServiceMock,
        },
        {
          provide: ExerciseSessionTrackingService,
          useValue: trackingMock,
        },
      ],
    });

    service = TestBed.inject(ExerciseSubmissionService);
  });

  afterEach(() => {
    if (originalGtag === undefined) {
      delete (globalThis as typeof globalThis & { gtag?: unknown }).gtag;
      return;
    }

    (globalThis as typeof globalThis & { gtag?: unknown }).gtag = originalGtag;
  });

  it('should return submit warning until audio reaches the last 10 seconds', () => {
    const warning = service.getSubmitWarningMessage({
      audioEnded: false,
      currentTime: 20,
      duration: 50,
    });

    expect(warning).toContain('Goal: write as much of the full audio text as you can.');

    const noWarning = service.getSubmitWarningMessage({
      audioEnded: false,
      currentTime: 40,
      duration: 50,
    });

    expect(noWarning).toBeNull();
  });

  it('should submit text, keep minimum loading state, track telemetry, and call success', fakeAsync(() => {
    const onSuccess = jasmine.createSpy('onSuccess');
    const gtagSpy = jasmine.createSpy('gtag');
    (globalThis as typeof globalThis & { gtag?: typeof gtagSpy }).gtag = gtagSpy;
    textComparisonsServiceMock.apiTextComparisonCompareTextsPost.and.returnValue(of({
      originalText: 'original text',
      userText: 'submitted text',
      comparisons: [{ kind: 'match' }],
      accuracyPercentage: 0.8,
    } as any) as any);

    service.submit({
      proposition: { id: 20, title: 'Exercise 20' } as any,
      exerciseId: 20,
      submittedUserText: 'submitted text',
      exerciseTimeUsedMs: 1200,
      onSuccess,
      onProRequired: jasmine.createSpy('onProRequired'),
      onFailure: jasmine.createSpy('onFailure'),
    });

    expect(service.isSubmitting()).toBeTrue();
    expect(textComparisonsServiceMock.apiTextComparisonCompareTextsPost).toHaveBeenCalledWith({
      propositionId: 20,
      userText: 'submitted text',
    });
    expect(trackingMock.markSubmitted).toHaveBeenCalled();
    expect(trackingMock.trackTextChanged).toHaveBeenCalledWith('submitted text');

    tick(1999);
    expect(onSuccess).not.toHaveBeenCalled();
    expect(service.isSubmitting()).toBeTrue();

    tick(1);
    expect(service.isSubmitting()).toBeFalse();
    expect(onSuccess).toHaveBeenCalledWith(jasmine.objectContaining({
      accuracyPercentage: 0.8,
    }), 'submitted text');
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'exercise_submit_succeeded',
      jasmine.objectContaining({
        comparison_count: 1,
      }),
      jasmine.objectContaining({
        accuracy_percentage: 0.8,
        exercise_time_used_ms: 1200,
      }),
    );
    expect(gtagSpy).toHaveBeenCalledWith('event', 'conversion', jasmine.objectContaining({
      send_to: 'AW-17978787910/WruICPy4xoAcEMaQ-vxC',
    }));
  }));

  it('should call Pro-required callback for Pro submit failures', fakeAsync(() => {
    const onProRequired = jasmine.createSpy('onProRequired');
    const onFailure = jasmine.createSpy('onFailure');
    textComparisonsServiceMock.apiTextComparisonCompareTextsPost.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 403,
      error: {
        access: 'pro_required',
      },
    })));

    service.submit({
      proposition: { id: 21, title: 'Exercise 21' } as any,
      exerciseId: 21,
      submittedUserText: 'submitted text',
      exerciseTimeUsedMs: null,
      onSuccess: jasmine.createSpy('onSuccess'),
      onProRequired,
      onFailure,
    });

    tick(2000);

    expect(service.isSubmitting()).toBeFalse();
    expect(onProRequired).toHaveBeenCalled();
    expect(onFailure).not.toHaveBeenCalled();
  }));
});
