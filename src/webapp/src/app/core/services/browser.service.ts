import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

@Injectable({
  providedIn: 'root'
})
export class BrowserService {
  private platformId = inject(PLATFORM_ID);
  private isBrowser: boolean;

  constructor() {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  // LocalStorage methods
  getItem(key: string): string | null {
    if (!this.isBrowser) return null;
    try {
      return localStorage.getItem(key);
    } catch (error) {
      console.error('Error accessing localStorage:', error);
      return null;
    }
  }

  setItem(key: string, value: string): void {
    if (!this.isBrowser) return;
    try {
      localStorage.setItem(key, value);
    } catch (error) {
      console.error('Error writing to localStorage:', error);
    }
  }

  removeItem(key: string): void {
    if (!this.isBrowser) return;
    try {
      localStorage.removeItem(key);
    } catch (error) {
      console.error('Error removing from localStorage:', error);
    }
  }

  // Window methods
  scrollToTop(): void {
    if (!this.isBrowser) return;
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  getWindowWidth(): number {
    if (!this.isBrowser) return 0;
    return window.innerWidth;
  }

  navigateTo(url: string): void {
    if (!this.isBrowser) return;
    window.location.href = url;
  }

  // Document methods
  addEventListener(event: string, handler: EventListener): void {
    if (!this.isBrowser) return;
    document.addEventListener(event, handler);
  }

  removeEventListener(event: string, handler: EventListener): void {
    if (!this.isBrowser) return;
    document.removeEventListener(event, handler);
  }

  isDocumentHidden(): boolean {
    if (!this.isBrowser) return false;
    return document.hidden;
  }

  blurActiveElement(): void {
    if (!this.isBrowser) return;

    const activeElement = document.activeElement;
    if (activeElement instanceof HTMLElement) {
      activeElement.blur();
    }
  }
}
