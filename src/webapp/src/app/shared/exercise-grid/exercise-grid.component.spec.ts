import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { ExerciseListItemDtoPagedResultDto } from '../../../api/listen-and-write/model/exerciseListItemDtoPagedResultDto';
import { PropositionsService } from '../../../api/listen-and-write/api/propositions.service';
import { ExerciseGridComponent } from './exercise-grid.component';

describe('ExerciseGridComponent', () => {
  let component: ExerciseGridComponent;
  let fixture: ComponentFixture<ExerciseGridComponent>;
  let propositionsServiceSpy: jasmine.SpyObj<PropositionsService>;
  let routerSpy: jasmine.SpyObj<Router>;
  let queryParams: Record<string, unknown>;

  beforeEach(async () => {
    queryParams = {};
    propositionsServiceSpy = jasmine.createSpyObj<PropositionsService>('PropositionsService', ['apiPropositionExercisesGet']);
    const emptyResponse: ExerciseListItemDtoPagedResultDto = {
      items: [],
      totalCount: 0,
      pageNumber: 1,
      pageSize: 18
    };
    propositionsServiceSpy.apiPropositionExercisesGet.and.returnValue(of(emptyResponse) as any);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [ExerciseGridComponent],
      providers: [
        provideNoopAnimations(),
        { provide: PropositionsService, useValue: propositionsServiceSpy },
        { provide: Router, useValue: routerSpy },
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
});
