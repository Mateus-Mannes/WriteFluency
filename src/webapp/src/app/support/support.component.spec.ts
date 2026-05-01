import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { SeoService } from '../core/services/seo.service';
import { SupportApiService } from './support-api.service';
import { SupportComponent } from './support.component';

describe('SupportComponent', () => {
  let component: SupportComponent;
  let fixture: ComponentFixture<SupportComponent>;
  let supportApiServiceSpy: jasmine.SpyObj<SupportApiService>;

  beforeEach(async () => {
    supportApiServiceSpy = jasmine.createSpyObj<SupportApiService>('SupportApiService', ['submitRequest']);
    supportApiServiceSpy.submitRequest.and.returnValue(of({ accepted: true }));

    await TestBed.configureTestingModule({
      imports: [SupportComponent],
      providers: [
        provideNoopAnimations(),
        { provide: SupportApiService, useValue: supportApiServiceSpy },
        {
          provide: SeoService,
          useValue: {
            updateMetaTags: jasmine.createSpy('updateMetaTags'),
            generateBreadcrumbStructuredData: jasmine.createSpy('generateBreadcrumbStructuredData').and.returnValue({}),
            addStructuredData: jasmine.createSpy('addStructuredData'),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SupportComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should validate required message', async () => {
    await component.submitSupportRequest();

    expect(component.supportForm.controls.message.hasError('required')).toBeTrue();
    expect(supportApiServiceSpy.submitRequest).not.toHaveBeenCalled();
  });

  it('should show success state and prevent duplicate submit after successful request', async () => {
    component.supportForm.setValue({
      message: 'Please help me with an exercise.',
      replyEmail: 'guest@writefluency.test',
    });

    await component.submitSupportRequest();
    await component.submitSupportRequest();
    fixture.detectChanges();

    expect(component.submitSucceeded()).toBeTrue();
    expect(component.supportForm.controls.message.getRawValue()).toBe('');
    expect(supportApiServiceSpy.submitRequest).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('Request sent');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });

  it('should show retryable error when request fails', async () => {
    supportApiServiceSpy.submitRequest.and.returnValue(throwError(() => ({ status: 500 })));
    component.supportForm.setValue({
      message: 'Please help me with an exercise.',
      replyEmail: '',
    });

    await component.submitSupportRequest();
    fixture.detectChanges();

    expect(component.submitSucceeded()).toBeFalse();
    expect(component.submitError()).toBe('Could not send your support request right now. Please try again.');
    expect(fixture.nativeElement.textContent).toContain('Could not send your support request right now');
  });
});
