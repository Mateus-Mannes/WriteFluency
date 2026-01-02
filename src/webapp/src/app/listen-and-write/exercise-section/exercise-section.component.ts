import { CommonModule } from '@angular/common';
import { Component, input, signal, ViewChild, ElementRef, output } from '@angular/core';
import { SubmitTourService } from '../services/submit-tour.service';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-exercise-section',
  imports: [ CommonModule, MatIconModule, MatButtonModule ],
  templateUrl: './exercise-section.component.html',
  styleUrl: './exercise-section.component.scss',
})
export class ExerciseSectionComponent {

  @ViewChild('exerciseTextArea') textAreaRef!: ElementRef<HTMLTextAreaElement>;

  submitConfirmed = output<void>();

  saveExerciseState = output<void>();

  autoPauseOptions = [
    { label: '3s', value: 3 },
    { label: '5s', value: 5 },
    { label: '7s', value: 7 },
    { label: 'Off', value: 0 }
  ];
  selectedAutoPause = signal(5);

  maxWords = input(100);
  
  text = signal('');

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

  constructor(private submitTour: SubmitTourService) { }


  focusTextArea() {
    this.textAreaRef?.nativeElement.focus();
  }

  onSubmitClick() {
    this.submitTour.startTour(() => {
      this.submitConfirmed.emit();
    });
  }

  blurTextArea() {
    this.textAreaRef?.nativeElement.blur();
  }

  selectAutoPause(value: number) {
    this.selectedAutoPause.set(value);
    this.saveState();
  }

  onTextChange(event: Event) {
    const value = (event.target as HTMLTextAreaElement).value;
    this.text.set(value);
    this.saveState();
  }

  saveState() {
    this.saveExerciseState.emit();
  }
}
