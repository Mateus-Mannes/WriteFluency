import { CommonModule, IMAGE_LOADER, NgOptimizedImage } from '@angular/common';
import { Component, HostListener, computed, effect, input, signal } from '@angular/core';
import { Proposition } from 'src/api/listen-and-write';
import { minioVariantImageLoader } from '../../shared/image-loaders/minio-variant-image.loader';
import { BrowserService } from '../../core/services/browser.service';

@Component({
  selector: 'app-news-image',
  imports: [ NgOptimizedImage, CommonModule ],
  templateUrl: './news-image.component.html',
  styleUrl: './news-image.component.scss',
  providers: [
    {
      provide: IMAGE_LOADER,
      useValue: minioVariantImageLoader,
    }
  ],
})
export class NewsImageComponent {

  proposition = input<Proposition | null>();
  private readonly mobileMaxWidth = 600;
  private readonly mobileSrcset = '320w, 512w, 640w';
  private readonly desktopSrcset = '320w, 512w, 640w, 1024w';
  readonly imageLoaderParams = { defaultWidth: 640 };
  readonly isMobileLayout = signal(false);
  readonly imageSrcset = computed(() => (
    this.isMobileLayout() ? this.mobileSrcset : this.desktopSrcset
  ));
  imageLoadFailed = signal(false);

  imageBaseId = computed(() => this.getImageBaseId(this.proposition()?.imageFileId));

  constructor(private browserService: BrowserService) {
    effect(() => {
      this.proposition();
      this.imageLoadFailed.set(false);
    });
    this.updateMobileLayout();
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.updateMobileLayout();
  }

  onOptimizedImageError(): void {
    this.imageLoadFailed.set(true);
  }

  private getImageBaseId(imageFileId?: string | null): string | null {
    if (!imageFileId) {
      return null;
    }
    const lastDot = imageFileId.lastIndexOf('.');
    const baseId = lastDot > 0 ? imageFileId.slice(0, lastDot) : imageFileId;
    return baseId.replace(/_w\d+$/, '');
  }

  private updateMobileLayout(): void {
    const width = this.browserService.getWindowWidth();
    this.isMobileLayout.set(width > 0 && width <= this.mobileMaxWidth);
  }
}
