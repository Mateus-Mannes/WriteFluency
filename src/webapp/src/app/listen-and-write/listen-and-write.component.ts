import { Component } from '@angular/core';
import { NewsInfoComponent } from './news-info/news-info.component';
import { NewsImageComponent } from './news-image/news-image.component';

@Component({
  selector: 'app-listen-and-write',
  imports: [ NewsInfoComponent, NewsImageComponent ],
  templateUrl: './listen-and-write.component.html',
  styleUrl: './listen-and-write.component.scss',
})
export class ListenAndWriteComponent {

}
