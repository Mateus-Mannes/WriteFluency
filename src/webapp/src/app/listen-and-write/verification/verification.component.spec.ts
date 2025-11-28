import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VerificationComponent, VerificationData } from './verification.component';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';

describe('VerificationComponent', () => {
  let component: VerificationComponent;
  let fixture: ComponentFixture<VerificationComponent>;

  const dialogData: VerificationData = {
    originalText: 'Original text here',
    userText: 'User text here',
    comparisions: []
  };

  const matDialogRefMock = {
    close: jasmine.createSpy('close')
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        VerificationComponent,
        MatTooltipModule,
      ],
      providers: [ 
        { provide: MAT_DIALOG_DATA, useValue: dialogData },
        { provide: MatDialogRef, useValue: matDialogRefMock }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(VerificationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
