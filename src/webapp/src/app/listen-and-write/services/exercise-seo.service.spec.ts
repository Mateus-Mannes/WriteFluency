import { TestBed } from '@angular/core/testing';
import { Proposition } from 'src/api/listen-and-write';
import { environment } from 'src/enviroments/enviroment';
import { SeoService } from '../../core/services/seo.service';
import { ExerciseSeoService } from './exercise-seo.service';

describe('ExerciseSeoService', () => {
  let service: ExerciseSeoService;
  let seoServiceMock: jasmine.SpyObj<SeoService>;

  beforeEach(() => {
    seoServiceMock = jasmine.createSpyObj<SeoService>(
      'SeoService',
      [
        'updateMetaTags',
        'generateExerciseStructuredData',
        'generateBreadcrumbStructuredData',
        'addStructuredData',
      ],
    );
    seoServiceMock.generateExerciseStructuredData.and.returnValue({ '@type': 'LearningResource' });
    seoServiceMock.generateBreadcrumbStructuredData.and.returnValue({ '@type': 'BreadcrumbList' });

    TestBed.configureTestingModule({
      providers: [
        ExerciseSeoService,
        {
          provide: SeoService,
          useValue: seoServiceMock,
        },
      ],
    });

    service = TestBed.inject(ExerciseSeoService);
  });

  it('should apply exercise meta tags and structured data', () => {
    const proposition = {
      title: 'Romania Faces Political Shake-Up',
      complexityId: 'Advanced',
      subjectId: 'Business',
      audioDurationSeconds: 121,
      imageFileId: 'exercise-image.webp',
      publishedOn: '2026-06-08T00:00:00Z',
    } as Proposition;

    service.applyExerciseSeo(42, proposition);

    const expectedImageUrl = `${environment.minioUrl}/images/exercise-image.webp`;
    expect(seoServiceMock.updateMetaTags).toHaveBeenCalledWith({
      title: 'Romania Faces Political Shake-Up - Advanced Level Exercise | WriteFluency',
      description: 'Practice your English writing with this advanced level listening exercise about Business. Listen to real news audio and improve your dictation skills. Duration: 3 min.',
      keywords: 'Business, Advanced level, English writing exercise, listening comprehension, dictation practice',
      type: 'article',
      url: '/english-writing-exercise/42',
      image: expectedImageUrl,
      publishedTime: '2026-06-08T00:00:00Z',
    });
    expect(seoServiceMock.generateExerciseStructuredData).toHaveBeenCalledWith({
      id: 42,
      title: 'Romania Faces Political Shake-Up',
      topic: 'Business',
      level: 'Advanced',
      duration: '3 min',
      imageUrl: expectedImageUrl,
      description: 'Practice your English writing with this advanced level listening exercise.',
    });
    expect(seoServiceMock.generateBreadcrumbStructuredData).toHaveBeenCalledWith([
      { name: 'Home', url: '/' },
      { name: 'Exercises', url: '/exercises' },
      { name: 'Romania Faces Political Shake-Up', url: '/english-writing-exercise/42' },
    ]);
    expect(seoServiceMock.addStructuredData).toHaveBeenCalledWith({
      '@context': 'https://schema.org',
      '@graph': [
        { '@type': 'LearningResource' },
        { '@type': 'BreadcrumbList' },
      ],
    });
  });

  it('should use existing fallback values when optional proposition fields are missing', () => {
    service.applyExerciseSeo(7, {
      title: '',
      complexityId: null,
      subjectId: null,
      audioDurationSeconds: null,
      imageFileId: null,
      publishedOn: null,
    } as unknown as Proposition);

    expect(seoServiceMock.updateMetaTags).toHaveBeenCalledWith(jasmine.objectContaining({
      title: ' - Intermediate Level Exercise | WriteFluency',
      image: undefined,
      publishedTime: undefined,
    }));
    expect(seoServiceMock.generateExerciseStructuredData).toHaveBeenCalledWith(jasmine.objectContaining({
      id: 7,
      title: 'English Writing Exercise',
      topic: 'News',
      level: 'Intermediate',
      duration: '1-2 min',
      imageUrl: undefined,
      description: 'Practice your English writing with this intermediate level listening exercise.',
    }));
    expect(seoServiceMock.generateBreadcrumbStructuredData).toHaveBeenCalledWith(jasmine.arrayContaining([
      { name: 'Exercise', url: '/english-writing-exercise/7' },
    ]));
  });
});
