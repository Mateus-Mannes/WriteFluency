import { Injectable } from '@angular/core';
import { offset } from '@floating-ui/dom';
import { ShepherdService } from 'angular-shepherd';
import { BrowserService } from '../../core/services/browser.service';
import { LISTEN_WRITE_FIRST_TIME_KEY } from '../listen-and-write.component';

@Injectable({ providedIn: 'root' })

export class ExerciseTourService {

  constructor(
    private shepherd: ShepherdService,
    private browserService: BrowserService
  ) { }

  finishTour() {
    if (this.shepherd.isActive) {
      this.shepherd.complete();
    }
  }

  cancelTour() {
    if (this.shepherd.isActive) {
      this.shepherd.cancel();
    }
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
      // Prevent Shepherd from using arrow keys so it doesn't clash with audio shortcuts.
      this.shepherd.keyboardNavigation = false;

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
          id: 'audio-player',
          title: 'Audio Player Controls',
          text: 'This is your audio player! Click the three dots menu to adjust <b>playback speed</b> (try 0.75x if the audio is too fast). You can also drag the progress bar to jump to any point in the recording.',
          attachTo: { element: '#newsAudio', on: 'bottom' },
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

      // Ensure localStorage is updated when tour ends (must be after addSteps)
      this.shepherd.tourObject?.on('complete', () => {
        this.browserService.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
      });
      this.shepherd.tourObject?.on('cancel', () => {
        this.browserService.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
      });

      this.shepherd.start();
    }, 50);
  }

  /**
   * Mobile-friendly guided tour with fewer steps and touch-first guidance.
   */
  startMobileTour() {
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
      this.shepherd.keyboardNavigation = false;

      this.shepherd.addSteps([
        {
          id: 'intro-tutorial-mobile',
          title: 'Mobile Preview',
          text: 'This mobile version is a preview. Features like auto‑pause and keyboard shortcuts are limited on mobile, so the experience is best on desktop with a keyboard.',
          attachTo: undefined,
          buttons: [
            {
              text: 'Start',
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
          id: 'audio-mobile',
          title: 'Audio Player',
          text: 'Tap play/pause while you type. You can also tap the three dots to adjust <b>playback speed</b> (try 0.75x for slower audio). Drag the progress bar to jump to any part of the recording.',
          attachTo: { element: '#newsAudio', on: 'bottom' },
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
          id: 'auto-pause-mobile',
          title: 'Desktop Recommended',
          text: 'We recommend using a desktop for the full experience. Mobile is mainly for a quick look at the app.',
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
          id: 'submit-mobile',
          title: 'Submit',
          text: 'When you are done, tap Submit to get feedback.',
          attachTo: { element: '#exercise-submit', on: 'top' },
          floatingUIOptions: { middleware: [offset(16)] },
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

      this.shepherd.tourObject?.on('complete', () => {
        this.browserService.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
      });
      this.shepherd.tourObject?.on('cancel', () => {
        this.browserService.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
      });

      this.shepherd.start();
    }, 50);
  }
}
