import { Component, ChangeDetectionStrategy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatCardModule } from '@angular/material/card';
import { PropositionsService } from '../../api/listen-and-write/api/propositions.service';
import { SubjectEnum } from '../../api/listen-and-write/model/subjectEnum';
import { ComplexityEnum } from '../../api/listen-and-write/model/complexityEnum';
import { ExerciseGridComponent, Exercise } from '../shared/exercise-grid/exercise-grid.component';
import { environment } from '../../enviroments/enviroment';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatCardModule,
    ExerciseGridComponent,
  ],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent implements OnInit {
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
      6, // pageSize
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

  // Fallback mock data in case API fails
  private mockExercises: Exercise[] = [
    {
      id: 1,
      title: 'Climate Change and Global Warming Impact',
      topic: SubjectEnum.Science,
      level: ComplexityEnum.Advanced,
      duration: '1 min',
      date: new Date('2026-01-15'),
    },
    {
      id: 2,
      title: 'World Cup Finals Preview',
      topic: SubjectEnum.Sports,
      level: ComplexityEnum.Intermediate,
      duration: '1 min',
      date: new Date('2026-01-14'),
    },
    {
      id: 3,
      title: 'Stock Market Updates',
      topic: SubjectEnum.Business,
      level: ComplexityEnum.Advanced,
      duration: '1 min',
      date: new Date('2026-01-13'),
    },
    {
      id: 4,
      title: 'Healthy Eating Habits',
      topic: SubjectEnum.Health,
      level: ComplexityEnum.Beginner,
      duration: '1 min',
      date: new Date('2026-01-12'),
    },
    {
      id: 5,
      title: 'Latest Movie Releases',
      topic: SubjectEnum.Entertainment,
      level: ComplexityEnum.Beginner,
      duration: '1 min',
      date: new Date('2026-01-11'),
    },
    {
      id: 6,
      title: 'AI Technology Breakthrough',
      topic: SubjectEnum.Tech,
      level: ComplexityEnum.Intermediate,
      duration: '1 min',
      date: new Date('2026-01-10'),
    },
    {
      id: 7,
      title: 'Election Results Analysis',
      topic: SubjectEnum.Politics,
      level: ComplexityEnum.Advanced,
      duration: '1 min',
      date: new Date('2026-01-09'),
    },
    {
      id: 8,
      title: 'Mediterranean Cuisine Secrets',
      topic: SubjectEnum.Food,
      level: ComplexityEnum.Intermediate,
      duration: '1 min',
      date: new Date('2026-01-08'),
    },
    {
      id: 9,
      title: 'Top Travel Destinations 2026',
      topic: SubjectEnum.Travel,
      level: ComplexityEnum.Beginner,
      duration: '1 min',
      date: new Date('2026-01-07'),
    },
    {
      id: 10,
      title: 'Cryptocurrency Market Trends',
      topic: SubjectEnum.Business,
      level: ComplexityEnum.Advanced,
      duration: '1 min',
      date: new Date('2026-01-06'),
    },
    {
      id: 11,
      title: 'Space Exploration Updates',
      topic: SubjectEnum.Science,
      level: ComplexityEnum.Intermediate,
      duration: '1 min',
      date: new Date('2026-01-05'),
    },
    {
      id: 12,
      title: 'Olympic Games Preparation',
      topic: SubjectEnum.Sports,
      level: ComplexityEnum.Beginner,
      duration: '1 min',
      date: new Date('2026-01-04'),
    },
  ];

  scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
