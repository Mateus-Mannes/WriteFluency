import { CommonModule } from '@angular/common';
import {
  Component,
  HostListener,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

export type ProgressFeedbackDismissReason = 'not_now' | 'close';

@Component({
  selector: 'app-progress-feedback-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './progress-feedback-modal.component.html',
  styleUrl: './progress-feedback-modal.component.scss',
})
export class ProgressFeedbackModalComponent {
  private readonly sanitizer = inject(DomSanitizer);

  readonly isOpen = input(false);
  readonly embedUrl = input('');
  readonly watchUrl = input('');
  readonly title = input('A quick note from Matthew');

  readonly dismissed = output<ProgressFeedbackDismissReason>();
  readonly submitted = output<string>();

  readonly safeEmbedUrl = signal<SafeResourceUrl | null>(null);
  protected comment = '';

  constructor() {
    effect(() => {
      const url = this.embedUrl();
      this.safeEmbedUrl.set(url
        ? this.sanitizer.bypassSecurityTrustResourceUrl(url)
        : null);
    });

    effect(() => {
      if (!this.isOpen()) {
        this.comment = '';
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscapeKeyPressed(): void {
    if (!this.isOpen()) {
      return;
    }

    this.dismissed.emit('close');
  }

  protected onBackdropClick(): void {
    this.dismissed.emit('close');
  }

  protected onCloseClick(): void {
    this.dismissed.emit('close');
  }

  protected onNotNowClick(): void {
    this.dismissed.emit('not_now');
  }

  protected onSubmit(): void {
    const trimmedComment = this.comment.trim();
    if (!trimmedComment) {
      return;
    }

    this.submitted.emit(trimmedComment);
  }
}
