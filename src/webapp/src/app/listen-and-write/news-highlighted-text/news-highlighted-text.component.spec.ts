import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NewsHighlightedTextComponent } from './news-highlighted-text.component';

describe('NewsHighlightedTextComponent', () => {
  let component: NewsHighlightedTextComponent;
  let fixture: ComponentFixture<NewsHighlightedTextComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NewsHighlightedTextComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NewsHighlightedTextComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should mark the active comparison highlight', () => {
    fixture.componentRef.setInput('textType', 'user');
    fixture.componentRef.setInput('activeComparisonIndex', 0);
    fixture.componentRef.setInput('result', {
      originalText: 'They may be ready',
      userText: 'They maybe ready',
      comparisons: [
        {
          sourceComparisonIndex: 7,
          originalTextRange: { initialIndex: 5, finalIndex: 10 },
          originalText: 'may be',
          userTextRange: { initialIndex: 5, finalIndex: 9 },
          userText: 'maybe',
        },
      ],
    });

    const activePart = component.textParts().find(part => part.highlight);
    expect(activePart?.text).toContain('maybe');
    expect(component.isActivePart(activePart!)).toBeTrue();
  });
});
