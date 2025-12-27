import { Component, signal, ViewChild, HostListener, effect } from '@angular/core';
import { NewsInfoComponent } from './news-info/news-info.component';
import { NewsImageComponent } from './news-image/news-image.component';
import { NewsAudioComponent } from './news-audio/news-audio.component';
import { ExerciseIntroSectionComponent } from './exercise-intro-section/exercise-intro-section.component';
import { ExerciseSectionComponent } from './exercise-section/exercise-section.component';
import { ExerciseResultsSectionComponent } from './exercise-results-section/exercise-results-section.component';
import { ListenFirstTourService } from './services/listen-first-tour.service';
import { ExerciseTourService } from './services/exercise-tour.service';

export type ExerciseState = 'intro' | 'exercise' | 'results';

@Component({
  selector: 'app-listen-and-write',
  imports: [ 
    NewsInfoComponent, 
    NewsImageComponent, 
    NewsAudioComponent, 
    ExerciseIntroSectionComponent, 
    ExerciseSectionComponent, 
    ExerciseResultsSectionComponent
  ],
  templateUrl: './listen-and-write.component.html',
  styleUrls: ['./listen-and-write.component.scss'],
})
export class ListenAndWriteComponent {
  @ViewChild(ExerciseSectionComponent) exerciseSectionComponent!: ExerciseSectionComponent;

  private autoPauseTimer: any = null;

  @HostListener('window:keydown', ['$event'])
  handleKeyboardEvent(event: KeyboardEvent) {
    if (this.exerciseState() !== 'exercise') return;
    // Play/Pause: Ctrl+Enter
    if (event.ctrlKey && event.key === 'Enter') {
      event.preventDefault();
      if (this.newsAudioComponent.isAudioPlaying()) {
        this.pauseAudioWithTimerClear();
      } else {
        this.playAudioWithAutoPause();
        // Finish the tutorial if active
        this.exerciseTourService.finishTour();
      }
    }
    // Rewind 3s: Ctrl+ArrowLeft
    if (event.code === 'ArrowLeft') {
      event.preventDefault();
      this.newsAudioComponent.rewindAudio(3);
    }
    // Forward 3s: Ctrl+ArrowRight
    if (event.code === 'ArrowRight') {
      event.preventDefault();
      this.newsAudioComponent.forwardAudio(3);
    }
  }

  playAudioWithAutoPause() {
    this.newsAudioComponent.playAudio();
    this.clearAutoPauseTimer();
    // Remove focus from textarea when audio starts
    this.exerciseSectionComponent?.blurTextArea();
    // Get auto-pause duration from exercise section
    const duration = this.exerciseSectionComponent?.selectedAutoPause?.() ?? 0;
    if (duration > 0) {
      this.autoPauseTimer = setTimeout(() => {
        if (this.newsAudioComponent.isAudioPlaying()) {
          this.newsAudioComponent.pauseAudio();
          // Switch UI to TYPING mode (exercise mode already active)
          // Optionally, you can trigger a method or signal here if needed
        }
        this.clearAutoPauseTimer();
        // Refocus textarea when audio is auto-paused
        this.exerciseSectionComponent?.focusTextArea();
      }, duration * 1000);
    }
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

  @ViewChild(NewsAudioComponent) newsAudioComponent!: NewsAudioComponent;

  exerciseState = signal<ExerciseState>('intro');

  constructor(
    private listenFirstTourService: ListenFirstTourService,
    private exerciseTourService: ExerciseTourService
  ) {

    effect(() => {
      const state = this.exerciseState();
      if (state === 'exercise') {
        this.startExerciseTour();
      }
    });
  }

  beginExercise() {
    this.newsAudioComponent.pauseAudio();
    this.clearAutoPauseTimer();

    if(this.newsAudioComponent.audioEnded) {
      this.exerciseState.set('exercise');
      return;
    }

    this.listenFirstTourService.prompt(
      '#newsAudio',
      () => this.newsAudioComponent.playAudio(),
      () => this.exerciseState.set('exercise')
    );
  }

  cancelTour() {
    this.listenFirstTourService.cancelTour();
  }

  startExerciseTour() {
    this.exerciseTourService.startTour();
  }
}
