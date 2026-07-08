import { Injectable } from '@angular/core';
import { offset } from '@floating-ui/dom';
import { ShepherdService } from 'angular-shepherd';
import { TextComparisonResult } from 'src/api/listen-and-write';
import { BrowserService } from '../../core/services/browser.service';
import * as constants from '../listen-and-write.constants';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';

@Injectable({ providedIn: 'root' })
export class ResultsTourService {
  private static readonly tourName = 'anonymous-results-tour';

  constructor(
    private shepherd: ShepherdService,
    private browserService: BrowserService,
    private exerciseSessionTracking: ExerciseSessionTrackingService
  ) { }

  maybeStartAnonymousSampleTour(
    result: TextComparisonResult,
    isAuthenticated: boolean,
    isMobileLayout: boolean
  ): boolean {
    if (result.mistakePatternStatus !== 'generated'
        || result.mistakePatternReviewSource !== 'anonymous_sample') {
      return false;
    }

    if (isAuthenticated) {
      this.trackSuppressed('authenticated_user');
      return false;
    }

    if (this.hasSeenAnonymousSampleTour()) {
      this.trackSuppressed('already_seen');
      return false;
    }

    if (this.shepherd.isActive) {
      this.trackSuppressed('active_tour');
      return false;
    }

    const missingAnchors = this.getMissingAnchors(isMobileLayout);
    if (missingAnchors.length > 0) {
      this.trackSuppressed('missing_anchor', {
        missing_anchors: missingAnchors.join(','),
      });
      return false;
    }

    this.startTour(isMobileLayout);
    return true;
  }

  private startTour(isMobileLayout: boolean): void {
    this.shepherd.defaultStepOptions = {
      scrollTo: true,
      cancelIcon: { enabled: true },
      classes: 'wf-tour',
      modalOverlayOpeningPadding: 10,
      modalOverlayOpeningRadius: 10,
    };
    this.shepherd.modal = true;
    this.shepherd.keyboardNavigation = false;

    const steps = isMobileLayout
      ? this.createMobileSteps()
      : this.createDesktopSteps();

    this.shepherd.addSteps(this.withTrackedButtons(steps));
    this.exerciseSessionTracking.trackEvent('shepherd_tour_opened', {
      tour_name: ResultsTourService.tourName,
    });

    this.shepherd.tourObject?.on('complete', () => {
      this.markAnonymousSampleTourSeen();
      this.exerciseSessionTracking.trackEvent('shepherd_tour_finished', {
        tour_name: ResultsTourService.tourName,
        outcome: 'complete',
      });
    });

    this.shepherd.tourObject?.on('cancel', () => {
      this.markAnonymousSampleTourSeen();
      this.exerciseSessionTracking.trackEvent('shepherd_tour_finished', {
        tour_name: ResultsTourService.tourName,
        outcome: 'cancel',
      });
    });

    this.shepherd.start();
  }

  private createDesktopSteps(): Array<Record<string, unknown>> {
    return [
      {
        id: 'results-summary',
        title: 'Your result summary',
        text: 'Start here: accuracy gives you the overall match, and the word counter helps you spot missing or extra words.',
        attachTo: { element: '#results-summary-panel', on: 'right' },
        floatingUIOptions: { middleware: [offset(16)] },
        buttons: [
          { text: 'Next', classes: 'wf-primary', action: () => this.shepherd.next() },
        ],
      },
      {
        id: 'highlighted-texts',
        title: 'Compare the highlights',
        text: 'Yellow highlights show the words from your answer. Green highlights show the matching words in the original text. Hover over either color to see its counterpart and compare the correction in context.',
        attachTo: { element: '#results-highlighted-text-panels', on: 'top' },
        floatingUIOptions: { middleware: [offset(16)] },
        buttons: [
          { text: 'Back', classes: 'wf-secondary', action: () => this.shepherd.back() },
          { text: 'Next', classes: 'wf-primary', action: () => this.shepherd.next() },
        ],
      },
      {
        id: 'pro-review',
        title: 'Mistakes grouped by pattern',
        text: 'This Pro review groups your corrections into patterns and gives short explanations. Hover over a correction here to highlight the matching yellow and green text on the left, so you can find the mistake in its full sentence.',
        attachTo: { element: '#results-mistake-pattern-review-panel', on: 'left' },
        floatingUIOptions: { middleware: [offset(16)] },
        buttons: [
          { text: 'Back', classes: 'wf-secondary', action: () => this.shepherd.back() },
          { text: 'Got it', classes: 'wf-primary', action: () => this.shepherd.complete() },
        ],
      },
    ];
  }

  private createMobileSteps(): Array<Record<string, unknown>> {
    return [
      {
        id: 'mobile-results-and-highlights',
        title: 'Read your corrections',
        text: 'Your score summarizes the attempt. Yellow highlights are your words, green highlights are the original text, and tapping either one shows its counterpart.',
        attachTo: { element: '#results-summary-panel', on: 'bottom' },
        floatingUIOptions: { middleware: [offset(16)] },
        buttons: [
          { text: 'Next', classes: 'wf-primary', action: () => this.shepherd.next() },
        ],
      },
      {
        id: 'mobile-pro-review',
        title: 'Use the Pro review',
        text: 'Mistake patterns show the type of error and a short explanation. Tap a correction to connect it back to the highlighted words in your answer and the original text.',
        attachTo: { element: '#results-mistake-pattern-review-panel', on: 'top' },
        floatingUIOptions: { middleware: [offset(16)] },
        buttons: [
          { text: 'Back', classes: 'wf-secondary', action: () => this.shepherd.back() },
          { text: 'Got it', classes: 'wf-primary', action: () => this.shepherd.complete() },
        ],
      },
    ];
  }

  private getMissingAnchors(isMobileLayout: boolean): string[] {
    if (!this.browserService.isBrowserEnvironment()) {
      return ['document'];
    }

    const selectors = isMobileLayout
      ? ['#results-summary-panel', '#results-mistake-pattern-review-panel']
      : [
          '#results-summary-panel',
          '#results-highlighted-text-panels',
          '#results-mistake-pattern-review-panel',
        ];

    return selectors.filter(selector => !document.querySelector(selector));
  }

  private hasSeenAnonymousSampleTour(): boolean {
    return this.browserService.getItem(constants.anonymousSampleResultsTourStorageKey) === 'done';
  }

  private markAnonymousSampleTourSeen(): void {
    this.browserService.setItem(constants.anonymousSampleResultsTourStorageKey, 'done');
  }

  private trackSuppressed(reason: string, extraProperties: Record<string, string> = {}): void {
    this.exerciseSessionTracking.trackEvent('anonymous_results_tour_suppressed', {
      reason,
      ...extraProperties,
    });
  }

  private withTrackedButtons(
    steps: Array<{ id?: string; buttons?: Array<{ text?: string; action?: () => void }> } & Record<string, unknown>>
  ): Array<Record<string, unknown>> {
    return steps.map((step) => {
      if (!step.buttons?.length) {
        return step;
      }

      const stepId = step.id ?? 'unknown';
      return {
        ...step,
        buttons: step.buttons.map((button) => {
          const originalAction = button.action;
          return {
            ...button,
            action: () => {
              this.exerciseSessionTracking.trackEvent('shepherd_button_clicked', {
                tour_name: ResultsTourService.tourName,
                step_id: stepId,
                button_text: this.getButtonText(button.text),
              });
              originalAction?.();
            },
          };
        }),
      };
    });
  }

  private getButtonText(text: string | undefined): string {
    return (text ?? '').replace(/<[^>]*>/g, '').trim();
  }
}
