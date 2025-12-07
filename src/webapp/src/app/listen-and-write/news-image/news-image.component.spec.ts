import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NewsImageComponent } from './news-image.component';

describe('NewsImageComponent', () => {
  let component: NewsImageComponent;
  let fixture: ComponentFixture<NewsImageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NewsImageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NewsImageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
