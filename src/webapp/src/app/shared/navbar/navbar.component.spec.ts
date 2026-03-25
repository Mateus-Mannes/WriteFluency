import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

import { NavbarComponent } from './navbar.component';
import { AuthSessionStore } from '../../auth/services/auth-session.store';

describe('NavbarComponent', () => {
  let component: NavbarComponent;
  let fixture: ComponentFixture<NavbarComponent>;
  let authSessionStoreMock: AuthSessionStore;
  let logoutSpy: jasmine.Spy;

  beforeEach(async () => {
    logoutSpy = jasmine.createSpy('logout').and.returnValue(Promise.resolve());
    authSessionStoreMock = {
      logout: logoutSpy,
      isAuthenticated: signal(false),
      email: signal<string | null>(null),
      state: signal({
        isAuthenticated: false,
        userId: null,
        email: null,
        emailConfirmed: false,
        isLoading: false,
        error: null,
      }),
    } as unknown as AuthSessionStore;

    await TestBed.configureTestingModule({
      imports: [NavbarComponent],
      providers: [
        { provide: AuthSessionStore, useValue: authSessionStoreMock },
        {
          provide: ActivatedRoute,
          useValue: {
            params: of({}),
            queryParams: of({})
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NavbarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should call logout on auth store', async () => {
    await component.onLogout();
    expect(logoutSpy).toHaveBeenCalled();
  });
});
