import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { MatTooltip } from '@angular/material/tooltip';

import { ExerciseIntroSectionComponent } from './exercise-intro-section.component';

describe('ExerciseIntroSectionComponent', () => {
  let component: ExerciseIntroSectionComponent;
  let fixture: ComponentFixture<ExerciseIntroSectionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExerciseIntroSectionComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ExerciseIntroSectionComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('isLoading', false);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit begin click when not loading', () => {
    spyOn(component.beginExerciseClick, 'emit');

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    button.click();

    expect(component.beginExerciseClick.emit).toHaveBeenCalled();
  });

  it('should not emit begin click when loading', () => {
    spyOn(component.beginExerciseClick, 'emit');
    fixture.componentRef.setInput('isLoading', true);
    fixture.detectChanges();

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    button.click();

    expect(component.beginExerciseClick.emit).not.toHaveBeenCalled();
  });

  it('should disable button and add loading class when loading', () => {
    fixture.componentRef.setInput('isLoading', true);
    fixture.detectChanges();

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(button.disabled).toBeTrue();
    expect(button.classList.contains('cta-button-loading')).toBeTrue();
  });

  it('should enable tooltip only when loading', () => {
    fixture.componentRef.setInput('isLoading', false);
    fixture.detectChanges();

    let tooltip = fixture.debugElement.query(By.directive(MatTooltip)).injector.get(MatTooltip);
    expect(tooltip.disabled).toBeTrue();

    fixture.componentRef.setInput('isLoading', true);
    fixture.componentRef.setInput('loadingTooltip', 'Exercise is finishing loading.');
    fixture.detectChanges();

    tooltip = fixture.debugElement.query(By.directive(MatTooltip)).injector.get(MatTooltip);
    expect(tooltip.disabled).toBeFalse();
    expect(tooltip.message).toBe('Exercise is finishing loading.');
  });
});
