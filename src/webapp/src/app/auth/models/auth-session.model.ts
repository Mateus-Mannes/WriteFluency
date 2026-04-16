export interface AuthSession {
  isAuthenticated: boolean;
  userId: string | null;
  email: string | null;
  emailConfirmed: boolean;
  issuedAtUtc: string | null;
  expiresAtUtc: string | null;
}

export interface AuthSessionState {
  isAuthenticated: boolean;
  userId: string | null;
  email: string | null;
  emailConfirmed: boolean;
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
