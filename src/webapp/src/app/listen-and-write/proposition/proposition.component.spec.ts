import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { PropositionComponent } from './proposition.component';
import { ListenAndWriteService } from '../listen-and-write.service';
import { AlertService } from 'src/app/shared/services/alert-service';
import { SharedModule } from 'src/app/shared/shared.module';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PropositionComponent', () => {
  let component: PropositionComponent;
  let fixture: ComponentFixture<PropositionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
    declarations: [PropositionComponent],
    imports: [SharedModule],
    providers: [ListenAndWriteService, AlertService, provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
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
