import { Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { BrowserService } from '../../core/services/browser.service';
import { ExerciseSessionTrackingService } from './exercise-session-tracking.service';
import * as constants from '../listen-and-write.constants';
import * as models from '../listen-and-write.models';

export interface PrepareGuestExerciseBeginRequest {
  isFirstTimeUser: boolean;
  audioEndedBeforeBegin: boolean;
}

export interface GuestExerciseBeginPromptResult {
  context: models.BeginExerciseContext;
  decision: models.GuestLoginModalDecision;
}

@Injectable()
export class GuestExerciseLoginPromptService {
  readonly isOpen = signal<boolean>(false);

  private pendingBeginExerciseContext: models.BeginExerciseContext | null = null;

  constructor(
    private authSessionStore: AuthSessionStore,
    private browserService: BrowserService,
    private exerciseSessionTracking: ExerciseSessionTrackingService,
    private router: Router,
  ) {}

  reset(): void {
    this.isOpen.set(false);
    this.pendingBeginExerciseContext = null;
  }

  prepareBeginExercise(request: PrepareGuestExerciseBeginRequest): GuestExerciseBeginPromptResult {
    const context = this.buildBeginExerciseContext(request);
    const decision = this.evaluateDecision(context);

    this.exerciseSessionTracking.trackEvent('begin_exercise_clicked', {
      is_first_time: context.isFirstTimeUser,
      audio_ended_before_begin: context.audioEndedBeforeBegin,
      is_authenticated: this.authSessionStore.isAuthenticated(),
      guest_begin_attempt_count: context.guestBeginAttemptCount,
      guest_login_modal_decision: decision.reason,
    }, {
      guest_begin_attempt_count: context.guestBeginAttemptCount ?? 0,
      guest_login_modal_cooldown_remaining_minutes: Math.ceil(decision.cooldownRemainingMs / 60000),
    });

    if (!decision.shouldShow && !this.authSessionStore.isAuthenticated() && decision.reason !== 'authenticated') {
      this.exerciseSessionTracking.trackEvent('guest_login_modal_not_shown', {
        reason: decision.reason,
        guest_begin_attempt_count: context.guestBeginAttemptCount,
      }, {
        guest_begin_attempt_count: context.guestBeginAttemptCount ?? 0,
        guest_login_modal_cooldown_remaining_minutes: Math.ceil(decision.cooldownRemainingMs / 60000),
      });
    }

    return {
      context,
      decision,
    };
  }

  open(context: models.BeginExerciseContext): void {
    this.pendingBeginExerciseContext = context;
    this.isOpen.set(true);
    this.browserService.setItem(constants.guestBeginLoginModalLastShownStorageKey, new Date().toISOString());

    this.exerciseSessionTracking.trackEvent('guest_login_modal_shown', {
      source: 'begin_exercise',
      guest_begin_attempt_count: context.guestBeginAttemptCount,
      is_first_time: context.isFirstTimeUser,
      audio_ended_before_begin: context.audioEndedBeforeBegin,
    }, {
      guest_begin_attempt_count: context.guestBeginAttemptCount ?? 0,
      guest_login_modal_cooldown_hours: constants.guestBeginLoginModalCooldownMs / (60 * 60 * 1000),
    });
  }

  signIn(returnUrl: string): void {
    const beginContext = this.pendingBeginExerciseContext;

    this.exerciseSessionTracking.trackEvent('guest_login_modal_login_clicked', {
      source: 'begin_exercise',
      return_url: returnUrl,
      guest_begin_attempt_count: beginContext?.guestBeginAttemptCount,
    }, {
      guest_begin_attempt_count: beginContext?.guestBeginAttemptCount ?? 0,
    });

    this.reset();

    void this.router.navigate(['/auth/login'], {
      queryParams: {
        returnUrl,
        source: 'begin_exercise_modal',
      },
    });
  }

  continueAsGuest(): models.BeginExerciseContext | null {
    return this.dismiss('continue_as_guest');
  }

  dismissBackdrop(): models.BeginExerciseContext | null {
    return this.dismiss('backdrop');
  }

  private dismiss(reason: models.GuestLoginModalDismissReason): models.BeginExerciseContext | null {
    const beginContext = this.pendingBeginExerciseContext;

    this.exerciseSessionTracking.trackEvent('guest_login_modal_dismissed', {
      reason,
      source: 'begin_exercise',
      guest_begin_attempt_count: beginContext?.guestBeginAttemptCount,
    }, {
      guest_begin_attempt_count: beginContext?.guestBeginAttemptCount ?? 0,
    });

    this.reset();

    if (!beginContext) {
      return null;
    }

    return {
      ...beginContext,
      guestLoginModalShownBeforeStart: true,
    };
  }

  private buildBeginExerciseContext(request: PrepareGuestExerciseBeginRequest): models.BeginExerciseContext {
    return {
      isFirstTimeUser: request.isFirstTimeUser,
      audioEndedBeforeBegin: request.audioEndedBeforeBegin,
      guestBeginAttemptCount: this.incrementGuestBeginAttemptCountIfGuest(),
      guestLoginModalShownBeforeStart: false,
    };
  }

  private evaluateDecision(context: models.BeginExerciseContext): models.GuestLoginModalDecision {
    if (this.authSessionStore.isAuthenticated()) {
      return {
        shouldShow: false,
        reason: 'authenticated',
        cooldownRemainingMs: 0,
      };
    }

    const guestAttempt = context.guestBeginAttemptCount ?? 0;
    if (guestAttempt < constants.guestBeginLoginModalAttemptThreshold) {
      return {
        shouldShow: false,
        reason: 'below_threshold',
        cooldownRemainingMs: 0,
      };
    }

    const cooldownRemainingMs = this.getGuestLoginModalCooldownRemainingMs(Date.now());
    if (cooldownRemainingMs > 0) {
      return {
        shouldShow: false,
        reason: 'cooldown_active',
        cooldownRemainingMs,
      };
    }

    return {
      shouldShow: true,
      reason: 'eligible',
      cooldownRemainingMs: 0,
    };
  }

  private incrementGuestBeginAttemptCountIfGuest(): number | null {
    if (this.authSessionStore.isAuthenticated()) {
      return null;
    }

    const currentAttempt = this.readPositiveIntFromStorage(constants.guestBeginAttemptCountStorageKey);
    const nextAttempt = currentAttempt + 1;
    this.browserService.setItem(constants.guestBeginAttemptCountStorageKey, String(nextAttempt));
    return nextAttempt;
  }

  private getGuestLoginModalCooldownRemainingMs(nowMs: number): number {
    const rawLastShown = this.browserService.getItem(constants.guestBeginLoginModalLastShownStorageKey);
    if (!rawLastShown) {
      return 0;
    }

    const lastShownMs = Date.parse(rawLastShown);
    if (!Number.isFinite(lastShownMs)) {
      return 0;
    }

    const elapsedMs = Math.max(0, nowMs - lastShownMs);
    return Math.max(0, constants.guestBeginLoginModalCooldownMs - elapsedMs);
  }

  private readPositiveIntFromStorage(key: string): number {
    const raw = this.browserService.getItem(key);
    if (!raw) {
      return 0;
    }

    const parsed = Number.parseInt(raw, 10);
    if (!Number.isFinite(parsed) || parsed < 0) {
      return 0;
    }

    return parsed;
  }
}
