import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';

import { PropositionComponent } from './proposition.component';
import { ListenAndWriteService } from '../listen-and-write.service';
import { AlertService } from 'src/app/shared/services/alert-service';
import { SharedModule } from 'src/app/shared/shared.module';

describe('PropositionComponent', () => {
  let component: PropositionComponent;
  let fixture: ComponentFixture<PropositionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, SharedModule],
      declarations: [ PropositionComponent ],
      providers: [ListenAndWriteService, AlertService]
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
