import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ListenAndWriteComponent } from './listen-and-write.component';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { AlertService } from '../shared/services/alert-service';
import { ListenAndWriteService } from './listen-and-write.service';
import { VerificationData } from './verification/verification.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ListenAndWriteModule } from './listen-and-write.module';

describe('ListenAndWriteComponent', () => {
  let component: ListenAndWriteComponent;
  let fixture: ComponentFixture<ListenAndWriteComponent>;

  beforeEach(async () => {

    const dialogData: VerificationData = {
      originalText: 'Original text here',
      userText: 'User text here',
      comparisions: []
    };

    const matDialogRefMock = {
      close: jasmine.createSpy('close')
    };

    await TestBed.configureTestingModule({
      imports: [ 
        HttpClientTestingModule,
        ListenAndWriteModule,
       ],
      declarations: [ ListenAndWriteComponent ],
      providers: [ 
        AlertService, 
        ListenAndWriteService,
        { provide: MAT_DIALOG_DATA, useValue: dialogData },
        { provide: MatDialogRef, useValue: matDialogRefMock }
      ]
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
