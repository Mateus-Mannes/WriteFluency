import { Component } from '@angular/core';
import { NewsInfoComponent } from './news-info/news-info.component';
import { NewsImageComponent } from './news-image/news-image.component';
import { NewsAudioComponent } from './news-audio/news-audio.component';
import { ExerciseIntroSectionComponent } from './exercise-intro-section/exercise-intro-section.component';

@Component({
  selector: 'app-listen-and-write',
  imports: [ NewsInfoComponent, NewsImageComponent, NewsAudioComponent, ExerciseIntroSectionComponent ],
  templateUrl: './listen-and-write.component.html',
  styleUrl: './listen-and-write.component.scss',
})
export class ListenAndWriteComponent {

}
