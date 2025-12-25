import { CommonModule } from '@angular/common';
import { Component, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-exercise-section',
  imports: [ CommonModule, MatIconModule, MatButtonModule ],
  templateUrl: './exercise-section.component.html',
  styleUrl: './exercise-section.component.scss',
})
export class ExerciseSectionComponent {
  autoPauseOptions = [
    { label: '3s', value: 3 },
    { label: '5s', value: 5 },
    { label: '7s', value: 7 },
    { label: 'Off', value: 0 }
  ];
  selectedAutoPause = signal(3);

  maxWords = input(100);
  text = signal('');

  selectAutoPause(value: number) {
    this.selectedAutoPause.set(value);
  }

  onTextChange(event: Event) {
    const value = (event.target as HTMLTextAreaElement).value;
    this.text.set(value);
  }

  get wordCount(): number {
    // Count words, ignore multiple spaces, newlines, etc.
    return this.text().trim().split(/\s+/).filter(Boolean).length;
  }

  get wordCountClass(): string {
    const percent = this.wordCount / this.maxWords();
    if (percent >= 0.9) return 'word-count-success';
    if (percent > 0.5) return 'word-count-primary';
    return 'word-count-highlight';
  }
}
