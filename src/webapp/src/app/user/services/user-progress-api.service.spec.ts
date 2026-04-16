import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../enviroments/enviroment';
import { UserProgressApiService } from './user-progress-api.service';
import { ProgressAttemptResponse, ProgressItemResponse, ProgressSummaryResponse } from '../models/user-progress.model';

describe('UserProgressApiService', () => {
  let service: UserProgressApiService;
  let httpMock: HttpTestingController;
  let progressBaseUrl: string;

  beforeEach(() => {
    progressBaseUrl = `${environment.usersProgressApiUrl.replace(/\/$/, '')}/users/progress`;

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [UserProgressApiService],
    });

    service = TestBed.inject(UserProgressApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should call start endpoint with cookies enabled', () => {
    service.start({ exerciseId: 10, exerciseTitle: 'Exercise 10' }).subscribe();

    const request = httpMock.expectOne(`${progressBaseUrl}/start`);
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({ trackingEnabled: true, exerciseId: 10, status: 'in_progress', updatedAtUtc: new Date().toISOString() });
  });

  it('should include exerciseId query parameter when listing attempts', () => {
    service.attempts(15).subscribe();

    const request = httpMock.expectOne((req) =>
      req.url === `${progressBaseUrl}/attempts` && req.params.get('exerciseId') === '15',
    );

    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBeTrue();
    request.flush([]);
  });

  it('should include exerciseId query parameter when requesting state', () => {
    service.state(21).subscribe();

    const request = httpMock.expectOne((req) =>
      req.url === `${progressBaseUrl}/state` && req.params.get('exerciseId') === '21',
    );

    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBeTrue();
    request.flush({
      trackingEnabled: true,
      exerciseId: 21,
      hasServerState: false,
      exerciseState: null,
      userText: null,
      wordCount: null,
      autoPauseSeconds: null,
      pausedTimeSeconds: null,
      updatedAtUtc: null,
    });
  });

  it('should deserialize summary with totalActiveSeconds', () => {
    let response: ProgressSummaryResponse | undefined;

    service.summary().subscribe((result) => {
      response = result;
    });

    const request = httpMock.expectOne(`${progressBaseUrl}/summary`);
    expect(request.request.method).toBe('GET');
    request.flush({
      trackingEnabled: true,
      totalItems: 1,
      inProgressCount: 1,
      completedCount: 0,
      totalAttempts: 2,
      totalActiveSeconds: 125,
      averageAccuracyPercentage: null,
      bestAccuracyPercentage: null,
      lastActivityAtUtc: new Date().toISOString(),
    });

    expect(response?.totalActiveSeconds).toBe(125);
  });

  it('should deserialize items with activeSeconds', () => {
    let response: ProgressItemResponse[] | undefined;

    service.items().subscribe((result) => {
      response = result;
    });

    const request = httpMock.expectOne(`${progressBaseUrl}/items`);
    expect(request.request.method).toBe('GET');
    request.flush([
      {
        exerciseId: 30,
        status: 'in_progress',
        exerciseTitle: 'Exercise 30',
        subject: 'Science',
        complexity: 'Medium',
        attemptCount: 1,
        latestAccuracyPercentage: null,
        bestAccuracyPercentage: null,
        activeSeconds: 45,
        startedAtUtc: new Date().toISOString(),
        completedAtUtc: null,
        updatedAtUtc: new Date().toISOString(),
        currentWordCount: 12,
      },
    ]);

    expect(response?.[0]?.activeSeconds).toBe(45);
  });

  it('should deserialize attempts with activeSeconds', () => {
    let response: ProgressAttemptResponse[] | undefined;

    service.attempts(30).subscribe((result) => {
      response = result;
    });

    const request = httpMock.expectOne((req) =>
      req.url === `${progressBaseUrl}/attempts` && req.params.get('exerciseId') === '30',
    );
    expect(request.request.method).toBe('GET');
    request.flush([
      {
        attemptId: 'attempt-1',
        exerciseId: 30,
        accuracyPercentage: 0.8,
        wordCount: 100,
        originalWordCount: 110,
        activeSeconds: 180,
        createdAtUtc: new Date().toISOString(),
        exerciseTitle: 'Exercise 30',
        subject: 'Science',
        complexity: 'Medium',
      },
    ]);

    expect(response?.[0]?.activeSeconds).toBe(180);
  });
});
