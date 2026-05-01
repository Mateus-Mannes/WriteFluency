import { CommonModule, isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { firstValueFrom } from 'rxjs';
import { SeoService } from '../core/services/seo.service';
import { SupportApiService } from './support-api.service';

const maxSupportMessageLength = 4000;

@Component({
  selector: 'app-support',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './support.component.html',
  styleUrls: ['./support.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupportComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly supportApiService = inject(SupportApiService);
  private readonly seoService = inject(SeoService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);

  readonly isSubmitting = signal(false);
  readonly submitSucceeded = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly maxMessageLength = maxSupportMessageLength;

  readonly supportForm = this.formBuilder.nonNullable.group({
    message: ['', [Validators.required, Validators.maxLength(maxSupportMessageLength)]],
    replyEmail: ['', [Validators.email]],
  });

  ngOnInit(): void {
    this.seoService.updateMetaTags({
      title: 'Support | WriteFluency',
      description: 'Send a support request to the WriteFluency team.',
      keywords: 'WriteFluency support, help, bug report, contact',
      type: 'website',
      url: '/support',
    });

    const breadcrumbData = this.seoService.generateBreadcrumbStructuredData([
      { name: 'Home', url: '/' },
      { name: 'Support', url: '/support' },
    ]);
    this.seoService.addStructuredData(breadcrumbData);
  }

  async submitSupportRequest(): Promise<void> {
    if (this.isSubmitting() || this.submitSucceeded()) {
      return;
    }

    if (this.supportForm.invalid) {
      this.supportForm.markAllAsTouched();
      return;
    }

    const message = this.supportForm.controls.message.getRawValue().trim();
    const replyEmail = this.normalizeOptional(this.supportForm.controls.replyEmail.getRawValue());
    this.supportForm.controls.message.setValue(message);

    this.isSubmitting.set(true);
    this.submitError.set(null);

    try {
      await firstValueFrom(this.supportApiService.submitRequest({
        message,
        replyEmail,
        sourceUrl: this.getSourceUrl(),
      }));

      this.supportForm.reset();
      this.submitSucceeded.set(true);
    } catch (error: unknown) {
      this.submitError.set(this.buildSubmitErrorMessage(error));
    } finally {
      this.isSubmitting.set(false);
    }
  }

  private normalizeOptional(value: string): string | null {
    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  private getSourceUrl(): string | null {
    return this.isBrowser ? window.location.href : null;
  }

  private buildSubmitErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 429) {
      return 'Too many support requests were sent recently. Please try again later.';
    }

    if (error instanceof HttpErrorResponse && error.status === 400) {
      return 'Check the message and reply email, then try again.';
    }

    return 'Could not send your support request right now. Please try again.';
  }
}
