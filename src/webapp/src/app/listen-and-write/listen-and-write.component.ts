import { Component, signal, ViewChild, HostListener, effect } from '@angular/core';
import { NewsInfoComponent } from './news-info/news-info.component';
import { NewsImageComponent } from './news-image/news-image.component';
import { NewsAudioComponent } from './news-audio/news-audio.component';
import { ExerciseIntroSectionComponent } from './exercise-intro-section/exercise-intro-section.component';
import { ExerciseSectionComponent } from './exercise-section/exercise-section.component';
import { ExerciseResultsSectionComponent } from './exercise-results-section/exercise-results-section.component';
import { ListenFirstTourService } from './services/listen-first-tour.service';
import { ExerciseTourService } from './services/exercise-tour.service';
import { NewsHighlightedTextComponent } from './news-highlighted-text/news-highlighted-text.component';
import { TextComparision } from './entities/text-comparision';

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
export class ListenAndWriteComponent {

  @ViewChild(ExerciseSectionComponent) exerciseSectionComponent!: ExerciseSectionComponent;

  @ViewChild(NewsAudioComponent) newsAudioComponent!: NewsAudioComponent;

  private autoPauseTimer: any = null;

  exerciseState = signal<ExerciseState>('intro');

  stateAnimOn = signal(false);

  isFirstTime = signal(true);

  constructor(
    private listenFirstTourService: ListenFirstTourService,
    private exerciseTourService: ExerciseTourService
  ) {

    // Check localStorage for first time flag
    const stored = localStorage.getItem(LISTEN_WRITE_FIRST_TIME_KEY);
    if (stored === 'false') {
      this.isFirstTime.set(false);
    } else {
      this.isFirstTime.set(true);
      localStorage.setItem(LISTEN_WRITE_FIRST_TIME_KEY, 'false');
    }

    effect(() => {
      const state = this.exerciseState();

      this.stateAnimOn.set(false);
      queueMicrotask(() => this.stateAnimOn.set(true));

      if (state === 'exercise' && this.isFirstTime()) {
        this.startExerciseTour();
      }
    });
  }

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
        () => this.exerciseState.set('exercise')
      );
    } else {
      this.exerciseState.set('exercise');
    }
  }

  cancelTour() {
    this.listenFirstTourService.cancelTour();
  }

  startExerciseTour() {
    this.exerciseTourService.startTour();
  }

  onExerciseSubmit() {
    this.exerciseState.set('results');
  }

  onTryAgain() {
    this.isFirstTime.set(false);
    this.exerciseState.set('exercise');
  }

  onFindAnotherExercise() {
    // Redirect to home page
    window.location.href = '/';
  }

  originalText: string = 'A large number of people, about ninety thousand, gathered in Sydney to support Palestine. They crossed the Sydney Harbour Bridge with flags and protest signs. Many had umbrellas because it was rainy and windy. The police stopped the march for safety reasons because the crowd was very big. The organizer said the event was more than they expected and it was a very peaceful and hopeful day. At the same time, a smaller group of three thousand people gathered in Melbourne to raise awareness about the situation in Gaza. Later on, the police asked the Sydney crowd to go back to the city because they were worried about too many people and possible injuries. Despite the challenges, the protests showed that many people care about peace and helping others in need.'
  userText: string = 'A large number of people, about ninety thousand, gathered in Sydney to support Palestine. They crossed the Sydney Harbour Bridge with flags and protest signs. Many had umbrellas because it was rainy and windy. The police stoppd the march for safety reasons because the crowd was very. The organizer said the event was more than they expected and it was a very peaceful and hopeful day. At the same time, a smaller group of threethousand people gathered in Melbourne to raise awareness about the situation in Gaza. Later on, the police asked the Sydney crowd to go back to he citybecause they were worried about too many people and possibl. Despite th, the protests showed that many people care about peac helping others in need.\n'
  differences : TextComparision[] = [
    {
      originalTextRange: {
        initialIndex: 221,
        finalIndex: 227,
      },
      originalText: 'stopped',
      userTextRange: {
        initialIndex: 221,
        finalIndex: 226,
      },
      userText: 'stoppd',
    },
    {
      originalTextRange: {
        initialIndex: 280,
        finalIndex: 292,
      },
      originalText: 'very big. The',
      userTextRange: {
        initialIndex: 279,
        finalIndex: 287,
      },
      userText: 'very. The',
    },
    {
      originalTextRange: {
        initialIndex: 425,
        finalIndex: 448,
      },
      originalText: 'of three thousand people',
      userTextRange: {
        initialIndex: 420,
        finalIndex: 442,
      },
      userText: 'of threethousand people',
    },
    {
      originalTextRange: {
        initialIndex: 578,
        finalIndex: 593,
      },
      originalText: 'the city because',
      userTextRange: {
        initialIndex: 572,
        finalIndex: 585,
      },
      userText: 'he citybecause',
    },
    {
      originalTextRange: {
        initialIndex: 639,
        finalIndex: 684,
      },
      originalText: 'possible injuries. Despite the challenges, the',
      userTextRange: {
        initialIndex: 631,
        finalIndex: 654,
      },
      userText: 'possibl. Despite th, the',
    },
    {
      originalTextRange: {
        initialIndex: 730,
        finalIndex: 746,
      },
      originalText: 'peace and helping',
      userTextRange: {
        initialIndex: 700,
        finalIndex: 711,
      },
      userText: 'peac helping',
    },
    {
      originalTextRange: {
        initialIndex: 758,
        finalIndex: 761,
      },
      originalText: 'need',
      userTextRange: {
        initialIndex: 723,
        finalIndex: 728,
      },
      userText: 'need.\n',
    },
  ];
}
