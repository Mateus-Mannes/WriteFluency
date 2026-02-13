import { Component, ChangeDetectionStrategy, signal, effect, inject, OnInit, Input } from '@angular/core';
import { CommonModule, IMAGE_LOADER, NgOptimizedImage } from '@angular/common';
import { RouterLink, Router, ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatCardModule } from '@angular/material/card';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SubjectEnum } from '../../../api/listen-and-write/model/subjectEnum';
import { ComplexityEnum } from '../../../api/listen-and-write/model/complexityEnum';
import { PropositionsService } from '../../../api/listen-and-write/api/propositions.service';
import { minioVariantImageLoader } from '../image-loaders/minio-variant-image.loader';
import { ImagePlaceholderDirective } from '../directives/image-placeholder.directive';

export interface Exercise {
  id: number;
  title: string;
  topic: SubjectEnum;
  level: ComplexityEnum;
  duration: string;
  date: Date;
  imageFileId?: string | null;
  imageBaseId?: string;
  imageLoadFailed?: boolean;
  newsUrl?: string;
}

@Component({
  selector: 'app-exercise-grid',
  standalone: true,
  imports: [
    CommonModule,
    NgOptimizedImage,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatSelectModule,
    MatFormFieldModule,
    MatCardModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    FormsModule,
    ImagePlaceholderDirective,
  ],
  templateUrl: './exercise-grid.component.html',
  styleUrls: ['./exercise-grid.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: IMAGE_LOADER,
      useValue: minioVariantImageLoader,
    }
  ],
})
export class ExerciseGridComponent implements OnInit {
  private propositionsService = inject(PropositionsService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  // Input parameters for customization
  @Input() maxItems?: number; // Limit number of items (for home page preview)
  @Input() hideFilters = false; // Hide filter controls
  @Input() hidePagination = false; // Hide pagination controls
  
  private initialized = false;

  // Data signals
  exercises = signal<Exercise[]>([]);
  totalExercises = signal<number>(0);
  isLoading = signal<boolean>(false);
  error = signal<string | null>(null);
  
  // Filter and sort signals
  selectedTopic = signal<string>('all');
  selectedLevel = signal<string>('all');
  sortOrder = signal<'newest' | 'oldest'>('newest');
  
  // Pagination signals
  pageIndex = signal<number>(0);
  pageSize = signal<number>(18);
  
  // Available filter options
  topics = ['all', ...Object.values(SubjectEnum)];
  levels = ['all', ...Object.values(ComplexityEnum)];
  readonly imageLoaderParams = { defaultWidth: 640 };
  sortOptions = [
    { value: 'newest', label: 'Newest to Oldest' },
    { value: 'oldest', label: 'Oldest to Newest' }
  ];

  constructor() {
    // Effect to load exercises whenever filters, sort, or pagination changes
    effect(() => {
      // Skip initial run until initialized (after URL params are restored)
      if (!this.initialized) {
        return;
      }
      
      // Track all signals that should trigger a reload
      const topic = this.selectedTopic();
      const level = this.selectedLevel();
      const sort = this.sortOrder();
      const page = this.pageIndex();
      const size = this.pageSize();
      
      // Update URL params if not in preview mode (home page)
      if (!this.maxItems) {
        this.updateUrlParams();
      }
      
      // Load exercises with current parameters
      this.loadExercises();
    }, {  });
  }

  ngOnInit(): void {
    // If maxItems is set, override pageSize and hide pagination (home page mode)
    if (this.maxItems) {
      this.pageSize.set(this.maxItems);
      this.hidePagination = true;
    } else {
      // Restore state from URL params
      this.restoreFromUrlParams();
    }
    
    // Mark as initialized - this will trigger the effect to run
    this.initialized = true;
    
    // Manually trigger initial load
    this.loadExercises();
  }
  
  private loadExercises(): void {
    this.isLoading.set(true);
    this.error.set(null);

    const topic = this.selectedTopic() === 'all' ? undefined : this.selectedTopic();
    const level = this.selectedLevel() === 'all' ? undefined : this.selectedLevel();

    this.propositionsService.apiPropositionExercisesGet(
      topic as SubjectEnum | undefined,
      level as ComplexityEnum | undefined,
      this.pageIndex() + 1, // API expects 1-based page numbers
      this.pageSize(),
      this.sortOrder()
    ).subscribe({
      next: (response) => {
        const exercises: Exercise[] = (response.items || []).map(item => ({
          id: item.id!,
          title: item.title!,
          topic: item.topic!,
          level: item.level!,
          duration: this.formatDuration(item.audioDurationSeconds || 60),
          date: new Date(item.publishedOn!),
          imageFileId: item.imageFileId,
          imageBaseId: item.imageFileId
            ? this.getImageBaseId(item.imageFileId)
            : undefined,
          imageLoadFailed: false,
          newsUrl: item.newsUrl ?? ''
        }));
        
        this.exercises.set(exercises);
        this.totalExercises.set(response.totalCount || 0);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading exercises:', err);
        this.error.set('Failed to load exercises. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  private formatDuration(seconds: number): string {
    return `${seconds} sec`;
  }
  
  private restoreFromUrlParams(): void {
    const params = this.route.snapshot.queryParams;
    
    if (params['topic']) {
      this.selectedTopic.set(params['topic']);
    }
    
    if (params['level']) {
      this.selectedLevel.set(params['level']);
    }
    
    if (params['sort']) {
      this.sortOrder.set(params['sort'] as 'newest' | 'oldest');
    }
    
    if (params['page']) {
      const page = parseInt(params['page'], 10);
      if (!isNaN(page) && page >= 0) {
        this.pageIndex.set(page);
      }
    }
    
    if (params['pageSize']) {
      const size = parseInt(params['pageSize'], 10);
      if (!isNaN(size) && size > 0) {
        this.pageSize.set(size);
      }
    }
  }
  
  private updateUrlParams(): void {
    const queryParams: any = {};
    
    if (this.selectedTopic() !== 'all') {
      queryParams.topic = this.selectedTopic();
    }
    
    if (this.selectedLevel() !== 'all') {
      queryParams.level = this.selectedLevel();
    }
    
    if (this.sortOrder() !== 'newest') {
      queryParams.sort = this.sortOrder();
    }
    
    if (this.pageIndex() > 0) {
      queryParams.page = this.pageIndex();
    }
    
    if (this.pageSize() !== 18) {
      queryParams.pageSize = this.pageSize();
    }
    
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      replaceUrl: true
    });
  }
  
  onTopicChange(topic: string): void {
    this.selectedTopic.set(topic);
    this.pageIndex.set(0); // Reset to first page when filter changes
  }
  
  onLevelChange(level: string): void {
    this.selectedLevel.set(level);
    this.pageIndex.set(0); // Reset to first page when filter changes
  }
  
  onSortChange(order: 'newest' | 'oldest'): void {
    this.sortOrder.set(order);
    this.pageIndex.set(0); // Reset to first page when sort changes
  }
  
  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    // Update page size if changed
    if (event.pageSize !== this.pageSize()) {
      this.pageSize.set(event.pageSize);
    }
  }
  
  clearFilters(): void {
    this.selectedTopic.set('all');
    this.selectedLevel.set('all');
    this.sortOrder.set('newest');
    this.pageIndex.set(0);
    this.pageSize.set(18);
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

  onOptimizedImageError(exerciseId: number): void {
    this.exercises.update(items =>
      items.map(item =>
        item.id === exerciseId
          ? { ...item, imageLoadFailed: true }
          : item
      )
    );
  }

  private getImageBaseId(imageFileId: string): string {
    const lastDot = imageFileId.lastIndexOf('.');
    return lastDot > 0 ? imageFileId.slice(0, lastDot) : imageFileId;
  }

  getInitialPropositionState(exercise: Exercise) {
    return {
      id: exercise.id,
      title: exercise.title,
      subjectId: exercise.topic,
      complexityId: exercise.level,
      imageFileId: exercise.imageFileId,
      newsUrl: exercise.newsUrl,
      date: exercise.date
    };
  }
}
