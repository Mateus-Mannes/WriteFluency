import { Injectable, Optional } from '@angular/core';
import { Insights, InsightsMeasurements, InsightsProperties } from '../../../telemetry/insights.service';
import { AuthSessionState } from '../../auth/models/auth-session.model';
import { AuthSessionStore } from '../../auth/services/auth-session.store';

type ProCtaPropertyValue = string | number | boolean | null | undefined;

export type ProCtaTelemetryKind = 'shown' | 'clicked';

export interface ProCtaTelemetryContext {
  ctaId: string;
  location: string;
  label: string;
  destination?: string | null;
  source?: string | null;
  accessStatus?: string | null;
  reviewStatus?: string | null;
  exerciseId?: number | string | null;
  returnUrl?: string | null;
  extraProperties?: Record<string, ProCtaPropertyValue>;
  measurements?: InsightsMeasurements;
}

@Injectable({ providedIn: 'root' })
export class ProCtaTelemetryService {
  constructor(
    private readonly authSessionStore: AuthSessionStore,
    @Optional() private readonly insights: Insights | null,
  ) {}

  trackShown(context: ProCtaTelemetryContext): void {
    this.track('shown', context);
  }

  trackClicked(context: ProCtaTelemetryContext): void {
    this.track('clicked', context);
  }

  private track(kind: ProCtaTelemetryKind, context: ProCtaTelemetryContext): void {
    const state = this.getAuthState();
    const properties: InsightsProperties = {
      event_area: 'pro_cta',
      cta_id: context.ctaId,
      cta_location: context.location,
      cta_label: context.label,
      cta_kind: kind,
      cta_destination: context.destination ?? '',
      source: context.source ?? '',
      access_status: context.accessStatus ?? '',
      review_status: context.reviewStatus ?? '',
      exercise_id: String(context.exerciseId ?? ''),
      return_url: context.returnUrl ?? '',
      is_authenticated: String(state.isAuthenticated),
      user_id: state.userId ?? '',
      user_email: state.email ?? '',
      plan: state.plan,
      entitlement_status: state.entitlementStatus,
      is_pro: String(state.isPro),
      ...this.toInsightsProperties(context.extraProperties ?? {}),
    };

    if (typeof this.insights?.trackEvent !== 'function') {
      return;
    }

    this.insights.trackEvent(`pro_cta_${kind}`, properties, context.measurements);
  }

  private toInsightsProperties(properties: Record<string, ProCtaPropertyValue>): InsightsProperties {
    return Object.fromEntries(
      Object.entries(properties)
        .filter(([, value]) => value !== undefined)
        .map(([key, value]) => [key, value === null ? '' : String(value)])
    );
  }

  private getAuthState(): Pick<
    AuthSessionState,
    'isAuthenticated' | 'userId' | 'email' | 'plan' | 'entitlementStatus' | 'isPro'
  > {
    const store = this.authSessionStore as unknown as {
      state?: () => AuthSessionState;
      isAuthenticated?: () => boolean;
      userId?: () => string | null;
      email?: () => string | null;
      plan?: () => AuthSessionState['plan'];
      entitlementStatus?: () => AuthSessionState['entitlementStatus'];
      isPro?: () => boolean;
    };

    if (typeof store.state === 'function') {
      return store.state();
    }

    return {
      isAuthenticated: store.isAuthenticated?.() ?? false,
      userId: store.userId?.() ?? null,
      email: store.email?.() ?? null,
      plan: store.plan?.() ?? 'free',
      entitlementStatus: store.entitlementStatus?.() ?? 'free',
      isPro: store.isPro?.() ?? false,
    };
  }
}
