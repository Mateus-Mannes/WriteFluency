import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthSession, ExternalProvider } from '../models/auth-session.model';
import { environment } from '../../../enviroments/enviroment';

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

  requestOtp(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.basePath}/passwordless/request`, { email }, { withCredentials: true });
  }

  verifyOtp(email: string, code: string): Observable<unknown> {
    return this.http.post(`${this.basePath}/passwordless/verify`, { email, code }, { withCredentials: true });
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
}
