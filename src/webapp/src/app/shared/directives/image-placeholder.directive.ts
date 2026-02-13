import { isPlatformServer } from '@angular/common';
import {
  Directive,
  ElementRef,
  HostBinding,
  HostListener,
  Inject,
  Input,
  OnChanges,
  PLATFORM_ID,
  Renderer2,
  SimpleChanges,
} from '@angular/core';

type ImageSrc = string | null | undefined;

@Directive({
  selector: 'img[appImagePlaceholder]',
  standalone: true,
})
export class ImagePlaceholderDirective implements OnChanges {
  @Input() appImagePlaceholder: ImageSrc = null;

  private readonly skeletonClass = 'wf-image-skeleton';
  @HostBinding('class.wf-image-skeleton') isSkeleton = true;

  constructor(
    @Inject(PLATFORM_ID) private platformId: object,
    private imageRef: ElementRef<HTMLImageElement>,
    private renderer: Renderer2
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (isPlatformServer(this.platformId)) {
      this.isSkeleton = true;
      return;
    }

    if (changes['appImagePlaceholder']) {
      this.resetSkeleton();
    }
  }

  @HostListener('load')
  onLoad(): void {
    this.removeSkeleton();
  }

  @HostListener('error')
  onError(): void {
    this.removeSkeleton();
  }

  private resetSkeleton(): void {
    const image = this.imageRef.nativeElement;
    const resolvedSrc =
      this.appImagePlaceholder || image.currentSrc || image.getAttribute('src');

    if (!resolvedSrc) {
      this.removeSkeleton();
      return;
    }

    this.isSkeleton = true;
    this.renderer.addClass(image, this.skeletonClass);

    if (image.complete && image.naturalWidth > 0) {
      this.removeSkeleton();
    }
  }

  removeSkeleton(): void {
    this.isSkeleton = false;
    this.renderer.removeClass(this.imageRef.nativeElement, this.skeletonClass);
  }
}
