import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

import { HomeComponent } from './home.component';
import { Insights } from '../../telemetry/insights.service';

describe('HomeComponent', () => {
  let component: HomeComponent;
  let fixture: ComponentFixture<HomeComponent>;
  let insightsMock: jasmine.SpyObj<Insights>;

  beforeEach(async () => {
    insightsMock = jasmine.createSpyObj<Insights>('Insights', ['trackEvent']);

    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            params: of({}),
            queryParams: of({})
          }
        },
        {
          provide: Insights,
          useValue: insightsMock
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(HomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render tutorial video iframe on home page', () => {
    const iframe = fixture.nativeElement.querySelector('#home-tutorial-video-frame');

    expect(iframe).toBeTruthy();
  });

  it('should render start first exercise call-to-action in tutorial panel', () => {
    const cta = fixture.nativeElement.querySelector('#home-start-first-exercise');

    expect(cta).toBeTruthy();
    expect(cta.textContent).toContain('Start first exercise');
  });

  it('should track tutorial_video_opened once when iframe loads', () => {
    component.onTutorialVideoFrameLoaded();
    component.onTutorialVideoFrameLoaded();

    expect(insightsMock.trackEvent).toHaveBeenCalledWith('tutorial_video_opened', { source: 'home' });
    expect(insightsMock.trackEvent).toHaveBeenCalledTimes(1);
  });
});
