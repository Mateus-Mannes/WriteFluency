import { Injectable } from '@angular/core';
import { offset } from '@floating-ui/dom';
import { ShepherdService } from 'angular-shepherd';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';

@Injectable({ providedIn: 'root' })
export class ListenFirstTourService {
  constructor(
    private shepherd: ShepherdService,
    private exerciseSessionTracking: ExerciseSessionTrackingService
  ) { }

  cancelTour() {
    if (this.shepherd.isActive) {
      this.shepherd.cancel();
    }
  }

  prompt(anchorSelector: string, onListen: () => void, onSkip: () => void) {
    // If user clicks multiple times, avoid stacking
    if (this.shepherd.isActive) {
      this.shepherd.cancel();
    }

    this.shepherd.defaultStepOptions = {
      scrollTo: true,
      cancelIcon: { enabled: true },
    };

    this.shepherd.modal = true;
    this.shepherd.keyboardNavigation = false;

    const steps = [
      {
        id: 'listen-first',
        title: 'Before you start',
        text: 'Do you want to listen to the full audio before starting the exercise?',
        classes: 'wf-tour',
        attachTo: { element: anchorSelector, on: 'top' },
        floatingUIOptions: {
          middleware: [
            offset(30),
          ],
        },
        modalOverlayOpeningPadding: 15,
        modalOverlayOpeningRadius: 16,
        buttons: [
          {
            text: 'Yes, play audio',
            classes: 'wf-primary',
            action: () => {
              this.exerciseSessionTracking.trackEvent('listen_first_choice', {
                choice: 'listen_audio'
              });
              this.shepherd.complete();
              onListen();
            },
          },
          {
            text: 'No, start now',
            classes: 'wf-secondary',
            action: () => {
              this.exerciseSessionTracking.trackEvent('listen_first_choice', {
                choice: 'start_without_listening'
              });
              this.shepherd.complete();
              onSkip();
            },
          },
        ],

      },
    ];

    this.shepherd.addSteps(this.withTrackedButtons('listen-first-tour', steps));
    this.exerciseSessionTracking.trackEvent('shepherd_tour_opened', {
      tour_name: 'listen-first-tour'
    });

    this.shepherd.tourObject?.on('complete', () => {
      this.exerciseSessionTracking.trackEvent('shepherd_tour_finished', {
        tour_name: 'listen-first-tour',
        outcome: 'complete'
      });
    });

    this.shepherd.tourObject?.on('cancel', () => {
      this.exerciseSessionTracking.trackEvent('shepherd_tour_finished', {
        tour_name: 'listen-first-tour',
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
