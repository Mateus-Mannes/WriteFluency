import { Component, computed, input, output, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { TextComparison, TextComparisonResult } from 'src/api/listen-and-write';

interface MistakePatternRow {
  comparisonIndex: number;
  sourceComparisonIndex: number;
  tags: string[];
  studentPhrase: string;
  originalText: string;
  userText: string;
}

@Component({
  selector: 'app-mistake-pattern-review',
  imports: [NgClass],
  templateUrl: './mistake-pattern-review.component.html',
  styleUrl: './mistake-pattern-review.component.scss',
})
export class MistakePatternReviewComponent {
  result = input<TextComparisonResult | null>(null);
  activeComparisonIndex = input<number | null>(null);

  activeComparisonIndexChange = output<number | null>();
  pinnedComparisonIndexChange = output<number | null>();

  readonly selectedTag = signal<string | null>(null);

  readonly rows = computed<MistakePatternRow[]>(() => {
    const result = this.result();
    if (!result?.comparisons?.length) {
      return [];
    }

    return result.comparisons
      .map((comparison, comparisonIndex) => {
        const sourceComparisonIndex = comparison.sourceComparisonIndex ?? comparisonIndex;
        const tags = (comparison.mistakePatternTags ?? [])
          .map(tag => tag?.trim())
          .filter((tag): tag is string => Boolean(tag));
        const studentPhrase = comparison.mistakePatternPhrase?.trim();
        if (tags.length === 0 || !studentPhrase) {
          return null;
        }

        return {
          comparisonIndex,
          sourceComparisonIndex,
          tags: tags.slice(0, 3),
          studentPhrase,
          originalText: comparison.originalText ?? '',
          userText: comparison.userText ?? '',
        };
      })
      .filter((row): row is MistakePatternRow => row !== null);
  });

  readonly tagCounts = computed(() => {
    const counts = new Map<string, number>();
    for (const row of this.rows()) {
      for (const tag of row.tags) {
        counts.set(tag, (counts.get(tag) ?? 0) + 1);
      }
    }

    return Array.from(counts.entries())
      .map(([tag, count]) => ({ tag, count }))
      .sort((first, second) => first.tag.localeCompare(second.tag));
  });

  readonly filteredRows = computed(() => {
    const selectedTag = this.selectedTag();
    if (!selectedTag) {
      return this.rows();
    }

    return this.rows().filter(row => row.tags.includes(selectedTag));
  });

  selectTag(tag: string | null): void {
    this.selectedTag.set(this.selectedTag() === tag ? null : tag);
  }

  setActiveComparison(comparisonIndex: number | null): void {
    this.activeComparisonIndexChange.emit(comparisonIndex);
  }

  togglePinnedComparison(comparisonIndex: number): void {
    this.pinnedComparisonIndexChange.emit(comparisonIndex);
  }

  isActive(row: MistakePatternRow): boolean {
    return this.activeComparisonIndex() === row.comparisonIndex;
  }

  formatTag(tag: string): string {
    return tag.replaceAll('_', ' ');
  }
}
