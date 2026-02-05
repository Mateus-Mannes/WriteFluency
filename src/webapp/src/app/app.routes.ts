import { Routes } from '@angular/router';

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
        path: 'english-writing-exercise/:id',
        loadComponent: () =>
            import('./listen-and-write/listen-and-write.component').then((m) => m.ListenAndWriteComponent),
    },
];
