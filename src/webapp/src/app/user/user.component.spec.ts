import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { UserProgressApiService } from './services/user-progress-api.service';
import { UserComponent } from './user.component';

describe('UserComponent', () => {
  let component: UserComponent;
  let fixture: ComponentFixture<UserComponent>;
  let userProgressApiSpy: jasmine.SpyObj<UserProgressApiService>;

  beforeEach(async () => {
    userProgressApiSpy = jasmine.createSpyObj<UserProgressApiService>('UserProgressApiService', ['summary', 'items']);

    userProgressApiSpy.summary.and.returnValue(of({
      trackingEnabled: true,
      totalItems: 2,
      inProgressCount: 1,
      completedCount: 1,
      totalAttempts: 3,
      totalActiveSeconds: 3665,
      averageAccuracyPercentage: 0.75,
      bestAccuracyPercentage: 0.9,
      lastActivityAtUtc: new Date().toISOString(),
    }));

    userProgressApiSpy.items.and.returnValue(of([
      {
        exerciseId: 10,
        status: 'completed',
        exerciseTitle: 'Exercise 10',
        subject: 'World',
        complexity: 'Medium',
        attemptCount: 2,
        latestAccuracyPercentage: 0.8,
        bestAccuracyPercentage: 0.9,
        activeSeconds: 420,
        startedAtUtc: new Date().toISOString(),
        completedAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString(),
      },
      {
        exerciseId: 11,
        status: 'in_progress',
        exerciseTitle: 'Exercise 11',
        subject: 'Tech',
        complexity: 'Hard',
        attemptCount: 1,
        latestAccuracyPercentage: null,
        bestAccuracyPercentage: null,
        activeSeconds: 95,
        startedAtUtc: new Date().toISOString(),
        completedAtUtc: null,
        updatedAtUtc: new Date().toISOString(),
      },
    ]));

    await TestBed.configureTestingModule({
      imports: [UserComponent],
      providers: [
        {
          provide: AuthSessionStore,
          useValue: {
            state: signal({
              isAuthenticated: true,
              userId: 'user-123',
              email: 'user@test.com',
              emailConfirmed: true,
              issuedAtUtc: new Date().toISOString(),
              expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
              isLoading: false,
              error: null,
            }),
          } as Pick<AuthSessionStore, 'state'>,
        },
        { provide: UserProgressApiService, useValue: userProgressApiSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render in-progress and completed statuses', () => {
    const root: HTMLElement = fixture.nativeElement;

    expect(root.textContent).toContain('Completed');
    expect(root.textContent).toContain('In progress');
    expect(root.textContent).toContain('user@test.com');
    expect(root.textContent).toContain('Total active time');
    expect(root.textContent).toContain('01:01:05');
    expect(root.textContent).toContain('Active so far');
    expect(root.textContent).toContain('Active time');
    expect(userProgressApiSpy.summary).toHaveBeenCalled();
    expect(userProgressApiSpy.items).toHaveBeenCalled();
  });
});
