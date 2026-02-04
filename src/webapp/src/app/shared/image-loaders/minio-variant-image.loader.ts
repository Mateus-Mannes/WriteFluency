import { ImageLoaderConfig } from '@angular/common';
import { environment } from '../../../enviroments/enviroment';

const FALLBACK_WIDTH = 640;

function getWidth(config: ImageLoaderConfig): number {
  if (typeof config.width === 'number' && Number.isFinite(config.width)) {
    return Math.round(config.width);
  }

  const configuredDefault = config.loaderParams?.['defaultWidth'];
  if (typeof configuredDefault === 'number' && Number.isFinite(configuredDefault)) {
    return Math.round(configuredDefault);
  }

  return FALLBACK_WIDTH;
}

export function minioVariantImageLoader(config: ImageLoaderConfig): string {
  const src = config.src?.trim();
  if (!src) {
    return '';
  }

  const width = getWidth(config);
  return `${environment.minioUrl}/images/${src}_w${width}.webp`;
}
