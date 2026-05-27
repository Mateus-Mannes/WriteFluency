export type AuthPlan = 'free' | 'pro';

export type AuthEntitlementStatus = 'free' | 'pro_active' | 'pro_canceling' | 'pro_expired';

export interface AuthSession {
  isAuthenticated: boolean;
  userId: string | null;
  email: string | null;
  emailConfirmed: boolean;
  listenWriteTutorialCompleted?: boolean;
  plan: AuthPlan;
  entitlementStatus: AuthEntitlementStatus;
  isPro: boolean;
  currentPeriodEndUtc: string | null;
  cancelAtPeriodEnd: boolean;
  issuedAtUtc: string | null;
  expiresAtUtc: string | null;
}

export interface AuthSessionState {
  isAuthenticated: boolean;
  userId: string | null;
  email: string | null;
  emailConfirmed: boolean;
  listenWriteTutorialCompleted: boolean | null;
  plan: AuthPlan;
  entitlementStatus: AuthEntitlementStatus;
  isPro: boolean;
  currentPeriodEndUtc: string | null;
  cancelAtPeriodEnd: boolean;
  hasReliableSessionState: boolean;
  issuedAtUtc: string | null;
  expiresAtUtc: string | null;
  isLoading: boolean;
  error: string | null;
}

export interface ExternalProvider {
  id: string;
  displayName: string;
  startEndpoint: string;
}

export interface CallbackResult {
  auth: 'success' | 'error' | null;
  provider: string | null;
  code: string | null;
}
