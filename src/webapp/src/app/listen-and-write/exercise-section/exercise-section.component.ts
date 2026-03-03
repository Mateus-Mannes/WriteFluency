import { CommonModule } from '@angular/common';
import { Component, input, signal, ViewChild, ElementRef, output, computed, OnInit, HostListener } from '@angular/core';
import { SubmitTourService } from '../services/submit-tour.service';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Proposition } from 'src/api/listen-and-write';
import { BrowserService } from '../../core/services/browser.service';
import { ExerciseSessionTrackingService } from '../services/exercise-session-tracking.service';

@Component({
  selector: 'app-exercise-section',
  imports: [ CommonModule, MatIconModule, MatButtonModule ],
  templateUrl: './exercise-section.component.html',
  styleUrl: './exercise-section.component.scss',
})
export class ExerciseSectionComponent implements OnInit {

  @ViewChild('exerciseTextArea') textAreaRef!: ElementRef<HTMLTextAreaElement>;

  submitConfirmed = output<void>();

  textChanged = output<string>();

  saveExerciseState = output<void>();

  proposition = input<Proposition | null>();

  initialText = input<string | null>();
  initialAutoPause = input<number | null>();

  private readonly desktopAutoPauseOptions = [
    { label: 'Off', value: 0 },
    { label: '2s', value: 2 },
    { label: '3s', value: 3 },
    { label: '4s', value: 4 },
    { label: '5s', value: 5 }
  ];
  autoPauseOptions = [...this.desktopAutoPauseOptions];
  private isMobileMode = false;
  private lastDesktopAutoPause = 2;
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
    private browserService: BrowserService,
    private exerciseSessionTracking: ExerciseSessionTrackingService
  ) { }

  ngOnInit(): void {
    // Initialize text and auto-pause from inputs
    if (this.initialText()) {
      this.text.set(this.initialText()?.toString() || '');
    }

    const initialAutoPause = this.initialAutoPause();
    if (initialAutoPause !== null && initialAutoPause !== undefined) {
      const parsedAutoPause = Number(initialAutoPause);
      this.selectedAutoPause.set(parsedAutoPause);
      this.lastDesktopAutoPause = parsedAutoPause;
    }

    this.syncLayoutMode();
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.syncLayoutMode();
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
    const previousValue = this.selectedAutoPause();
    this.selectedAutoPause.set(value);
    if (!this.isMobileMode) {
      this.lastDesktopAutoPause = value;
    }
    if (previousValue !== value) {
      this.exerciseSessionTracking.trackEvent('exercise_auto_pause_changed', {
        source: 'exercise_section',
        previous_auto_pause_seconds: previousValue,
        auto_pause_seconds: value,
        auto_pause_enabled: value > 0
      });
    }
    this.saveState();
  }

  onTextChange(event: Event) {
    const value = (event.target as HTMLTextAreaElement).value;
    this.text.set(value);
    this.textChanged.emit(value);
    this.saveState();
  }

  saveState() {
    this.saveExerciseState.emit();
  }

  private isMobileLayout(): boolean {
    const width = this.browserService.getWindowWidth();
    return width > 0 && width <= 900;
  }

  private syncLayoutMode(): void {
    const shouldUseMobileMode = this.isMobileLayout();
    if (shouldUseMobileMode === this.isMobileMode) {
      return;
    }

    this.isMobileMode = shouldUseMobileMode;

    if (shouldUseMobileMode) {
      this.autoPauseOptions = [{ label: 'Off', value: 0 }];
      this.lastDesktopAutoPause = this.selectedAutoPause();
      if (this.selectedAutoPause() !== 0) {
        this.selectedAutoPause.set(0);
        this.saveState();
      }
      return;
    }

    this.autoPauseOptions = [...this.desktopAutoPauseOptions];
    if (this.selectedAutoPause() !== this.lastDesktopAutoPause) {
      this.selectedAutoPause.set(this.lastDesktopAutoPause);
      this.saveState();
    }
  }
}
