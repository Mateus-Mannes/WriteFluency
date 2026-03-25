import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { CallbackComponent } from './callback.component';
import { AuthSessionStore } from '../services/auth-session.store';

describe('CallbackComponent', () => {
  let component: CallbackComponent;
  let fixture: ComponentFixture<CallbackComponent>;
  let authSessionStoreSpy: jasmine.SpyObj<AuthSessionStore>;
  let routerSpy: jasmine.SpyObj<Router>;

  async function setupWithQuery(query: Record<string, string>): Promise<void> {
    authSessionStoreSpy = jasmine.createSpyObj<AuthSessionStore>('AuthSessionStore', ['refreshSession']);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    authSessionStoreSpy.refreshSession.and.returnValue(Promise.resolve());
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [CallbackComponent],
      providers: [
        { provide: AuthSessionStore, useValue: authSessionStoreSpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {
                get: (key: string) => query[key] ?? null,
              },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CallbackComponent);
    component = fixture.componentInstance;
  }

  it('should refresh session and navigate on success callback', async () => {
    await setupWithQuery({ auth: 'success', provider: 'google' });

    await component.ngOnInit();

    expect(authSessionStoreSpy.refreshSession).toHaveBeenCalled();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/']);
    expect(component.isSuccess()).toBeTrue();
  });

  it('should render error message on callback failure', async () => {
    await setupWithQuery({ auth: 'error', provider: 'google', code: 'invalid_state' });

    await component.ngOnInit();

    expect(component.isSuccess()).toBeFalse();
    expect(component.message()).toContain('invalid or expired');
  });
});
