import { Component, ChangeDetectionStrategy } from '@angular/core';
import { NavbarComponent } from './shared/navbar/navbar.component';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'app-root',
    standalone: true,
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    imports: [
      CommonModule,
      RouterOutlet,
      NavbarComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  // Root standalone component bootstrapped via `bootstrapApplication` in main.ts
}
