import { Routes } from '@angular/router';
import { ListenAndWriteComponent } from './listen-and-write/listen-and-write.component';
import { HomeComponent } from './home/home.component';
import { ExercisesComponent } from './exercises/exercises.component';

export const appRoutes: Routes = [
    { path: '', component: HomeComponent },
    { path: 'exercises', component: ExercisesComponent },
    { path: 'listen-and-write/:id', component: ListenAndWriteComponent },
];
