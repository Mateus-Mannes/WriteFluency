import { Component, signal, ViewChild, HostListener, effect, AfterViewInit, computed } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { NewsInfoComponent } from './news-info/news-info.component';
import { NewsImageComponent } from './news-image/news-image.component';
import { NewsAudioComponent } from './news-audio/news-audio.component';
import { ExerciseIntroSectionComponent } from './exercise-intro-section/exercise-intro-section.component';
import { ExerciseSectionComponent } from './exercise-section/exercise-section.component';
import { ExerciseResultsSectionComponent } from './exercise-results-section/exercise-results-section.component';
import { ListenFirstTourService } from './services/listen-first-tour.service';
import { ExerciseTourService } from './services/exercise-tour.service';
import { NewsHighlightedTextComponent } from './news-highlighted-text/news-highlighted-text.component';
import { PropositionsService } from '../../api/listen-and-write/api/propositions.service';
import { Proposition } from '../../api/listen-and-write/model/proposition';
import { TextComparisonResult, TextComparisonsService } from 'src/api/listen-and-write';

export type ExerciseState = 'intro' | 'exercise' | 'results';

export const LISTEN_WRITE_FIRST_TIME_KEY = 'listen-write-first-time';

@Component({
  selector: 'app-listen-and-write',
  imports: [
    NewsInfoComponent,
    NewsImageComponent,
    NewsAudioComponent,
    ExerciseIntroSectionComponent,
    ExerciseSectionComponent,
    ExerciseResultsSectionComponent,
    NewsHighlightedTextComponent
  ],
  templateUrl: './listen-and-write.component.html',
  styleUrls: ['./listen-and-write.component.scss'],
})
export class ListenAndWriteComponent implements AfterViewInit {

  @ViewChild(ExerciseSectionComponent) exerciseSectionComponent!: ExerciseSectionComponent;

  @ViewChild(NewsAudioComponent) newsAudioComponent!: NewsAudioComponent;

  private autoPauseTimer: any = null;

  exerciseState = signal<ExerciseState>('intro');

  stateAnimOn = signal(false);

  isFirstTime = signal(true);

  proposition = signal<Proposition | null>(null);

  result = signal<TextComparisonResult | null>(null);

  userText = signal<string>('');

  exerciseId: number | null = null;

  constructor(
    private listenFirstTourService: ListenFirstTourService,
    private exerciseTourService: ExerciseTourService,
    private route: ActivatedRoute,
    private propositionsService: PropositionsService,
    private textComparisonsService: TextComparisonsService
  ) {
    // Get the exercise ID from route parameters
    this.route.params.subscribe(params => {
      const id = params['id'];
      if (id) {
        this.exerciseId = +id; // Convert to number
        // Restore state immediately before effect runs
        this.restoreState();
        this.loadProposition(this.exerciseId);
      }
    });

    // Check localStorage for first time flag
    const stored = localStorage.getItem(LISTEN_WRITE_FIRST_TIME_KEY);
    if (stored === 'false') {
      this.isFirstTime.set(false);
    } else {
      this.isFirstTime.set(true);
      localStorage.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'true');
    }

    effect(() => {
      const state = this.exerciseState();

      const stateKey = this.getStateKey();
      if (stateKey) {
        localStorage.setItem(stateKey, state);
      }

      if(state === 'exercise') {
        this.updateExerciseState();
      }

      this.stateAnimOn.set(false);
      queueMicrotask(() => this.stateAnimOn.set(true));

      if (state === 'exercise' && this.isFirstTime()) {
        this.startExerciseTour();
      }
    });
  }

  ngAfterViewInit(): void {
    this.updateExerciseState();
  }

  private getExerciseStateKey(): string | null {
    if (!this.exerciseId) return null;
    return `exercise-section-state-${this.exerciseId}`;
  }

  private getStateKey(): string | null {
    if (!this.exerciseId) return null;
    return `listen-write-state-${this.exerciseId}`;
  }

  loadProposition(id: number): void {
    this.propositionsService.apiPropositionIdGet(id).subscribe({
      next: (data) => {
        this.proposition.set(data);
      },
      error: (error) => {
        console.error('Error loading proposition:', error);
        // notify the user and back to the home page
        alert('Error loading exercise. Returning to home page.');
        window.location.href = '/';
      }
    });
  }

  private restoreState(): void {
    const stateKey = this.getStateKey();
    if (stateKey) {
      const storedState = localStorage.getItem(stateKey);
      if (storedState) {
        this.exerciseState.set(storedState as ExerciseState);
      }
    }
  }

  updateExerciseState() 
  {
    const stateKey = this.getExerciseStateKey();
    if (!stateKey) return;
    
    const state = localStorage.getItem(stateKey);
    if (state) {
      const parsed = JSON.parse(state);

      if (this.exerciseSectionComponent) {
        this.exerciseSectionComponent.text.set(parsed.userText || '');
        this.exerciseSectionComponent.selectedAutoPause.set(parsed.autoPause || 5);
      }

      if (this.newsAudioComponent && parsed.pausedTime) {
        this.newsAudioComponent.forwardAudio(parsed.pausedTime);
      }

      this.result.set(parsed.result || null);
    }
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyboardEvent(event: KeyboardEvent) {
    if (this.exerciseState() !== 'exercise') return;
    
    const modifierKey = event.ctrlKey || event.metaKey;
    
    // Play/Pause: Ctrl+Enter (Windows/Linux) or Cmd+Enter (Mac)
    if (modifierKey && event.key === 'Enter') {
      event.preventDefault();
      if (this.newsAudioComponent.isAudioPlaying()) {
        this.pauseAudioWithTimerClear();
      } else {
        this.playAudioWithAutoPause();
        // Finish the tutorial if active
        this.exerciseTourService.finishTour();
      }
    }
    // Rewind 3s: Ctrl+ArrowLeft (Windows/Linux) or Cmd+ArrowLeft (Mac)
    if (modifierKey && event.code === 'ArrowLeft') {
      event.preventDefault();
      this.newsAudioComponent.rewindAudio(3);
    }
    // Forward 3s: Ctrl+ArrowRight (Windows/Linux) or Cmd+ArrowRight (Mac)
    if (modifierKey && event.code === 'ArrowRight') {
      event.preventDefault();
      this.newsAudioComponent.forwardAudio(3);
    }
  }

  playAudioWithAutoPause() {
    this.newsAudioComponent.playAudio();
    this.applyAutoPause();
  }

  pauseAudioWithTimerClear() {
    this.newsAudioComponent.pauseAudio();
    this.clearAutoPauseTimer();
    // Refocus textarea when audio is manually paused
    this.exerciseSectionComponent?.focusTextArea();
  }

  clearAutoPauseTimer() {
    if (this.autoPauseTimer) {
      clearTimeout(this.autoPauseTimer);
      this.autoPauseTimer = null;
    }
  }

  beginExercise() {
    this.newsAudioComponent.pauseAudio();
    this.clearAutoPauseTimer();

    if (this.newsAudioComponent.audioEnded) {
      this.exerciseState.set('exercise');
      return;
    }

    if (this.isFirstTime()) {
      this.listenFirstTourService.prompt(
        '#newsAudio',
        () => this.newsAudioComponent.playAudio(),
        () => {
          this.exerciseState.set('exercise');
        }
      );
    } else {
      this.exerciseState.set('exercise');
    }
  }

  onAudioPlayClicked() {
    this.cancelTour();
    
    // Apply auto-pause logic if in exercise state
    if (this.exerciseState() === 'exercise') {
      this.applyAutoPause();
    }
  }

  cancelTour() {
    this.listenFirstTourService.cancelTour();
  }

  startExerciseTour() {
    this.exerciseTourService.startTour();
  }

  applyAutoPause() {
    this.clearAutoPauseTimer();
    // Remove focus from textarea when audio starts
    this.exerciseSectionComponent?.blurTextArea();
    // Get auto-pause duration from exercise section
    const duration = this.exerciseSectionComponent?.selectedAutoPause?.() ?? 0;
    if (duration > 0) {
      this.autoPauseTimer = setTimeout(() => {
        if (this.newsAudioComponent.isAudioPlaying()) {
          this.newsAudioComponent.pauseAudio();
        }
        this.clearAutoPauseTimer();
        // Refocus textarea when audio is auto-paused
        this.exerciseSectionComponent?.focusTextArea();
      }, duration * 1000);
    }
  }

  onExerciseSubmit() {
    this.newsAudioComponent.pauseAudio();

    this.textComparisonsService.apiTextComparisonCompareTextsPost({
      originalText: this.proposition()!.text,
      userText: this.exerciseSectionComponent.text()
    }).subscribe({
      next: (result: TextComparisonResult) => {
        this.userText.set(this.exerciseSectionComponent.text());
        this.result.set(result);
        this.onSaveExerciseState();
        this.exerciseState.set('results');
      },
      error: (error) => {
        alert('Error processing your exercise. Please try again.');
      }
    });
  }

  onTryAgain() {
    const stateKey = this.getExerciseStateKey();
    if (stateKey) {
      localStorage.removeItem(stateKey);
    }
    this.newsAudioComponent.audioRef.nativeElement.currentTime = 0;
    this.isFirstTime.set(false);
    this.exerciseState.set('intro');
  }

  onFindAnotherExercise() {
    // Redirect to home page
    window.location.href = '/exercises';
  }

  onAudioPaused() {
    this.onSaveExerciseState();
  }

  onSaveExerciseState() {
    const stateKey = this.getExerciseStateKey();
    if (!stateKey) return;
    
    const state = {
      userText: this.exerciseSectionComponent.text(),
      autoPause: this.exerciseSectionComponent.selectedAutoPause(),
      pausedTime: this.newsAudioComponent.audioRef?.nativeElement.currentTime || 0,
      result: this.result()
    };

    localStorage.setItem(stateKey, JSON.stringify(state));
  }
}
