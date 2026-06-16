import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';

const signupConversionSendTo = 'AW-17978787910/XTDXCKykj8AcEMaQ-vxC';

interface Gtag {
  (command: 'set', target: 'user_data', params: Record<string, unknown>): void;
  (command: 'event', eventName: 'conversion', params: Record<string, unknown>): void;
}

@Injectable({
  providedIn: 'root',
})
export class GoogleAdsConversionService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);

  trackSignup(email: string): void {
    const normalizedEmail = this.normalizeEmail(email);
    if (!normalizedEmail || !this.isBrowser) {
      return;
    }

    const gtag = (globalThis as typeof globalThis & { gtag?: Gtag }).gtag;
    if (typeof gtag !== 'function') {
      return;
    }

    gtag('set', 'user_data', {
      email: normalizedEmail,
    });
    gtag('event', 'conversion', {
      send_to: signupConversionSendTo,
      value: 1.0,
      currency: 'BRL',
    });
  }

  private normalizeEmail(email: string): string | null {
    const normalized = email.trim().toLowerCase();
    if (!normalized) {
      return null;
    }

    const [localPart, domain] = normalized.split('@');
    if (!localPart || !domain) {
      return normalized;
    }

    if (domain === 'gmail.com' || domain === 'googlemail.com') {
      return `${localPart.replace(/\./g, '')}@${domain}`;
    }

    return normalized;
  }
}
