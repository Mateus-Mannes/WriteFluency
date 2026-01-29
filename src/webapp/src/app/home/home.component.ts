import { Component, ChangeDetectionStrategy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
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
    MatChipsModule,
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
      title: 'WriteFluency - Practice English Writing with Real News',
      description: 'Improve your English writing skills with WriteFluency. Listen to real news articles, type what you hear, and get instant feedback with highlighted corrections. Practice daily with beginner to advanced exercises.',
      keywords: 'English writing practice, listening comprehension, dictation exercises, English learning, ESL, language learning, writing skills, news articles',
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
