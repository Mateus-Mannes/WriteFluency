import { Injectable } from '@angular/core';
import { ShepherdService } from 'angular-shepherd';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';

@Injectable({ providedIn: 'root' })

export class SubmitTourService {

  constructor(
    private shepherd: ShepherdService,
    private exerciseSessionTracking: ExerciseSessionTrackingService
  ) { }

  startTour(onSubmit: () => void) {
    if (this.shepherd.isActive) {
      this.shepherd.cancel();
    }

    this.shepherd.defaultStepOptions = {
      scrollTo: true,
      cancelIcon: { enabled: true },
      classes: 'wf-tour',
      modalOverlayOpeningPadding: 10,
      modalOverlayOpeningRadius: 10,
    };
    this.shepherd.modal = true;
    this.shepherd.keyboardNavigation = false;

    const steps = [
      {
        id: 'submit',
        title: 'Ready to Submit?',
        text: 'Please review your answer before submitting. Once you submit, you won\'t be able to make changes.',
        attachTo: undefined, // Center of screen
        buttons: [
          {
            text: 'Cancel',
            classes: 'wf-primary',
            action: () => this.shepherd.cancel(),
          },
          {
            text: 'Submit',
            classes: 'wf-secondary',
            action: () => {
              this.exerciseSessionTracking.trackEvent('submit_tour_choice', {
                choice: 'submit'
              });
              this.shepherd.complete();
              onSubmit();
            },
          },
        ],
      },
    ];

    this.shepherd.addSteps(this.withTrackedButtons('submit-tour', steps));
    this.exerciseSessionTracking.trackEvent('shepherd_tour_opened', {
      tour_name: 'submit-tour'
    });

    this.shepherd.tourObject?.on('complete', () => {
      this.exerciseSessionTracking.trackEvent('shepherd_tour_finished', {
        tour_name: 'submit-tour',
        outcome: 'complete'
      });
    });

    this.shepherd.tourObject?.on('cancel', () => {
      this.exerciseSessionTracking.trackEvent('shepherd_tour_finished', {
        tour_name: 'submit-tour',
        outcome: 'cancel'
      });
    });

    this.shepherd.start();
  }

  startRecommendationTour(message: string, onSubmitAnyway: () => void) {
    if (this.shepherd.isActive) {
      this.shepherd.cancel();
    }

    this.shepherd.defaultStepOptions = {
      scrollTo: true,
      cancelIcon: { enabled: true },
      classes: 'wf-tour',
      modalOverlayOpeningPadding: 10,
      modalOverlayOpeningRadius: 10,
    };
    this.shepherd.modal = true;
    this.shepherd.keyboardNavigation = false;

    const steps = [
      {
        id: 'submit-recommendation',
        title: 'Before Submitting',
        text: message.replace(/\n/g, '<br>'),
        attachTo: undefined,
        buttons: [
          {
            text: 'Back to Exercise',
            classes: 'wf-primary',
            action: () => this.shepherd.cancel(),
          },
          {
            text: 'Submit Anyway',
            classes: 'wf-secondary',
            action: () => {
              this.exerciseSessionTracking.trackEvent('submit_recommendation_choice', {
                choice: 'submit_anyway'
              });
              this.shepherd.complete();
              onSubmitAnyway();
            },
          },
        ],
      },
    ];

    this.shepherd.addSteps(this.withTrackedButtons('submit-recommendation-tour', steps));
    this.exerciseSessionTracking.trackEvent('shepherd_tour_opened', {
      tour_name: 'submit-recommendation-tour'
    });

    this.shepherd.tourObject?.on('complete', () => {
      this.exerciseSessionTracking.trackEvent('shepherd_tour_finished', {
        tour_name: 'submit-recommendation-tour',
        outcome: 'complete'
      });
    });

    this.shepherd.tourObject?.on('cancel', () => {
      this.exerciseSessionTracking.trackEvent('shepherd_tour_finished', {
        tour_name: 'submit-recommendation-tour',
        outcome: 'cancel'
      });
    });

    this.shepherd.start();
  }

  private withTrackedButtons(
    tourName: string,
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
                tour_name: tourName,
                step_id: stepId,
                button_text: this.getButtonText(button.text)
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
