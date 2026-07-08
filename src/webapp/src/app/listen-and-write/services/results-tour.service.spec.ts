import { TestBed } from '@angular/core/testing';
import { ShepherdService } from 'angular-shepherd';
import { BrowserService } from '../../core/services/browser.service';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import { ResultsTourService } from './results-tour.service';
import * as constants from '../listen-and-write.constants';

describe('ResultsTourService', () => {
  let service: ResultsTourService;
  let shepherdMock: any;
  let browserServiceMock: jasmine.SpyObj<BrowserService>;
  let trackingMock: jasmine.SpyObj<ExerciseSessionTrackingService>;
  let tourCallbacks: Record<string, () => void>;
  let host: HTMLDivElement;

  const anonymousSampleResult = {
    mistakePatternStatus: 'generated',
    mistakePatternReviewSource: 'anonymous_sample',
  } as any;

  beforeEach(() => {
    host = document.createElement('div');
    document.body.appendChild(host);
    tourCallbacks = {};
    shepherdMock = {
      isActive: false,
      defaultStepOptions: {},
      modal: false,
      keyboardNavigation: true,
      addSteps: jasmine.createSpy('addSteps'),
      start: jasmine.createSpy('start'),
      next: jasmine.createSpy('next'),
      back: jasmine.createSpy('back'),
      complete: jasmine.createSpy('complete'),
      cancel: jasmine.createSpy('cancel'),
      tourObject: {
        on: jasmine.createSpy('on').and.callFake((eventName: string, callback: () => void) => {
          tourCallbacks[eventName] = callback;
        }),
      },
    };
    browserServiceMock = jasmine.createSpyObj<BrowserService>(
      'BrowserService',
      ['isBrowserEnvironment', 'getItem', 'setItem'],
    );
    browserServiceMock.isBrowserEnvironment.and.returnValue(true);
    browserServiceMock.getItem.and.returnValue(null);
    trackingMock = jasmine.createSpyObj<ExerciseSessionTrackingService>(
      'ExerciseSessionTrackingService',
      ['trackEvent'],
    );

    TestBed.configureTestingModule({
      providers: [
        ResultsTourService,
        { provide: ShepherdService, useValue: shepherdMock },
        { provide: BrowserService, useValue: browserServiceMock },
        { provide: ExerciseSessionTrackingService, useValue: trackingMock },
      ],
    });

    service = TestBed.inject(ResultsTourService);
  });

  afterEach(() => {
    host.remove();
  });

  it('should start desktop tour for anonymous generated sample result', () => {
    renderDesktopAnchors();

    const started = service.maybeStartAnonymousSampleTour(
      anonymousSampleResult,
      false,
      false);

    expect(started).toBeTrue();
    expect(shepherdMock.addSteps).toHaveBeenCalled();
    expect(shepherdMock.addSteps.calls.mostRecent().args[0].length).toBe(3);
    const steps = shepherdMock.addSteps.calls.mostRecent().args[0];
    expect(getButtonTexts(steps)).not.toContain('Skip');
    expect(steps[1].attachTo.element).toBe('#results-highlighted-text-panels');
    expect(steps[1].text).toContain('Green highlights show the matching words in the original text');
    expect(steps[1].text).toContain('Hover over either color');
    expect(steps[1].buttons.map((button: any) => button.text)).toEqual(['Back', 'Next']);
    expect(steps[2].text).toContain('Hover over a correction here');
    expect(steps[2].text).toContain('yellow and green text on the left');
    expect(steps[2].buttons.map((button: any) => button.text)).toEqual(['Back', 'Got it']);
    expect(shepherdMock.start).toHaveBeenCalled();
    expect(trackingMock.trackEvent).toHaveBeenCalledWith('shepherd_tour_opened', {
      tour_name: 'anonymous-results-tour',
    });
  });

  it('should start mobile tour with shorter step list', () => {
    renderMobileAnchors();

    const started = service.maybeStartAnonymousSampleTour(
      anonymousSampleResult,
      false,
      true);

    expect(started).toBeTrue();
    const steps = shepherdMock.addSteps.calls.mostRecent().args[0];
    expect(steps.length).toBe(2);
    expect(getButtonTexts(steps)).not.toContain('Skip');
    expect(steps[0].text).toContain('Yellow highlights are your words');
    expect(steps[0].text).toContain('green highlights are the original text');
    expect(steps[1].text).toContain('Tap a correction to connect it back');
    expect(steps[1].buttons.map((button: any) => button.text)).toEqual(['Back', 'Got it']);
  });

  it('should suppress when anonymous sample tour was already seen', () => {
    browserServiceMock.getItem
      .withArgs(constants.anonymousSampleResultsTourStorageKey)
      .and.returnValue('done');
    renderDesktopAnchors();

    const started = service.maybeStartAnonymousSampleTour(
      anonymousSampleResult,
      false,
      false);

    expect(started).toBeFalse();
    expect(shepherdMock.start).not.toHaveBeenCalled();
    expect(trackingMock.trackEvent).toHaveBeenCalledWith('anonymous_results_tour_suppressed', {
      reason: 'already_seen',
    });
  });

  it('should suppress when user is authenticated', () => {
    renderDesktopAnchors();

    const started = service.maybeStartAnonymousSampleTour(
      anonymousSampleResult,
      true,
      false);

    expect(started).toBeFalse();
    expect(trackingMock.trackEvent).toHaveBeenCalledWith('anonymous_results_tour_suppressed', {
      reason: 'authenticated_user',
    });
  });

  it('should suppress safely when required anchors are missing', () => {
    host.innerHTML = '<div id="results-summary-panel"></div>';

    const started = service.maybeStartAnonymousSampleTour(
      anonymousSampleResult,
      false,
      false);

    expect(started).toBeFalse();
    expect(shepherdMock.start).not.toHaveBeenCalled();
    expect(trackingMock.trackEvent).toHaveBeenCalledWith(
      'anonymous_results_tour_suppressed',
      jasmine.objectContaining({
        reason: 'missing_anchor',
      }));
  });

  it('should mark tour as seen when completed or cancelled', () => {
    renderDesktopAnchors();

    service.maybeStartAnonymousSampleTour(
      anonymousSampleResult,
      false,
      false);
    tourCallbacks['complete']?.();

    expect(browserServiceMock.setItem).toHaveBeenCalledWith(
      constants.anonymousSampleResultsTourStorageKey,
      'done');
    expect(trackingMock.trackEvent).toHaveBeenCalledWith('shepherd_tour_finished', {
      tour_name: 'anonymous-results-tour',
      outcome: 'complete',
    });

    browserServiceMock.setItem.calls.reset();
    trackingMock.trackEvent.calls.reset();
    service.maybeStartAnonymousSampleTour(
      anonymousSampleResult,
      false,
      false);
    tourCallbacks['cancel']?.();

    expect(browserServiceMock.setItem).toHaveBeenCalledWith(
      constants.anonymousSampleResultsTourStorageKey,
      'done');
    expect(trackingMock.trackEvent).toHaveBeenCalledWith('shepherd_tour_finished', {
      tour_name: 'anonymous-results-tour',
      outcome: 'cancel',
    });
  });

  function renderDesktopAnchors(): void {
    host.innerHTML = `
      <div id="results-summary-panel"></div>
      <div id="results-highlighted-text-panels"></div>
      <div id="results-mistake-pattern-review-panel"></div>
    `;
  }

  function renderMobileAnchors(): void {
    host.innerHTML = `
      <div id="results-summary-panel"></div>
      <div id="results-mistake-pattern-review-panel"></div>
    `;
  }

  function getButtonTexts(steps: any[]): string[] {
    return steps.flatMap(step => step.buttons?.map((button: any) => button.text) ?? []);
  }
});
