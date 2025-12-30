import { Component, input } from '@angular/core';
import { NgClass, DecimalPipe } from '@angular/common';

@Component({
  selector: 'app-exercise-results-section',
  imports: [NgClass, DecimalPipe],
  templateUrl: './exercise-results-section.component.html',
  styleUrl: './exercise-results-section.component.scss',
})
export class ExerciseResultsSectionComponent {
  accuracy = input<number>(0);
  wordCount = input<number>(0);
  totalWords = input<number>(0);

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
