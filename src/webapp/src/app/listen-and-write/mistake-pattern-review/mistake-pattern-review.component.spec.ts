import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MistakePatternReviewComponent } from './mistake-pattern-review.component';
import { TextComparisonResult } from 'src/api/listen-and-write';

describe('MistakePatternReviewComponent', () => {
  let fixture: ComponentFixture<MistakePatternReviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MistakePatternReviewComponent],
      providers: [provideRouter([])],
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

  it('renders a no-corrections state for generated perfect attempts without annotations', () => {
    const result: TextComparisonResult = {
      mistakePatternStatus: 'generated',
      accuracyPercentage: 1,
      comparisons: [],
    };

    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('No correction patterns');
    expect(element.textContent).toContain('Perfect match');
    expect(element.textContent).toContain('no mistake patterns to review');
    expect(element.querySelector('.mistake-pattern-review-empty')).not.toBeNull();
  });

  it('renders a no-corrections state for not-applicable perfect attempts without comparisons', () => {
    const result: TextComparisonResult = {
      mistakePatternStatus: 'not_applicable',
      accuracyPercentage: 1,
      comparisons: [],
    };

    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('No correction patterns');
    expect(element.textContent).toContain('Perfect match');
  });

  it('renders a no-user-text state for not-applicable blank attempts', () => {
    const result: TextComparisonResult = {
      mistakePatternStatus: 'not_applicable',
      accuracyPercentage: 0,
      userText: '',
      comparisons: [],
    };

    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('No text to review');
    expect(element.textContent).toContain('No answer submitted');
    expect(element.textContent).toContain('No comparisons were evaluated because there was no user text.');
  });

  it('renders a no-user-text state for restored blank attempts without review status', () => {
    const result: TextComparisonResult = {
      accuracyPercentage: 0,
      userText: '',
      comparisons: [],
    };

    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('No text to review');
    expect(element.textContent).toContain('No answer submitted');
  });

  it('truncates long comparison snippets in the row header', () => {
    const longOriginalText = 'The North Wales Crusaders rugby team faced a tough year. They suffered the biggest defeat in their history, losing a game by one hundred thirty-four points.';
    const result: TextComparisonResult = {
      mistakePatternStatus: 'generated',
      comparisons: [
        {
          sourceComparisonIndex: 0,
          originalText: longOriginalText,
          userText: 'tes fadg sdf d',
          mistakePatternTags: ['phrase_heard_incorrectly'],
          mistakePatternPhrase: 'Your answer does not match any part of the original sentence, so focus on catching real English words and phrases you hear.',
        },
      ],
    };

    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    const originalSnippet = element.querySelector('.mistake-pattern-original-text') as HTMLElement;
    const list = element.querySelector('.mistake-pattern-list') as HTMLElement;
    const row = element.querySelector('.mistake-pattern-row') as HTMLElement;
    const rowTags = element.querySelector('.mistake-pattern-row-tags') as HTMLElement;
    const renderedSnippet = originalSnippet.textContent?.trim() ?? '';
    expect(renderedSnippet).toContain('The North Wales Crusaders rugby team');
    expect(renderedSnippet.endsWith('…')).toBeTrue();
    expect(renderedSnippet.length).toBeLessThanOrEqual(84);
    expect(renderedSnippet.length).toBeLessThan(longOriginalText.length);
    expect(originalSnippet.getAttribute('title')).toBe(longOriginalText);
    expect(getComputedStyle(list).alignContent).toBe('start');
    expect(getComputedStyle(row).alignSelf).toBe('start');
    expect(getComputedStyle(rowTags).alignSelf).toBe('start');
    expect(element.textContent).toContain('Your answer does not match any part of the original sentence');
  });

  it('renders a locked login teaser and emits login CTA clicks', () => {
    const result: TextComparisonResult = {
      mistakePatternStatus: 'login_required_to_unlock_review',
      comparisons: [
        {
          originalText: 'may be',
          userText: 'maybe',
        },
      ],
    };
    const loginSpy = spyOn(fixture.componentInstance.loginToUnlockClick, 'emit');

    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('Log in to unlock your free Pro review');
    expect(element.textContent).toContain('Everyday');
    const button = element.querySelector('.mistake-pattern-lock-button') as HTMLButtonElement;
    button.click();
    expect(loginSpy).toHaveBeenCalled();
    expect(element.querySelector('.mistake-pattern-locked-preview')).not.toBeNull();
  });

  it('renders a locked upgrade teaser and emits upgrade CTA clicks', () => {
    const result: TextComparisonResult = {
      mistakePatternStatus: 'upgrade_required_to_unlock_review',
      comparisons: [
        {
          originalText: 'may be',
          userText: 'maybe',
        },
      ],
    };
    const upgradeSpy = spyOn(fixture.componentInstance.upgradeToProClick, 'emit');

    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('Upgrade to review every attempt');
    const button = element.querySelector('.mistake-pattern-lock-button') as HTMLButtonElement;
    button.click();
    expect(upgradeSpy).toHaveBeenCalled();
  });
});
