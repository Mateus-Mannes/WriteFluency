import { Injectable } from '@angular/core';
import { offset } from '@floating-ui/dom';
import { ShepherdService } from 'angular-shepherd';

@Injectable({ providedIn: 'root' })

export class SubmitTourService {

  constructor(private shepherd: ShepherdService) { }

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

    this.shepherd.addSteps([
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
            action: () => { onSubmit(); this.shepherd.complete(); },
          },
        ],
      },
    ]);

    this.shepherd.start();
  }
}
