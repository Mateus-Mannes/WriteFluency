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
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
