import { Component, input, OnInit } from '@angular/core';
import { TextComparision } from '../entities/text-comparision';
import { TextPart } from '../entities/text-part';

@Component({
  selector: 'app-news-highlighted-text',
  imports: [],
  templateUrl: './news-highlighted-text.component.html',
  styleUrl: './news-highlighted-text.component.scss',
})
export class NewsHighlightedTextComponent implements OnInit {

  textComparisons = input<TextComparision[]>(null!);
  userText = input<string>('');
  originalText = input<string>('');

  textParts: TextPart[] = [];

  ngOnInit(): void {
    this.highlightUserText(this.textComparisons());
  }

  highlightUserText(comparisions: TextComparision[]) {
    let text = this.userText();   
    let lastEnd = 0;
    for (let comparision of comparisions) {
      let start = comparision.userTextRange.initialIndex ;
      let end = comparision.userTextRange.finalIndex + 1;
      let correction = comparision.originalText;
      this.textParts.push({ 
        text: text.slice(lastEnd, start), highlight: false });
      this.textParts.push({ text: text.slice(start, end), highlight: true, correction });
      lastEnd = end;
    }
    this.textParts.push({ text: text.slice(lastEnd), highlight: false });
  }

}
