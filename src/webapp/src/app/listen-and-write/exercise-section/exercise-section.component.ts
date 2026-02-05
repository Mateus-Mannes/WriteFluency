import { CommonModule } from '@angular/common';
import { Component, input, signal, ViewChild, ElementRef, output, computed, OnInit } from '@angular/core';
import { SubmitTourService } from '../services/submit-tour.service';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Proposition } from 'src/api/listen-and-write';
import { BrowserService } from '../../core/services/browser.service';

@Component({
  selector: 'app-exercise-section',
  imports: [ CommonModule, MatIconModule, MatButtonModule ],
  templateUrl: './exercise-section.component.html',
  styleUrl: './exercise-section.component.scss',
})
export class ExerciseSectionComponent implements OnInit {

  @ViewChild('exerciseTextArea') textAreaRef!: ElementRef<HTMLTextAreaElement>;

  submitConfirmed = output<void>();

  saveExerciseState = output<void>();

  proposition = input<Proposition | null>();

  initialText = input<string | null>();
  initialAutoPause = input<number | null>();

  autoPauseOptions = [
    { label: 'Off', value: 0 },
    { label: '2s', value: 2 },
    { label: '3s', value: 3 },
    { label: '4s', value: 4 },
    { label: '5s', value: 5 }
  ];
  selectedAutoPause = signal(2);
  shortcutSeekSeconds = computed(() => {
    const selected = this.selectedAutoPause();
    return selected > 0 ? selected : 3;
  });

  maxWords = computed(() => {
    return this.proposition()?.text?.trim().split(/\s+/).filter(Boolean).length || 0;
  });
  
  text = signal('');

  get wordCount(): number {
    // Count words, ignore multiple spaces, newlines, etc.
    return this.text().trim().split(/\s+/).filter(Boolean).length;
  }

  get wordCountClass(): string {
    const percent = this.wordCount / this.maxWords();
    if (percent > 1.5) return 'word-count-highlight';
    if (percent > 1.1) return 'word-count-primary';
    if (percent >= 0.9) return 'word-count-success';
    if (percent > 0.5) return 'word-count-primary';
    return 'word-count-highlight';
  }

  constructor(
    private submitTour: SubmitTourService,
    private browserService: BrowserService
  ) { }

  ngOnInit(): void {
    // Initialize text and auto-pause from inputs
    if (this.initialText()) {
      this.text.set(this.initialText()?.toString() || '');
    }
    if (this.isMobileLayout()) {
      this.autoPauseOptions = [{ label: 'Off', value: 0 }];
      this.selectedAutoPause.set(0);
    } else {
      const initialAutoPause = this.initialAutoPause();
      if (initialAutoPause !== null && initialAutoPause !== undefined) {
        this.selectedAutoPause.set(Number(initialAutoPause));
      }
    }
  }

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

  private isMobileLayout(): boolean {
    const width = this.browserService.getWindowWidth();
    return width > 0 && width <= 900;
  }
}
