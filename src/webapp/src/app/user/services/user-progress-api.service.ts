import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../enviroments/enviroment';
import {
  CompleteProgressRequest,
  ProgressAttemptResponse,
  ProgressItemResponse,
  ProgressOperationResponse,
  ProgressStateResponse,
  ProgressSummaryResponse,
  SaveProgressStateRequest,
  StartProgressRequest,
} from '../models/user-progress.model';

@Injectable({ providedIn: 'root' })
export class UserProgressApiService {
  private readonly usersProgressApiUrl = environment.usersProgressApiUrl.replace(/\/$/, '');
  private readonly basePath = `${this.usersProgressApiUrl}/users/progress`;

  constructor(private readonly http: HttpClient) {}

  start(request: StartProgressRequest): Observable<ProgressOperationResponse> {
    return this.http.post<ProgressOperationResponse>(`${this.basePath}/start`, request, {
      withCredentials: true,
    });
  }

  complete(request: CompleteProgressRequest): Observable<ProgressOperationResponse> {
    return this.http.post<ProgressOperationResponse>(`${this.basePath}/complete`, request, {
      withCredentials: true,
    });
  }

  saveState(request: SaveProgressStateRequest): Observable<ProgressOperationResponse> {
    return this.http.post<ProgressOperationResponse>(`${this.basePath}/state`, request, {
      withCredentials: true,
    });
  }

  state(exerciseId: number): Observable<ProgressStateResponse> {
    const params = new HttpParams().set('exerciseId', String(exerciseId));
    return this.http.get<ProgressStateResponse>(`${this.basePath}/state`, {
      params,
      withCredentials: true,
    });
  }

  summary(): Observable<ProgressSummaryResponse> {
    return this.http.get<ProgressSummaryResponse>(`${this.basePath}/summary`, {
      withCredentials: true,
    });
  }

  items(): Observable<ProgressItemResponse[]> {
    return this.http.get<ProgressItemResponse[]>(`${this.basePath}/items`, {
      withCredentials: true,
    });
  }

  attempts(exerciseId?: number): Observable<ProgressAttemptResponse[]> {
    const params = exerciseId === undefined
      ? undefined
      : new HttpParams().set('exerciseId', String(exerciseId));

    return this.http.get<ProgressAttemptResponse[]>(`${this.basePath}/attempts`, {
      params,
      withCredentials: true,
    });
  }
}
