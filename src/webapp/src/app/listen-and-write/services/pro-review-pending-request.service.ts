import { Injectable } from '@angular/core';
import { BrowserService } from '../../core/services/browser.service';
import * as constants from '../listen-and-write.constants';

export interface ProReviewPendingRequest {
  exerciseId: number;
  draftUserText: string;
  returnUrl: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  source: 'pro_review_login_cta';
}

@Injectable({ providedIn: 'root' })
export class ProReviewPendingRequestService {
  constructor(private browserService: BrowserService) {}

  save(exerciseId: number, draftUserText: string, returnUrl: string): void {
    const now = Date.now();
    const request: ProReviewPendingRequest = {
      exerciseId,
      draftUserText,
      returnUrl,
      createdAtUtc: new Date(now).toISOString(),
      expiresAtUtc: new Date(now + constants.proReviewPendingRequestExpiryMs).toISOString(),
      source: 'pro_review_login_cta',
    };

    this.browserService.setSessionItem(
      constants.proReviewPendingRequestStorageKey,
      JSON.stringify(request));
  }

  consumeForExercise(exerciseId: number): ProReviewPendingRequest | null {
    const request = this.peek();
    if (!request || request.exerciseId !== exerciseId || this.isExpired(request)) {
      if (request && (request.exerciseId === exerciseId || this.isExpired(request))) {
        this.clear();
      }

      return null;
    }

    this.clear();
    return request;
  }

  peek(): ProReviewPendingRequest | null {
    const rawValue = this.browserService.getSessionItem(constants.proReviewPendingRequestStorageKey);
    if (!rawValue) {
      return null;
    }

    try {
      const parsed = JSON.parse(rawValue) as Partial<ProReviewPendingRequest>;
      if (!Number.isFinite(parsed.exerciseId)
          || typeof parsed.draftUserText !== 'string'
          || typeof parsed.returnUrl !== 'string'
          || typeof parsed.expiresAtUtc !== 'string') {
        return null;
      }

      return parsed as ProReviewPendingRequest;
    } catch {
      return null;
    }
  }

  isExpired(request: ProReviewPendingRequest): boolean {
    const expiresAtMs = Date.parse(request.expiresAtUtc);
    return !Number.isFinite(expiresAtMs) || expiresAtMs <= Date.now();
  }

  clear(): void {
    this.browserService.removeSessionItem(constants.proReviewPendingRequestStorageKey);
  }
}
