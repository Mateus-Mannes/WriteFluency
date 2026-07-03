import { Component, computed, input, OnInit, ViewEncapsulation } from '@angular/core';
import { NgClass } from '@angular/common';
import {MatTooltipModule} from '@angular/material/tooltip';
import { TextComparison, TextComparisonResult } from 'src/api/listen-and-write/model/models';

export type TextType = 'original' | 'user';

export interface TextPart {
  text: string;
  highlight: boolean;
  correction?: string;
  comparisonIndex?: number;
}

@Component({
  selector: 'app-news-highlighted-text',
  imports: [NgClass, MatTooltipModule],
  templateUrl: './news-highlighted-text.component.html',
  styleUrl: './news-highlighted-text.component.scss',
  encapsulation: ViewEncapsulation.None,
})
export class NewsHighlightedTextComponent implements OnInit {

  result = input<TextComparisonResult | null>(null);
  textType = input<TextType>('user');
  activeComparisonIndex = input<number | null>(null);

  get tooltipClass(): string {
    return this.textType() === 'user' ? 'tooltip-user-text' : 'tooltip-original-text';
  }

  textParts = computed<TextPart[]>(() => {
    if (!this.result()) {
      return [];
    }
    return this.generateTextParts(this.result()!.comparisons || []);
  });

  get highlightClass(): string {
    return this.textType() === 'user' ? 'user-highlighted-text' : 'original-highlighted-text';
  }

  ngOnInit(): void { }

  generateTextParts(comparisons: TextComparison[]) {
    let text = this.textType() === 'user' ? this.result()?.userText || '' : this.result()?.originalText || '';
    let lastEnd = 0;
    let parts = []
    for (let comparisonIndex = 0; comparisonIndex < comparisons.length; comparisonIndex++) {
      let comparison = comparisons[comparisonIndex];
      let start = this.textType() === 'user' ? comparison.userTextRange!.initialIndex : comparison.originalTextRange!.initialIndex;
      let end = this.textType() === 'user' ? comparison.userTextRange!.finalIndex! + 1 : comparison.originalTextRange!.finalIndex! + 1;
      let correction = this.textType() === 'user' ? comparison.originalText : comparison.userText;
      parts.push({ 
        text: text.slice(lastEnd, start), highlight: false });
      parts.push({
        text: text.slice(start, end),
        highlight: true,
        correction: correction || '',
        comparisonIndex
      });
      lastEnd = end;
    }
    parts.push({ text: text.slice(lastEnd), highlight: false });
    return parts;
  }

  isActivePart(part: TextPart): boolean {
    return part.highlight
      && part.comparisonIndex !== undefined
      && part.comparisonIndex === this.activeComparisonIndex();
  }

}
