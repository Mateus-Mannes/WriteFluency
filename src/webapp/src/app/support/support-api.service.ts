import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../enviroments/enviroment';

export interface SupportRequestPayload {
  message: string;
  replyEmail?: string | null;
  sourceUrl?: string | null;
}

export interface SupportRequestResponse {
  accepted: true;
}

@Injectable({ providedIn: 'root' })
export class SupportApiService {
  private readonly usersApiUrl = environment.usersApiUrl.replace(/\/$/, '');
  private readonly supportRequestsUrl = `${this.usersApiUrl}/users/support/requests`;

  constructor(private readonly http: HttpClient) {}

  submitRequest(payload: SupportRequestPayload): Observable<SupportRequestResponse> {
    return this.http.post<SupportRequestResponse>(
      this.supportRequestsUrl,
      payload,
      { withCredentials: true },
    );
  }
}
