import { NgOptimizedImage } from '@angular/common';
import { Component } from '@angular/core';

@Component({
  selector: 'app-news-image',
  imports: [ NgOptimizedImage ],
  templateUrl: './news-image.component.html',
  styleUrl: './news-image.component.scss',
})
export class NewsImageComponent {

}
