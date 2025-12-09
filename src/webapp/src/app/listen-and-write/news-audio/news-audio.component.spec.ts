import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NewsAudioComponent } from './news-audio.component';

describe('NewsAudioComponent', () => {
  let component: NewsAudioComponent;
  let fixture: ComponentFixture<NewsAudioComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NewsAudioComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NewsAudioComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
