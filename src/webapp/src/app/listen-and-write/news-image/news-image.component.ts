import { CommonModule, IMAGE_LOADER, NgOptimizedImage } from '@angular/common';
import { Component, computed, effect, input, signal } from '@angular/core';
import { Proposition } from 'src/api/listen-and-write';
import { minioVariantImageLoader } from '../../shared/image-loaders/minio-variant-image.loader';

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
  readonly imageLoaderParams = { defaultWidth: 640 };
  imageLoadFailed = signal(false);

  imageBaseId = computed(() => this.getImageBaseId(this.proposition()?.imageFileId));

  constructor() {
    effect(() => {
      this.proposition();
      this.imageLoadFailed.set(false);
    });
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
}
