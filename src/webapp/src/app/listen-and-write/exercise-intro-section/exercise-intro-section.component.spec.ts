import { ComponentFixture, TestBed } from '@angular/core/testing';

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
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
