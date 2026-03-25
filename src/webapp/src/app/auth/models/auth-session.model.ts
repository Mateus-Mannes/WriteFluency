export interface AuthSession {
  isAuthenticated: boolean;
  userId: string | null;
  email: string | null;
  emailConfirmed: boolean;
}

export interface AuthSessionState {
  isAuthenticated: boolean;
  userId: string | null;
  email: string | null;
  emailConfirmed: boolean;
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
