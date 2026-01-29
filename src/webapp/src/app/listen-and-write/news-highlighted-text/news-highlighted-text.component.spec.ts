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
});
