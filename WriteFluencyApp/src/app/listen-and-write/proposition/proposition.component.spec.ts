import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PropositionComponent } from './proposition.component';

describe('PropositionComponent', () => {
  let component: PropositionComponent;
  let fixture: ComponentFixture<PropositionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ PropositionComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PropositionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
