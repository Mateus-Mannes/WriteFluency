import { signal } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { ComplexityEnum } from '../../../api/listen-and-write/model/complexityEnum';
import { ExerciseListItemDto } from '../../../api/listen-and-write/model/exerciseListItemDto';
import { ExerciseListItemDtoPagedResultDto } from '../../../api/listen-and-write/model/exerciseListItemDtoPagedResultDto';
import { SubjectEnum } from '../../../api/listen-and-write/model/subjectEnum';
import { AuthSessionState } from '../../auth/models/auth-session.model';
import { AuthSessionStore } from '../../auth/services/auth-session.store';
import { PropositionsService } from '../../../api/listen-and-write/api/propositions.service';
import { ExerciseGridComponent } from './exercise-grid.component';

describe('ExerciseGridComponent', () => {
  let component: ExerciseGridComponent;
  let fixture: ComponentFixture<ExerciseGridComponent>;
  let propositionsServiceSpy: jasmine.SpyObj<PropositionsService>;
  let routerSpy: jasmine.SpyObj<Router>;
  let authSessionStoreMock: Pick<AuthSessionStore, 'state' | 'refreshSession'>;
  let authStateSignal: ReturnType<typeof signal<AuthSessionState>>;
  let queryParams: Record<string, unknown>;

  beforeEach(async () => {
    queryParams = {};
    authStateSignal = signal<AuthSessionState>({
      isAuthenticated: false,
      userId: null,
      email: null,
      emailConfirmed: false,
      listenWriteTutorialCompleted: null,
      plan: 'free',
      entitlementStatus: 'free',
      isPro: false,
      currentPeriodEndUtc: null,
      cancelAtPeriodEnd: false,
      hasReliableSessionState: true,
      issuedAtUtc: null,
      expiresAtUtc: null,
      isLoading: false,
      error: null,
    });
    authSessionStoreMock = {
      state: authStateSignal.asReadonly(),
      refreshSession: jasmine.createSpy('refreshSession').and.resolveTo(),
    };
    propositionsServiceSpy = jasmine.createSpyObj<PropositionsService>('PropositionsService', ['apiPropositionExercisesGet']);
    const emptyResponse: ExerciseListItemDtoPagedResultDto = {
      items: [],
      totalCount: 0,
      pageNumber: 1,
      pageSize: 18
    };
    propositionsServiceSpy.apiPropositionExercisesGet.and.returnValue(of(emptyResponse) as any);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate', 'createUrlTree', 'serializeUrl']);
    routerSpy.createUrlTree.and.returnValue({} as any);
    routerSpy.serializeUrl.and.returnValue('/english-writing-exercise/1');
    (routerSpy as any).url = '/exercises?topic=Business&page=2';
    (routerSpy as any).events = of();

    await TestBed.configureTestingModule({
      imports: [ExerciseGridComponent],
      providers: [
        provideNoopAnimations(),
        { provide: PropositionsService, useValue: propositionsServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: AuthSessionStore, useValue: authSessionStoreMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              get queryParams() {
                return queryParams;
              }
            }
          }
        }
      ],
    }).compileComponents();
  });

  function exerciseItem(overrides: Partial<ExerciseListItemDto> = {}): ExerciseListItemDto {
    return {
      id: 1,
      title: 'Global exercise',
      topic: SubjectEnum.Business,
      level: ComplexityEnum.Beginner,
      publishedOn: '2026-05-01T10:00:00Z',
      imageFileId: null,
      audioDurationSeconds: 60,
      newsUrl: 'https://example.com/news',
      requiresPro: false,
      ...overrides,
    };
  }

  function setExercises(items: ExerciseListItemDto[]): void {
    propositionsServiceSpy.apiPropositionExercisesGet.and.returnValue(of({
      items,
      totalCount: items.length,
      pageNumber: 1,
      pageSize: 18,
    }) as any);
  }

  function createComponent(): void {
    fixture = TestBed.createComponent(ExerciseGridComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('should restore search query from URL and send it to the exercises API', () => {
    queryParams = { q: 'climate', page: '1' };

    createComponent();

    expect(component.searchInput()).toBe('climate');
    expect(component.searchText()).toBe('climate');
    expect(propositionsServiceSpy.apiPropositionExercisesGet).toHaveBeenCalledWith(
      undefined,
      undefined,
      2,
      18,
      'newest',
      'climate'
    );
  });

  it('should debounce search input, reset to first page, and preserve query in URL', fakeAsync(() => {
    queryParams = { page: '3' };
    createComponent();
    propositionsServiceSpy.apiPropositionExercisesGet.calls.reset();
    routerSpy.navigate.calls.reset();

    component.onSearchInputChange('  ocean energy  ');
    tick(299);

    expect(propositionsServiceSpy.apiPropositionExercisesGet).not.toHaveBeenCalled();

    tick(1);
    fixture.detectChanges();

    expect(component.searchText()).toBe('ocean energy');
    expect(component.pageIndex()).toBe(0);
    expect(propositionsServiceSpy.apiPropositionExercisesGet).toHaveBeenCalledWith(
      undefined,
      undefined,
      1,
      18,
      'newest',
      'ocean energy'
    );
    expect(routerSpy.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({
      queryParams: { q: 'ocean energy' },
      replaceUrl: true
    }));
  }));

  it('should clear search with clear filters', fakeAsync(() => {
    queryParams = { q: 'climate', page: '2' };
    createComponent();

    component.clearFilters();
    tick(300);

    expect(component.searchInput()).toBe('');
    expect(component.searchText()).toBe('');
    expect(component.pageIndex()).toBe(0);
    expect(component.pageSize()).toBe(18);
  }));

  it('should render a Pro badge and open upgrade modal instead of navigating for anonymous restricted cards', () => {
    setExercises([exerciseItem({ requiresPro: true })]);
    createComponent();
    routerSpy.navigate.calls.reset();

    const restrictedButton: HTMLButtonElement = fixture.nativeElement.querySelector('.exercise-card-button');
    restrictedButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.pro-badge mat-icon')?.textContent).toContain('workspace_premium');
    expect(fixture.nativeElement.querySelector('.pro-modal')).not.toBeNull();
    expect(routerSpy.navigate).not.toHaveBeenCalled();
  });

  it('should let Pro users open API-marked Pro cards normally', () => {
    authStateSignal.set({
      ...authStateSignal(),
      isAuthenticated: true,
      plan: 'pro',
      entitlementStatus: 'pro_active',
      isPro: true,
    });
    setExercises([exerciseItem({ requiresPro: true })]);

    createComponent();

    expect(fixture.nativeElement.querySelector('.exercise-card-button')).toBeNull();
    const playableLink: HTMLAnchorElement = fixture.nativeElement.querySelector('a.exercise-card-link');
    expect(playableLink).not.toBeNull();
  });

  it('should send users to the plans page from the modal CTA', fakeAsync(() => {
    authStateSignal.set({
      ...authStateSignal(),
      isAuthenticated: true,
      userId: 'user-1',
      email: 'free@example.com',
    });
    setExercises([exerciseItem({ requiresPro: true })]);
    createComponent();

    component.openProUpgradeModal(component.exercises()[0]);
    fixture.detectChanges();
    component.viewAvailablePlans();
    tick();

    expect(routerSpy.navigate).toHaveBeenCalledWith(['/plans'], {
      queryParams: {
        source: 'pro_catalog_modal',
      },
    });
  }));
});
