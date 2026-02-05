import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { NavbarComponent } from './shared/navbar/navbar.component';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatIconRegistry } from '@angular/material/icon';
import { Insights } from '../telemetry/insights.service';

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
  private matIconRegistry = inject(MatIconRegistry);
  private insights = inject(Insights);

  constructor() {
    this.matIconRegistry.setDefaultFontSetClass('material-symbols-outlined');
    void this.insights;
  }
}
