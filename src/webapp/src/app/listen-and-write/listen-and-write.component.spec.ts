import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

import { ListenAndWriteComponent } from './listen-and-write.component';

describe('ListenAndWriteComponent', () => {
  let component: ListenAndWriteComponent;
  let fixture: ComponentFixture<ListenAndWriteComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ListenAndWriteComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            params: of({}),
            queryParams: of({})
          }
        }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ListenAndWriteComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should use auto-pause value for rewind shortcut when enabled', () => {
    const rewindAudio = jasmine.createSpy('rewindAudio');

    component.exerciseState.set('exercise');
    component.exerciseSectionComponent = {
      selectedAutoPause: () => 5
    } as any;
    component.newsAudioComponent = {
      rewindAudio,
      forwardAudio: jasmine.createSpy('forwardAudio'),
      isAudioPlaying: () => false
    } as any;

    const event = {
      ctrlKey: true,
      metaKey: false,
      key: 'ArrowLeft',
      code: 'ArrowLeft',
      preventDefault: jasmine.createSpy('preventDefault')
    } as unknown as KeyboardEvent;

    component.handleKeyboardEvent(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(rewindAudio).toHaveBeenCalledWith(5);
  });

  it('should use 3 seconds for forward shortcut when auto-pause is off', () => {
    const forwardAudio = jasmine.createSpy('forwardAudio');

    component.exerciseState.set('exercise');
    component.exerciseSectionComponent = {
      selectedAutoPause: () => 0
    } as any;
    component.newsAudioComponent = {
      rewindAudio: jasmine.createSpy('rewindAudio'),
      forwardAudio,
      isAudioPlaying: () => false
    } as any;

    const event = {
      ctrlKey: true,
      metaKey: false,
      key: 'ArrowRight',
      code: 'ArrowRight',
      preventDefault: jasmine.createSpy('preventDefault')
    } as unknown as KeyboardEvent;

    component.handleKeyboardEvent(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(forwardAudio).toHaveBeenCalledWith(3);
  });
});
