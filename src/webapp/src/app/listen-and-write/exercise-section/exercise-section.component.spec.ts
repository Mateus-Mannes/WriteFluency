import { ComponentFixture, TestBed } from '@angular/core/testing';

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

  it('should match shortcut seek seconds to auto-pause when enabled', () => {
    fixture.detectChanges();

    component.selectAutoPause(5);

    expect(component.shortcutSeekSeconds()).toBe(5);
  });

  it('should keep shortcut seek seconds at 3 when auto-pause is off', () => {
    fixture.detectChanges();

    component.selectAutoPause(0);

    expect(component.shortcutSeekSeconds()).toBe(3);
  });
});
