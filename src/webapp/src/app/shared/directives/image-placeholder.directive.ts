import { isPlatformServer } from '@angular/common';
import {
  Directive,
  ElementRef,
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

  constructor(
    @Inject(PLATFORM_ID) private platformId: object,
    private imageRef: ElementRef<HTMLImageElement>,
    private renderer: Renderer2
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (isPlatformServer(this.platformId)) {
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

    this.renderer.addClass(image, this.skeletonClass);

    if (image.complete && image.naturalWidth > 0) {
      this.removeSkeleton();
    }
  }

  removeSkeleton(): void {
    // use timeout to ensure this runs after load event
    setTimeout(() => {
      // assert the DOM element still exists before trying to remove class
      if (this.imageRef && this.imageRef.nativeElement) {
        this.renderer.removeClass(this.imageRef.nativeElement, this.skeletonClass);
      }
    }, 2000);
  }
}
