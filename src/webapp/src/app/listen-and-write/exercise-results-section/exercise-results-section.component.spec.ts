import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ExerciseResultsSectionComponent } from './exercise-results-section.component';

describe('ExerciseResultsSectionComponent', () => {
  let component: ExerciseResultsSectionComponent;
  let fixture: ComponentFixture<ExerciseResultsSectionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExerciseResultsSectionComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ExerciseResultsSectionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
