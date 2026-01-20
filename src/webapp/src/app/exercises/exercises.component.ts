import { Component, ChangeDetectionStrategy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PropositionsService } from '../../api/listen-and-write/api/propositions.service';
import { ExerciseGridComponent, Exercise } from '../shared/exercise-grid/exercise-grid.component';
import { environment } from '../../enviroments/enviroment';

@Component({
  selector: 'app-exercises',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    ExerciseGridComponent,
  ],
  templateUrl: './exercises.component.html',
  styleUrls: ['./exercises.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExercisesComponent implements OnInit {
  exercises = signal<Exercise[]>([]);
  isLoading = signal(false);
  error = signal<string | null>(null);

  constructor(private propositionsService: PropositionsService) {}

  ngOnInit(): void {
    this.loadExercises();
  }

  loadExercises(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.propositionsService.apiPropositionExercisesGet(
      undefined, // topic
      undefined, // level
      1, // pageNumber
      9, // pageSize
      'newest' // sortBy
    ).subscribe({
      next: (response) => {
        const exercises: Exercise[] = (response.items || []).map(item => ({
          id: item.id!,
          title: item.title!,
          topic: item.topic!,
          level: item.level!,
          duration: this.formatDuration(item.audioDurationSeconds || 60),
          date: new Date(item.publishedOn!),
          imageUrl: item.imageFileId
            ? `${environment.minioUrl}/images/${item.imageFileId}`
            : undefined
        }));
        this.exercises.set(exercises);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading exercises:', err);
        this.error.set('Failed to load exercises');
        this.isLoading.set(false);
      }
    });
  }

  formatDuration(seconds: number): string {
    const minutes = Math.floor(seconds / 60);
    return `${minutes} min`;
  }
}
