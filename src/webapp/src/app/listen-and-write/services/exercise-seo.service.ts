import { Injectable } from '@angular/core';
import { Proposition } from 'src/api/listen-and-write';
import { environment } from 'src/enviroments/enviroment';
import { SeoService } from '../../core/services/seo.service';

@Injectable()
export class ExerciseSeoService {
  constructor(private readonly seoService: SeoService) {}

  applyExerciseSeo(exerciseId: number, proposition: Proposition): void {
    const complexityDesc = proposition.complexityId || 'Intermediate';
    const subjectDesc = proposition.subjectId || 'News';
    const duration = proposition.audioDurationSeconds
      ? `${Math.ceil(proposition.audioDurationSeconds / 60)} min`
      : '1-2 min';
    const exerciseImageUrl = this.getExerciseImageUrl(proposition.imageFileId);

    this.seoService.updateMetaTags({
      title: `${proposition.title} - ${complexityDesc} Level Exercise | WriteFluency`,
      description: `Practice your English writing with this ${complexityDesc.toLowerCase()} level listening exercise about ${subjectDesc}. Listen to real news audio and improve your dictation skills. Duration: ${duration}.`,
      keywords: `${subjectDesc}, ${complexityDesc} level, English writing exercise, listening comprehension, dictation practice`,
      type: 'article',
      url: `/english-writing-exercise/${exerciseId}`,
      image: exerciseImageUrl,
      publishedTime: proposition.publishedOn || undefined,
    });

    const exerciseData = this.seoService.generateExerciseStructuredData({
      id: exerciseId,
      title: proposition.title || 'English Writing Exercise',
      topic: subjectDesc,
      level: complexityDesc,
      duration,
      imageUrl: exerciseImageUrl,
      description: `Practice your English writing with this ${complexityDesc.toLowerCase()} level listening exercise.`,
    });

    const breadcrumbData = this.seoService.generateBreadcrumbStructuredData([
      { name: 'Home', url: '/' },
      { name: 'Exercises', url: '/exercises' },
      { name: proposition.title || 'Exercise', url: `/english-writing-exercise/${exerciseId}` },
    ]);

    this.seoService.addStructuredData({
      '@context': 'https://schema.org',
      '@graph': [exerciseData, breadcrumbData],
    });
  }

  private getExerciseImageUrl(imageFileId?: string | null): string | undefined {
    if (!imageFileId) {
      return undefined;
    }

    return `${environment.minioUrl}/images/${imageFileId}`;
  }
}
