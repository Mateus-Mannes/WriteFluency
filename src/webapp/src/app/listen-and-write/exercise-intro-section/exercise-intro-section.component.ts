import { Component, output } from '@angular/core';
import {MatButtonModule} from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-exercise-intro-section',
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './exercise-intro-section.component.html',
  styleUrl: './exercise-intro-section.component.scss',
})
export class ExerciseIntroSectionComponent {

  beginExerciseClick = output<void>();

  beginExercise() {
    this.beginExerciseClick.emit();
  }

}
