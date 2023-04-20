import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { ListenAndWriteComponent } from './listen-and-write/listen-and-write.component';

const routes: Routes = [
  // load home component when the path is empty
  { path: '', component: HomeComponent },
  { path: 'listen-and-write', component: ListenAndWriteComponent },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
