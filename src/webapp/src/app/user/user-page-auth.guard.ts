import { isPlatformBrowser } from '@angular/common';
import { inject, PLATFORM_ID } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthSessionStore } from '../auth/services/auth-session.store';

export const userPageAuthGuard: CanActivateFn = async () => {
  const authSessionStore = inject(AuthSessionStore);
  const router = inject(Router);
  const platformId = inject(PLATFORM_ID);

  if (!isPlatformBrowser(platformId)) {
    return true;
  }

  if (authSessionStore.isAuthenticated()) {
    return true;
  }

  await authSessionStore.refreshSession();

  if (authSessionStore.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/auth/login']);
};
