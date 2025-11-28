import { Component } from '@angular/core';
import { NavbarComponent } from './shared/navbar/navbar.component';
import { RouterOutlet } from '@angular/router';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.css'],
    imports: [
      RouterOutlet,
      NavbarComponent, 
    ],
})
export class AppComponent {
  title = 'WriteFluencyApp';
}
