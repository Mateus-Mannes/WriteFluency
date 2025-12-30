import { Component, effect, input, signal } from '@angular/core';
import { NgClass, DecimalPipe } from '@angular/common';

@Component({
  selector: 'app-exercise-results-section',
  imports: [NgClass, DecimalPipe],
  templateUrl: './exercise-results-section.component.html',
  styleUrl: './exercise-results-section.component.scss',
})
export class ExerciseResultsSectionComponent {
  accuracy = input<number>(0.85);
  wordCount = input<number>(0);
  totalWords = input<number>(0);
  displayAccuracy = signal(0);

  constructor() {
    effect(() => {
      const target = this.accuracy();
      this.animateAccuracy(target);
    });
  }

  private animateAccuracy(target: number) {
    const duration = 2600; // ms
    const start = performance.now();
    const from = 0;

    const tick = (now: number) => {
      const progress = Math.min((now - start) / duration, 1);

      const eased = progress * (2 - progress); // easeOutQuad

      this.displayAccuracy.set(from + (target - from) * eased);

      if (progress < 1) {
        requestAnimationFrame(tick);
      }
    };

    requestAnimationFrame(tick);
  }

  get accuracyClass(): string {
    const percent = this.accuracy();
    if (percent >= 0.9) return 'result-good';
    if (percent > 0.5) return 'result-ok';
    return 'result-bad';
  }

  get wordCountClass(): string {
    const percent = this.wordCount() / (this.totalWords() || 1);
    if (percent >= 0.9) return 'result-good';
    if (percent > 0.5) return 'result-ok';
    return 'result-bad';
  }
}
