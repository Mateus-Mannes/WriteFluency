import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ListenAndWriteComponent } from './listen-and-write.component';

describe('ListenAndWriteComponent', () => {
  let component: ListenAndWriteComponent;
  let fixture: ComponentFixture<ListenAndWriteComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ListenAndWriteComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ListenAndWriteComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
