import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../enviroments/enviroment';
import {
  BillingEntitlementResponse,
  CheckoutSessionResponse,
  ConfirmCheckoutSessionRequest,
} from '../models/billing.model';

@Injectable({ providedIn: 'root' })
export class BillingApiService {
  private readonly usersApiUrl = environment.usersApiUrl.replace(/\/$/, '');
  private readonly basePath = `${this.usersApiUrl}/users/billing`;

  constructor(private readonly http: HttpClient) {}

  createCheckoutSession(): Observable<CheckoutSessionResponse> {
    return this.http.post<CheckoutSessionResponse>(
      `${this.basePath}/checkout-session`,
      {},
      { withCredentials: true },
    );
  }

  confirmCheckoutSession(sessionId: string): Observable<BillingEntitlementResponse> {
    const payload: ConfirmCheckoutSessionRequest = { sessionId };
    return this.http.post<BillingEntitlementResponse>(
      `${this.basePath}/checkout-session/confirm`,
      payload,
      { withCredentials: true },
    );
  }
}
