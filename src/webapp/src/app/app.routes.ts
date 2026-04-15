import { Routes } from '@angular/router';
import { listenAndWriteFeedbackGuard } from './listen-and-write/listen-and-write-feedback.guard';
import { userPageAuthGuard } from './user/user-page-auth.guard';

export const appRoutes: Routes = [
    {
        path: '',
        loadComponent: () =>
            import('./home/home.component').then((m) => m.HomeComponent),
    },
    {
        path: 'exercises',
        loadComponent: () =>
            import('./exercises/exercises.component').then((m) => m.ExercisesComponent),
    },
    {
        path: 'about',
        loadComponent: () =>
            import('./about/about.component').then((m) => m.AboutComponent),
    },
    {
        path: 'user',
        canActivate: [userPageAuthGuard],
        loadComponent: () =>
            import('./user/user.component').then((m) => m.UserComponent),
    },
    {
        path: 'auth/login',
        loadComponent: () =>
            import('./auth/login/login.component').then((m) => m.LoginComponent),
    },
    {
        path: 'auth/register',
        redirectTo: 'auth/login',
        pathMatch: 'full',
    },
    {
        path: 'auth/confirm-email',
        loadComponent: () =>
            import('./auth/confirm-email/confirm-email.component').then((m) => m.ConfirmEmailComponent),
    },
    {
        path: 'auth/callback',
        loadComponent: () =>
            import('./auth/callback/callback.component').then((m) => m.CallbackComponent),
    },
    {
        path: 'english-writing-exercise/:id',
        canDeactivate: [listenAndWriteFeedbackGuard],
        loadComponent: () =>
            import('./listen-and-write/listen-and-write.component').then((m) => m.ListenAndWriteComponent),
    },
];
