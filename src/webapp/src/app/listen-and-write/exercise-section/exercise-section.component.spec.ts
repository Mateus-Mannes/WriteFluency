import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatTooltip } from '@angular/material/tooltip';
import { By } from '@angular/platform-browser';

import { ExerciseSectionComponent } from './exercise-section.component';

describe('ExerciseSectionComponent', () => {
  let component: ExerciseSectionComponent;
  let fixture: ComponentFixture<ExerciseSectionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExerciseSectionComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ExerciseSectionComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should restore auto-pause off state from initial input', () => {
    fixture.componentRef.setInput('initialAutoPause', 0);
    fixture.detectChanges();

    expect(component.selectedAutoPause()).toBe(0);
  });

  it('should derive forward and rewind seconds from auto-pause when enabled', () => {
    fixture.detectChanges();

    component.selectAutoPause(5);

    expect(component.forwardSeekSeconds()).toBe(5);
    expect(component.rewindSeekSeconds()).toBe(6);
  });

  it('should use the fallback plus one for rewind when auto-pause is off', () => {
    fixture.detectChanges();

    component.selectAutoPause(0);

    expect(component.forwardSeekSeconds()).toBe(2);
    expect(component.rewindSeekSeconds()).toBe(3);
  });

  it('should emit tutorialVideoRequested when help icon is clicked', () => {
    fixture.detectChanges();
    const emitSpy = spyOn(component.tutorialVideoRequested, 'emit');
    const helpButton = fixture.nativeElement.querySelector('#exercise-tutorial-video-help');

    helpButton.click();

    expect(emitSpy).toHaveBeenCalled();
  });

  it('should render tutorial help icon with expected tooltip message', () => {
    fixture.detectChanges();

    const helpButton = fixture.nativeElement.querySelector('#exercise-tutorial-video-help');
    const tooltipDirective = fixture.debugElement.query(By.directive(MatTooltip)).injector.get(MatTooltip);

    expect(helpButton).toBeTruthy();
    expect(tooltipDirective.message).toBe('In doubt? Watch the quick tutorial video');
  });

  it('should render user and original word counts', () => {
    fixture.componentRef.setInput('proposition', { originalWordCount: 131 });
    component.text.set('one two three');
    fixture.detectChanges();

    const wordCounter = fixture.nativeElement.querySelector('#exercise-word-count');

    expect(wordCounter.textContent).toContain('Word Count:');
    expect(wordCounter.textContent).toContain('3/131');
  });

  [
    { words: 50, expectedClass: 'word-count-highlight' },
    { words: 51, expectedClass: 'word-count-primary' },
    { words: 90, expectedClass: 'word-count-success' },
    { words: 111, expectedClass: 'word-count-primary' },
    { words: 151, expectedClass: 'word-count-highlight' },
  ].forEach(({ words, expectedClass }) => {
    it(`should use ${expectedClass} at ${words} percent of the original word count`, () => {
      fixture.componentRef.setInput('proposition', { originalWordCount: 100 });
      component.text.set(Array.from({ length: words }, (_, index) => `word${index}`).join(' '));
      fixture.detectChanges();

      const count: HTMLElement = fixture.nativeElement.querySelector('#exercise-word-count b');

      expect(count.classList.contains(expectedClass)).toBeTrue();
    });
  });

});
