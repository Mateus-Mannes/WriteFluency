import { Injectable } from '@angular/core';
import { offset } from '@floating-ui/dom';
import { ShepherdService } from 'angular-shepherd';

const LISTEN_WRITE_FIRST_TIME_KEY = 'listen-write-first-time';

@Injectable({ providedIn: 'root' })

export class ExerciseTourService {

  constructor(private shepherd: ShepherdService) { }

  finishTour() {
    if (this.shepherd) {
      this.shepherd.complete();
    }
  }

  cancelTour() {
    this.shepherd.cancel();
  }

  /**
   * Starts the guided exercise tour using hardcoded selectors for each step.
   */
  startTour() {
    setTimeout(() => {
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

      // Ensure localStorage is updated when tour ends
      this.shepherd.tourObject?.on('complete', () => {
        localStorage.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
      });
      this.shepherd.tourObject?.on('cancel', () => {
        localStorage.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
      });

      this.shepherd.addSteps([
        {
          id: 'intro-tutorial',
          title: 'Interactive Tutorial',
          text: 'Welcome! This is an interactive tutorial to help you get started with the exercise. You can follow the steps, interact with the UI, and skip at any time.',
          attachTo: undefined, // Center of screen
          buttons: [
            {
              text: 'Start',
              classes: 'wf-primary',
              action: () => this.shepherd.next(),
            },
            {
              text: 'Skip Tutorial',
              classes: 'wf-secondary',
              action: () => this.shepherd.cancel(),
            },
          ],
        },
        {
          id: 'shortcuts',
          title: 'Keyboard Shortcuts',
          text: 'Use keyboard shortcuts to control audio without leaving the text area.',
          attachTo: { element: '#exercise-shortcuts', on: 'top' },
          floatingUIOptions: { middleware: [offset(16)] },
          buttons: [
            {
              text: 'Next',
              classes: 'wf-primary',
              action: () => this.shepherd.next(),
            },
            {
              text: 'Skip',
              classes: 'wf-secondary',
              action: () => this.shepherd.cancel(),
            },
          ],
        },
        {
          id: 'auto-pause',
          title: 'Audio Auto-Pause',
          text: 'Audio will auto-pause every X seconds so you can type. You can change this here.',
          attachTo: { element: '#exercise-auto-pause', on: 'bottom' },
          floatingUIOptions: { middleware: [offset(16)] },
          buttons: [
            {
              text: 'Next',
              classes: 'wf-primary',
              action: () => this.shepherd.next(),
            },
            {
              text: 'Back',
              classes: 'wf-secondary',
              action: () => this.shepherd.back(),
            },
            {
              text: 'Skip',
              classes: 'wf-secondary',
              action: () => this.shepherd.cancel(),
            },
          ],
        },
        {
          id: 'word-counter',
          title: 'Word Counter',
          text: 'This is just a helper. You don’t need to match the exact word count.',
          attachTo: { element: '#exercise-word-count', on: 'bottom' },
          floatingUIOptions: { middleware: [offset(16)] },
          buttons: [
            {
              text: 'Next',
              classes: 'wf-primary',
              action: () => this.shepherd.next(),
            },
            {
              text: 'Back',
              classes: 'wf-secondary',
              action: () => this.shepherd.back(),
            },
            {
              text: 'Skip',
              classes: 'wf-secondary',
              action: () => this.shepherd.cancel(),
            },
          ],
        },
        {
          id: 'submit',
          title: 'Submit Your Answer',
          text: 'Done? Click Submit. It’s okay if it’s not perfect — this is for learning.',
          attachTo: { element: '#exercise-submit', on: 'top' },
          floatingUIOptions: { middleware: [offset(16)] },
          buttons: [
            {
              text: 'Next',
              classes: 'wf-primary',
              action: () => this.shepherd.next(),
            },
            {
              text: 'Back',
              classes: 'wf-secondary',
              action: () => this.shepherd.back(),
            },
            {
              text: 'Skip',
              classes: 'wf-secondary',
              action: () => this.shepherd.cancel(),
            },
          ],
        },
        {
          id: 'final',
          title: 'Ready to Start!',
          text: 'You’re ready. Press <b>Ctrl/⌘ + Enter</b> to start your first listening.',
          attachTo: undefined, // Center of screen
          buttons: [
            {
              text: 'Finish',
              classes: 'wf-primary',
              action: () => this.shepherd.complete(),
            },
            {
              text: 'Back',
              classes: 'wf-secondary',
              action: () => this.shepherd.back(),
            },
          ],
        },
      ]);

      this.shepherd.start();
    }, 50);
  }
}
