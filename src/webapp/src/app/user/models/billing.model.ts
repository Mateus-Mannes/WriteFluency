import { AuthEntitlementStatus, AuthPlan } from '../../auth/models/auth-session.model';

export interface BillingEntitlementResponse {
  plan: AuthPlan;
  entitlementStatus: AuthEntitlementStatus;
  isPro: boolean;
  currentPeriodEndUtc: string | null;
  cancelAtPeriodEnd: boolean;
}

export interface CheckoutSessionResponse extends BillingEntitlementResponse {
  status: 'checkout_created' | 'subscription_management_required';
  checkoutUrl: string | null;
}

export interface PortalSessionResponse {
  portalUrl: string;
}

export interface ConfirmCheckoutSessionRequest {
  sessionId: string;
}
