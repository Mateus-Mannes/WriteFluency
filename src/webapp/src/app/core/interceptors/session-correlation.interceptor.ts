import { Injectable } from '@angular/core';
import {
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest
} from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../enviroments/enviroment';
import { ExerciseSessionTrackingService } from '../../listen-and-write/services/exercise-session-tracking.service';
import { BrowserService } from '../services/browser.service';

@Injectable()
export class SessionCorrelationInterceptor implements HttpInterceptor {
  constructor(
    private exerciseSessionTracking: ExerciseSessionTrackingService,
    private browserService: BrowserService
  ) { }

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (!this.browserService.isBrowserEnvironment() || !this.isApiRequest(req.url)) {
      return next.handle(req);
    }

    const sessionId = this.exerciseSessionTracking.getCurrentSessionId();
    const operationId = this.exerciseSessionTracking.getCurrentOperationId();

    if (!sessionId && !operationId) {
      return next.handle(req);
    }

    return next.handle(req.clone({
      setHeaders: {
        ...(sessionId ? { 'x-wf-session-id': sessionId } : {}),
        ...(operationId ? { 'x-wf-operation-id': operationId } : {})
      }
    }));
  }

  private isApiRequest(url: string): boolean {
    const normalizedBase = environment.apiUrl.replace(/\/+$/, '');
    return url.startsWith(normalizedBase) || url.startsWith('/api/');
  }
}
