import { Component, input, output } from '@angular/core';
import {MatButtonModule} from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-exercise-intro-section',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './exercise-intro-section.component.html',
  styleUrl: './exercise-intro-section.component.scss',
})
export class ExerciseIntroSectionComponent {

  isLoading = input.required<boolean>();
  loadingTooltip = input<string>('Finishing exercise loading...');
  beginExerciseClick = output<void>();

  beginExercise() {
    if (this.isLoading()) {
      return;
    }

    this.beginExerciseClick.emit();
  }

}
