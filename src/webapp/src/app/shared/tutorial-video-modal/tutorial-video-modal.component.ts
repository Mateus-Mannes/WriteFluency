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
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

@Component({
  selector: 'app-tutorial-video-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tutorial-video-modal.component.html',
  styleUrl: './tutorial-video-modal.component.scss',
})
export class TutorialVideoModalComponent {
  private readonly sanitizer = inject(DomSanitizer);

  readonly isOpen = input(false);
  readonly embedUrl = input.required<string>();
  readonly watchUrl = input.required<string>();
  readonly title = input('Quick tutorial');

  readonly closed = output<void>();
  readonly opened = output<void>();

  readonly safeEmbedUrl = signal<SafeResourceUrl | null>(null);
  private hasEmittedOpened = false;

  constructor() {
    effect(() => {
      const url = this.embedUrl();
      this.safeEmbedUrl.set(url
        ? this.sanitizer.bypassSecurityTrustResourceUrl(url)
        : null);
    });

    effect(() => {
      const open = this.isOpen();
      if (open && !this.hasEmittedOpened) {
        this.hasEmittedOpened = true;
        this.opened.emit();
        return;
      }

      if (!open) {
        this.hasEmittedOpened = false;
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscapeKeyPressed(): void {
    if (!this.isOpen()) {
      return;
    }

    this.closed.emit();
  }

  onBackdropClick(): void {
    this.closed.emit();
  }

  onCloseClick(): void {
    this.closed.emit();
  }
}
