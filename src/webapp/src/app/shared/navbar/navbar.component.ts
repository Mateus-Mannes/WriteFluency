import { Component, ChangeDetectionStrategy } from '@angular/core';
import { MatToolbarModule } from '@angular/material/toolbar';
import {MatIconModule} from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { CommonModule } from '@angular/common';

@Component({
        selector: 'app-navbar',
        standalone: true,
        templateUrl: './navbar.component.html',
        styleUrls: ['./navbar.component.scss'],
        imports: [ CommonModule, MatToolbarModule, MatIconModule, MatButtonModule ],
        changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavbarComponent {

}
