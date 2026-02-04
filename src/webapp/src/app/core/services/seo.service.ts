import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { DOCUMENT } from '@angular/common';
import { environment } from '../../../enviroments/enviroment';

export interface SEOConfig {
  title: string;
  description: string;
  image?: string;
  url?: string;
  keywords?: string;
  type?: 'website' | 'article';
  author?: string;
  publishedTime?: string;
  modifiedTime?: string;
}

@Injectable({
  providedIn: 'root'
})
export class SeoService {
  private meta = inject(Meta);
  private titleService = inject(Title);
  private router = inject(Router);
  private document = inject(DOCUMENT);

  private defaultConfig: SEOConfig = {
    title: 'English Writing Practice Online | WriteFluency',
    description: 'Practice English writing online with short daily exercises. Listen to real news audio, write what you hear, and improve your writing with instant feedback.',
    image: `${environment.production ? 'https://writefluency.com' : 'http://localhost:4200'}/assets/hero.gif`,
    keywords: 'english writing practice, english writing exercises online, practice english writing daily, improve english writing, listening and writing exercises',
    type: 'website'
  };

  updateMetaTags(config: Partial<SEOConfig>): void {
    const seoConfig: SEOConfig = { ...this.defaultConfig, ...config };
    const url = this.normalizeUrl(seoConfig.url || this.router.url);

    // Update title
    this.titleService.setTitle(seoConfig.title);

    // Basic meta tags
    this.meta.updateTag({ name: 'description', content: seoConfig.description });
    this.meta.updateTag({ name: 'keywords', content: seoConfig.keywords || this.defaultConfig.keywords! });
    this.meta.updateTag({ name: 'author', content: seoConfig.author || 'WriteFluency' });

    // Canonical URL
    this.updateCanonicalUrl(url);

    // Open Graph tags
    this.meta.updateTag({ property: 'og:title', content: seoConfig.title });
    this.meta.updateTag({ property: 'og:description', content: seoConfig.description });
    this.meta.updateTag({ property: 'og:type', content: seoConfig.type || 'website' });
    this.meta.updateTag({ property: 'og:url', content: url });
    this.meta.updateTag({ property: 'og:site_name', content: 'WriteFluency' });
    
    if (seoConfig.image) {
      this.meta.updateTag({ property: 'og:image', content: seoConfig.image });
      this.meta.updateTag({ property: 'og:image:alt', content: seoConfig.title });
      this.meta.updateTag({ property: 'og:image:width', content: '1200' });
      this.meta.updateTag({ property: 'og:image:height', content: '630' });
    }

    if (seoConfig.publishedTime) {
      this.meta.updateTag({ property: 'article:published_time', content: seoConfig.publishedTime });
    }

    if (seoConfig.modifiedTime) {
      this.meta.updateTag({ property: 'article:modified_time', content: seoConfig.modifiedTime });
    }

    // Twitter Card tags
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: seoConfig.title });
    this.meta.updateTag({ name: 'twitter:description', content: seoConfig.description });
    
    if (seoConfig.image) {
      this.meta.updateTag({ name: 'twitter:image', content: seoConfig.image });
      this.meta.updateTag({ name: 'twitter:image:alt', content: seoConfig.title });
    }
  }

  addStructuredData(data: any): void {
    // Structured data manipulation works with Angular Universal's DOM implementation
    let script: HTMLScriptElement | null = this.document.querySelector('script[type="application/ld+json"]');
    
    if (!script) {
      script = this.document.createElement('script');
      script.type = 'application/ld+json';
      this.document.head.appendChild(script);
    }

    script.textContent = JSON.stringify(data);
  }

  removeStructuredData(): void {
    const script = this.document.querySelector('script[type="application/ld+json"]');
    if (script) {
      script.remove();
    }
  }

  private updateCanonicalUrl(url: string): void {
    // Canonical URL manipulation works with Angular Universal's DOM implementation
    let link: HTMLLinkElement | null = this.document.querySelector('link[rel="canonical"]');
    
    if (!link) {
      link = this.document.createElement('link');
      link.setAttribute('rel', 'canonical');
      this.document.head.appendChild(link);
    }
    
    link.setAttribute('href', url);
  }

  private getAbsoluteUrl(path: string): string {
    const normalizedPath = path.startsWith('/') ? path : `/${path}`;
    return `${this.getSiteBaseUrl()}${normalizedPath}`;
  }

  private normalizeUrl(urlOrPath: string): string {
    try {
      return new URL(urlOrPath).toString();
    } catch {
      return this.getAbsoluteUrl(urlOrPath);
    }
  }

  private getSiteBaseUrl(): string {
    return environment.production
      ? 'https://writefluency.com'
      : 'http://localhost:4200';
  }

  // Helper method to generate exercise structured data
  generateExerciseStructuredData(exercise: {
    id: number;
    title: string;
    topic: string;
    level: string;
    duration: string;
    imageUrl?: string;
    description?: string;
  }): any {
    return {
      '@context': 'https://schema.org',
      '@type': 'LearningResource',
      '@id': this.getAbsoluteUrl(`/listen-and-write/${exercise.id}`),
      name: exercise.title,
      description: exercise.description || `Practice your English writing skills with this ${exercise.level.toLowerCase()} level exercise about ${exercise.topic}. Listen to the audio and type what you hear.`,
      educationalLevel: exercise.level,
      learningResourceType: 'Exercise',
      inLanguage: 'en-US',
      teaches: 'English writing and listening comprehension',
      timeRequired: exercise.duration,
      isAccessibleForFree: true,
      ...(exercise.imageUrl && {
        image: exercise.imageUrl
      }),
      provider: {
        '@type': 'Organization',
        name: 'WriteFluency',
        url: this.getAbsoluteUrl('/')
      }
    };
  }

  // Organization structured data for homepage
  generateOrganizationStructuredData(): any {
    return {
      '@context': 'https://schema.org',
      '@type': 'Organization',
      name: 'WriteFluency',
      url: this.getAbsoluteUrl('/'),
      logo: this.getAbsoluteUrl('/assets/app-icon.svg'),
      description: 'WriteFluency helps learners improve their English writing skills through interactive listening and writing exercises with real news content.',
      sameAs: [
        // Add your social media links here when available
        // 'https://twitter.com/writefluency',
        // 'https://facebook.com/writefluency',
      ]
    };
  }

  // WebSite structured data
  generateWebsiteStructuredData(): any {
    return {
      '@context': 'https://schema.org',
      '@type': 'WebSite',
      name: 'WriteFluency',
      url: this.getAbsoluteUrl('/')
    };
  }

  // Breadcrumb list for better navigation understanding
  generateBreadcrumbStructuredData(items: { name: string; url: string }[]): any {
    return {
      '@context': 'https://schema.org',
      '@type': 'BreadcrumbList',
      itemListElement: items.map((item, index) => ({
        '@type': 'ListItem',
        position: index + 1,
        name: item.name,
        item: this.getAbsoluteUrl(item.url)
      }))
    };
  }
}
