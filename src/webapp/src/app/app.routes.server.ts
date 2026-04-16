import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    path: 'auth/login',
    renderMode: RenderMode.Client
  },
  {
    path: 'auth/confirm-email',
    renderMode: RenderMode.Client
  },
  {
    path: 'auth/callback',
    renderMode: RenderMode.Client
  },
  {
    path: 'user',
    renderMode: RenderMode.Client
  },
  {
    path: '**',
    renderMode: RenderMode.Server
  }
];
