import { ComponentFixture, TestBed } from '@angular/core/testing';

import { UnsupportedScreenComponent } from './unsupported-screen.component';

describe('UnsupportedScreenComponent', () => {
  let component: UnsupportedScreenComponent;
  let fixture: ComponentFixture<UnsupportedScreenComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UnsupportedScreenComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(UnsupportedScreenComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
