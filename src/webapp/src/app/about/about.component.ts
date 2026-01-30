import { Component, ChangeDetectionStrategy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { SeoService } from '../core/services/seo.service';

@Component({
  selector: 'app-about',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    RouterLink,
  ],
  templateUrl: './about.component.html',
  styleUrls: ['./about.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AboutComponent implements OnInit {
  private seoService = inject(SeoService);

  readonly creatorName = 'Mateus Mannes de Medeiros';
  readonly contactEmail = 'services@mateusmedeiros.dev';
  readonly linkedInUrl = 'https://www.linkedin.com/in/mateus-mannes-de-medeiros';
  readonly linkedInLabel = 'linkedin.com/in/mateus-mannes-de-medeiros';

  ngOnInit(): void {
    this.seoService.updateMetaTags({
      title: 'About WriteFluency',
      description: 'Learn about the WriteFluency project and how to get in touch for support, suggestions, or bug reports.',
      keywords: 'WriteFluency about, contact, support, feedback, creator',
      type: 'website',
      url: '/about'
    });

    const breadcrumbData = this.seoService.generateBreadcrumbStructuredData([
      { name: 'Home', url: '/' },
      { name: 'About', url: '/about' }
    ]);
    this.seoService.addStructuredData(breadcrumbData);
  }
}
