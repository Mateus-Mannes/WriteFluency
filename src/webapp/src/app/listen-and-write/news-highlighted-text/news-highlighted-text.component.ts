import { Component, input, OnInit, ViewEncapsulation } from '@angular/core';
import { TextComparision } from '../entities/text-comparision';
import { TextPart } from '../entities/text-part';
import { NgClass } from '@angular/common';
import {MatTooltipModule} from '@angular/material/tooltip';

export type TextType = 'original' | 'user';

@Component({
  selector: 'app-news-highlighted-text',
  imports: [NgClass, MatTooltipModule],
  templateUrl: './news-highlighted-text.component.html',
  styleUrl: './news-highlighted-text.component.scss',
  encapsulation: ViewEncapsulation.None,
})
export class NewsHighlightedTextComponent implements OnInit {

  textComparisons = input<TextComparision[]>(null!);
  userText = input<string>('');
  originalText = input<string>('');
  textType = input<TextType>('user');

  get tooltipClass(): string {
    return this.textType() === 'user' ? 'tooltip-user-text' : 'tooltip-original-text';
  }

  textParts: TextPart[] = [];

  get highlightClass(): string {
    return this.textType() === 'user' ? 'user-highlighted-text' : 'original-highlighted-text';
  }

  ngOnInit(): void {
    this.highlightUserText(this.textComparisons());
  }

  highlightUserText(comparisions: TextComparision[]) {
    let text = this.textType() === 'user' ? this.userText() : this.originalText();
    let lastEnd = 0;
    for (let comparision of comparisions) {
      let start = this.textType() === 'user' ? comparision.userTextRange.initialIndex : comparision.originalTextRange.initialIndex;
      let end = this.textType() === 'user' ? comparision.userTextRange.finalIndex + 1 : comparision.originalTextRange.finalIndex + 1;
      let correction = this.textType() === 'user' ? comparision.originalText : comparision.userText;
      this.textParts.push({ 
        text: text.slice(lastEnd, start), highlight: false });
      this.textParts.push({ text: text.slice(start, end), highlight: true, correction });
      lastEnd = end;
    }
    this.textParts.push({ text: text.slice(lastEnd), highlight: false });
  }

}
