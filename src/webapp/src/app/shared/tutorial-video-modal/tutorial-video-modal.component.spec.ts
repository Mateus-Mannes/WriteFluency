import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TutorialVideoModalComponent } from './tutorial-video-modal.component';

describe('TutorialVideoModalComponent', () => {
  let component: TutorialVideoModalComponent;
  let fixture: ComponentFixture<TutorialVideoModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TutorialVideoModalComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TutorialVideoModalComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('embedUrl', 'https://www.youtube-nocookie.com/embed/video-id');
    fixture.componentRef.setInput('watchUrl', 'https://www.youtube.com/watch?v=video-id');
  });

  it('should render iframe only when open', () => {
    fixture.componentRef.setInput('isOpen', false);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('iframe')).toBeNull();

    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('iframe')).not.toBeNull();
  });

  it('should emit opened when modal becomes visible', () => {
    const openedSpy = jasmine.createSpy('openedSpy');
    component.opened.subscribe(openedSpy);

    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();

    expect(openedSpy).toHaveBeenCalledTimes(1);
  });

  it('should emit close when backdrop is clicked', () => {
    const closeSpy = jasmine.createSpy('closeSpy');
    component.closed.subscribe(closeSpy);

    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();
    const backdrop = fixture.nativeElement.querySelector('.tutorial-video-modal-backdrop');
    backdrop.click();

    expect(closeSpy).toHaveBeenCalled();
  });

  it('should emit close on escape key', () => {
    const closeSpy = jasmine.createSpy('closeSpy');
    component.closed.subscribe(closeSpy);

    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();
    component.onEscapeKeyPressed();

    expect(closeSpy).toHaveBeenCalled();
  });
});
