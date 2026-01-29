import { CommonModule, NgOptimizedImage } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { Proposition } from 'src/api/listen-and-write';
import { environment } from 'src/enviroments/enviroment';

@Component({
  selector: 'app-news-image',
  imports: [ NgOptimizedImage, CommonModule ],
  templateUrl: './news-image.component.html',
  styleUrl: './news-image.component.scss',
})
export class NewsImageComponent {

  proposition = input<Proposition | null>();

  imageUrl = computed(() => {
    if (!this.proposition()) {
      return '';
    }
    return environment.minioUrl + '/images/' + this.proposition()?.imageFileId;
  });

}
