import { Component, ChangeDetectionStrategy, signal, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatCardModule } from '@angular/material/card';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { FormsModule } from '@angular/forms';
import { SubjectEnum } from '../../../api/listen-and-write/model/subjectEnum';
import { ComplexityEnum } from '../../../api/listen-and-write/model/complexityEnum';

export interface Exercise {
  id: number;
  title: string;
  topic: SubjectEnum;
  level: ComplexityEnum;
  duration: string;
  date: Date;
  imageUrl?: string;
}

@Component({
  selector: 'app-exercise-grid',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatSelectModule,
    MatFormFieldModule,
    MatCardModule,
    MatPaginatorModule,
    FormsModule,
  ],
  templateUrl: './exercise-grid.component.html',
  styleUrls: ['./exercise-grid.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExerciseGridComponent {
  // Inputs
  exercises = input.required<Exercise[]>();
  initialPageSize = input<number>(9);
  
  // Filter and sort signals
  selectedTopic = signal<string>('all');
  selectedLevel = signal<string>('all');
  sortOrder = signal<'newest' | 'oldest'>('newest');
  
  // Pagination signals
  pageIndex = signal<number>(0);
  pageSize = computed(() => this.initialPageSize());
  
  // Available filter options
  topics = ['all', ...Object.values(SubjectEnum)];
  levels = ['all', ...Object.values(ComplexityEnum)];
  sortOptions = [
    { value: 'newest', label: 'Newest to Oldest' },
    { value: 'oldest', label: 'Oldest to Newest' }
  ];
  
  // Computed filtered and sorted exercises
  filteredAndSortedExercises = computed(() => {
    let filtered = this.exercises().filter(exercise => {
      const topicMatch = this.selectedTopic() === 'all' || exercise.topic === this.selectedTopic();
      const levelMatch = this.selectedLevel() === 'all' || exercise.level === this.selectedLevel();
      return topicMatch && levelMatch;
    });
    
    // Sort by date
    filtered = [...filtered].sort((a, b) => {
      return this.sortOrder() === 'newest' 
        ? b.date.getTime() - a.date.getTime()
        : a.date.getTime() - b.date.getTime();
    });
    
    return filtered;
  });
  
  // Computed paginated exercises
  paginatedExercises = computed(() => {
    const startIndex = this.pageIndex() * this.pageSize();
    const endIndex = startIndex + this.pageSize();
    return this.filteredAndSortedExercises().slice(startIndex, endIndex);
  });
  
  // Total count for pagination
  get totalExercises(): number {
    return this.filteredAndSortedExercises().length;
  }
  
  onTopicChange(topic: string): void {
    this.selectedTopic.set(topic);
    this.pageIndex.set(0); // Reset to first page
  }
  
  onLevelChange(level: string): void {
    this.selectedLevel.set(level);
    this.pageIndex.set(0); // Reset to first page
  }
  
  onSortChange(order: 'newest' | 'oldest'): void {
    this.sortOrder.set(order);
    this.pageIndex.set(0); // Reset to first page
  }
  
  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
  }
  
  // Helper to get initials for placeholder images
  getTopicInitial(topic: string): string {
    return topic.charAt(0).toUpperCase();
  }
  
  // Helper to format topic names
  formatTopicName(topic: string): string {
    if (topic === 'all') return 'All Topics';
    return topic;
  }

  // Helper to format level names
  formatLevelName(level: string): string {
    if (level === 'all') return 'All Levels';
    return level;
  }
}
