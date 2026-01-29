import { Injectable } from '@angular/core';
import { offset } from '@floating-ui/dom';
import { ShepherdService } from 'angular-shepherd';

@Injectable({ providedIn: 'root' })
export class ListenFirstTourService {
  constructor(private shepherd: ShepherdService) { }

  cancelTour() {
    this.shepherd.cancel();
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

    this.shepherd.addSteps([
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
            action: () => { this.shepherd.complete(); onListen(); },
          },
          {
            text: 'No, start now',
            classes: 'wf-secondary',
            action: () => { this.shepherd.complete(); onSkip(); },
          },
        ],

      },
    ]);

    this.shepherd.start();
  }
}
