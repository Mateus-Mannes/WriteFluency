import { Component, signal, ViewChild } from '@angular/core';
import { NewsInfoComponent } from './news-info/news-info.component';
import { NewsImageComponent } from './news-image/news-image.component';
import { NewsAudioComponent } from './news-audio/news-audio.component';
import { ExerciseIntroSectionComponent } from './exercise-intro-section/exercise-intro-section.component';
import { ExerciseSectionComponent } from './exercise-section/exercise-section.component';
import { ExerciseResultsSectionComponent } from './exercise-results-section/exercise-results-section.component';
import { ListenFirstTourService } from './services/listen-first-tour.service';

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

  @ViewChild(NewsAudioComponent) newsAudioComponent!: NewsAudioComponent;

  exerciseState = signal<ExerciseState>('intro');

  constructor(private listenFirstTourService: ListenFirstTourService) {
  }

  beginExercise() {
    this.newsAudioComponent.pauseAudio();

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
}
