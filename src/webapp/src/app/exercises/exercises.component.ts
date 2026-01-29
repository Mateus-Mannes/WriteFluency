import { Component, ChangeDetectionStrategy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ExerciseGridComponent } from '../shared/exercise-grid/exercise-grid.component';
import { SeoService } from '../core/services/seo.service';

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
  private seoService = inject(SeoService);

  ngOnInit(): void {
    // Set SEO meta tags for exercises page
    this.seoService.updateMetaTags({
      title: 'Browse All English Writing Exercises | WriteFluency',
      description: 'Browse our complete collection of English writing exercises. Filter by topic, difficulty level, and more. Practice listening and writing with real news articles from beginner to advanced levels.',
      keywords: 'English exercises, writing practice, listening exercises, ESL practice, language learning exercises, dictation practice',
      type: 'website',
      url: '/exercises'
    });

    // Add breadcrumb structured data
    const breadcrumbData = this.seoService.generateBreadcrumbStructuredData([
      { name: 'Home', url: '/' },
      { name: 'Exercises', url: '/exercises' }
    ]);
    this.seoService.addStructuredData(breadcrumbData);
  }
}
