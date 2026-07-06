import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MistakePatternReviewComponent } from './mistake-pattern-review.component';
import { TextComparisonResult } from 'src/api/listen-and-write';

describe('MistakePatternReviewComponent', () => {
  let fixture: ComponentFixture<MistakePatternReviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MistakePatternReviewComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(MistakePatternReviewComponent);
  });

  it('renders the usage-limit alert instead of an empty review list', () => {
    const result: TextComparisonResult = {
      mistakePatternStatus: 'skipped_usage_limit',
      mistakePatternMessage: 'You reached today\'s Pro AI review limit. Your correction highlights are still available; only the AI mistake-pattern review is paused. You can use AI review again tomorrow. If this seems unexpected, contact us on the Support page.',
      comparisons: [
        {
          originalText: 'may be',
          userText: 'maybe',
          mistakePatternTags: null,
          mistakePatternPhrase: null,
        },
      ],
    };

    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('AI review limit reached');
    expect(element.textContent).toContain('You reached today\'s Pro AI review limit.');
    expect(element.textContent).toContain('correction highlights are still available');
    expect(element.querySelector('.mistake-pattern-alert-link')?.getAttribute('href')).toBe('/support');
    expect(element.querySelector('.mistake-pattern-list')).toBeNull();
  });

  it('renders generated mistake-pattern rows when annotations exist', () => {
    const result: TextComparisonResult = {
      mistakePatternStatus: 'generated',
      comparisons: [
        {
          sourceComparisonIndex: 0,
          originalText: 'may be',
          userText: 'maybe',
          mistakePatternTags: ['word_boundary'],
          mistakePatternPhrase: '"May be" is two words here; "maybe" changes it into an adverb.',
        },
      ],
    };

    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('Patterns in this attempt');
    expect(element.textContent).toContain('word boundary');
    expect(element.querySelector('.mistake-pattern-list')).not.toBeNull();
  });
});
