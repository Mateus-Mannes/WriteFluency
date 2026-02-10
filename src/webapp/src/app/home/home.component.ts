import { Component, ChangeDetectionStrategy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { ExerciseGridComponent } from '../shared/exercise-grid/exercise-grid.component';
import { BrowserService } from '../core/services/browser.service';
import { SeoService } from '../core/services/seo.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    ExerciseGridComponent,
  ],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent implements OnInit {
  private seoService = inject(SeoService);
  
  constructor(private browserService: BrowserService) {}

  ngOnInit(): void {
    // Set SEO meta tags for homepage
    this.seoService.updateMetaTags({
      title: 'English Writing Practice Online | Daily Exercises | WriteFluency',
      description: 'Practice English writing online with short daily exercises. Listen to real news audio, write what you hear, and improve your English writing with instant feedback.',
      keywords: 'english writing practice, english writing exercises online, practice english writing daily, improve english writing, listening and writing exercises',
      type: 'website',
      url: '/'
    });

    // Add multiple structured data types for homepage
    const structuredData = {
      '@context': 'https://schema.org',
      '@graph': [
        this.seoService.generateOrganizationStructuredData(),
        this.seoService.generateWebsiteStructuredData()
      ]
    };
    this.seoService.addStructuredData(structuredData);

  }

  scrollToTop(): void {
    this.browserService.scrollToTop();
  }

}
