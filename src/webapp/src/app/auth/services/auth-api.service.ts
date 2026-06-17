import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthSession, ExternalProvider } from '../models/auth-session.model';
import { environment } from '../../../enviroments/enviroment';
import { FeedbackPromptStatusResponse } from '../models/feedback-prompt.model';

export interface PasswordlessVerifyResponse {
  isNewUser: boolean;
}

export type PasswordContinueStatus =
  | 'signed_in'
  | 'confirmation_required'
  | 'wrong_password'
  | 'password_setup_required'
  | 'account_locked';

export interface PasswordContinueResponse {
  status: PasswordContinueStatus;
  isNewUser: boolean;
}

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly usersApiUrl = environment.usersApiUrl.replace(/\/$/, '');
  private readonly basePath = `${this.usersApiUrl}/users/auth`;

  constructor(private readonly http: HttpClient) {}

  register(email: string, password: string): Observable<unknown> {
    return this.http.post(`${this.basePath}/register`, { email, password }, { withCredentials: true });
  }

  loginPassword(email: string, password: string): Observable<unknown> {
    const params = new HttpParams().set('useCookies', 'true');
    return this.http.post(`${this.basePath}/login`, { email, password }, { params, withCredentials: true });
  }

  continueWithPassword(email: string, password: string, sendEmail = true): Observable<PasswordContinueResponse> {
    return this.http.post<PasswordContinueResponse>(
      `${this.basePath}/password/continue`,
      { email, password, sendEmail },
      { withCredentials: true },
    );
  }

  logout(): Observable<unknown> {
    return this.http.post(`${this.basePath}/logout`, {}, { withCredentials: true });
  }

  session(): Observable<AuthSession> {
    return this.http.get<AuthSession>(`${this.basePath}/session`, { withCredentials: true });
  }

  markListenWriteTutorialCompleted(): Observable<{ listenWriteTutorialCompleted: true }> {
    return this.http.post<{ listenWriteTutorialCompleted: true }>(
      `${this.basePath}/tutorial/listen-write/completed`,
      {},
      { withCredentials: true },
    );
  }

  feedbackPromptStatus(campaignKey: string): Observable<FeedbackPromptStatusResponse> {
    const encodedCampaignKey = encodeURIComponent(campaignKey);
    return this.http.get<FeedbackPromptStatusResponse>(
      `${this.basePath}/feedback-prompts/${encodedCampaignKey}/status`,
      { withCredentials: true },
    );
  }

  markFeedbackPromptShown(campaignKey: string): Observable<FeedbackPromptStatusResponse> {
    return this.markFeedbackPrompt(campaignKey, 'shown');
  }

  markFeedbackPromptDismissed(campaignKey: string): Observable<FeedbackPromptStatusResponse> {
    return this.markFeedbackPrompt(campaignKey, 'dismissed');
  }

  markFeedbackPromptSubmitted(campaignKey: string): Observable<FeedbackPromptStatusResponse> {
    return this.markFeedbackPrompt(campaignKey, 'submitted');
  }

  requestOtp(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.basePath}/passwordless/request`, { email }, { withCredentials: true });
  }

  verifyOtp(email: string, code: string): Observable<PasswordlessVerifyResponse> {
    return this.http.post<PasswordlessVerifyResponse>(`${this.basePath}/passwordless/verify`, { email, code }, { withCredentials: true });
  }

  externalProviders(): Observable<ExternalProvider[]> {
    return this.http.get<ExternalProvider[]>(`${this.basePath}/external/providers`, { withCredentials: true });
  }

  confirmEmail(userId: string, code: string): Observable<unknown> {
    const params = new HttpParams()
      .set('userId', userId)
      .set('code', code);

    return this.http.get(`${this.basePath}/confirmEmail`, {
      params,
      withCredentials: true,
      responseType: 'text',
    });
  }

  confirmPasswordSetup(token: string): Observable<{ status: string }> {
    return this.http.post<{ status: string }>(`${this.basePath}/password/setup/confirm`, { token }, { withCredentials: true });
  }

  resetPassword(email: string, resetCode: string, newPassword: string): Observable<unknown> {
    return this.http.post(`${this.basePath}/resetPassword`, {
      email,
      resetCode,
      newPassword,
    }, { withCredentials: true });
  }

  private markFeedbackPrompt(
    campaignKey: string,
    action: 'shown' | 'dismissed' | 'submitted',
  ): Observable<FeedbackPromptStatusResponse> {
    const encodedCampaignKey = encodeURIComponent(campaignKey);
    return this.http.post<FeedbackPromptStatusResponse>(
      `${this.basePath}/feedback-prompts/${encodedCampaignKey}/${action}`,
      {},
      { withCredentials: true },
    );
  }
}
