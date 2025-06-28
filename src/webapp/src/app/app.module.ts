import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { AlertService } from './shared/services/alert-service';
import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { SharedModule } from './shared/shared.module';
import { HomeComponent } from './home/home.component';
import { ListenAndWriteModule } from './listen-and-write/listen-and-write.module';
import { HttpClientModule } from '@angular/common/http';

@NgModule({
  declarations: [
    AppComponent,
    HomeComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    SharedModule,
    ListenAndWriteModule,
    HttpClientModule
  ],
  providers: [AlertService],
  bootstrap: [AppComponent]
})
export class AppModule { }
