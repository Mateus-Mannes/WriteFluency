import { Component, computed, input, output, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
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
  imports: [NgClass, RouterLink],
  templateUrl: './mistake-pattern-review.component.html',
  styleUrl: './mistake-pattern-review.component.scss',
})
export class MistakePatternReviewComponent {
  private static readonly maxTextPairSnippetLength = 84;

  result = input<TextComparisonResult | null>(null);
  activeComparisonIndex = input<number | null>(null);

  activeComparisonIndexChange = output<number | null>();
  pinnedComparisonIndexChange = output<number | null>();
  loginToUnlockClick = output<void>();
  upgradeToProClick = output<void>();

  readonly selectedTag = signal<string | null>(null);
  readonly mockRows: MistakePatternRow[] = [
    {
      comparisonIndex: -1,
      sourceComparisonIndex: -1,
      tags: ['word_boundary'],
      studentPhrase: '"Every day" (two words) means each day, while "everyday" (one word) is an adjective meaning ordinary.',
      originalText: 'Every day',
      userText: 'Everyday',
    },
    {
      comparisonIndex: -2,
      sourceComparisonIndex: -2,
      tags: ['spelling'],
      studentPhrase: '"Forward" is missing the final "d" in your version, so be sure to include all the letters of the word.',
      originalText: 'forward',
      userText: 'forwar',
    },
    {
      comparisonIndex: -3,
      sourceComparisonIndex: -3,
      tags: ['missing_or_extra_word'],
      studentPhrase: 'The original uses the full phrase "new ways," so leaving out "new" changes the meaning and makes the idea less specific.',
      originalText: 'new ways',
      userText: 'ways',
    },
  ];

  readonly isUsageLimitSkipped = computed(() =>
    this.result()?.mistakePatternStatus === 'skipped_usage_limit');

  readonly isLoginRequired = computed(() =>
    this.result()?.mistakePatternStatus === 'login_required_to_unlock_review');

  readonly isUpgradeRequired = computed(() =>
    this.result()?.mistakePatternStatus === 'upgrade_required_to_unlock_review');

  private readonly mistakePatternStatus = computed(() =>
    this.result()?.mistakePatternStatus ?? 'not_applicable');

  readonly shouldShowNoCorrections = computed(() =>
    (this.mistakePatternStatus() === 'generated'
      || this.mistakePatternStatus() === 'not_applicable')
    && this.rows().length === 0);

  readonly shouldShowNoUserText = computed(() =>
    this.mistakePatternStatus() === 'not_applicable'
    && this.result()?.userText !== undefined
    && !this.result()?.userText?.trim());

  readonly isPerfectResult = computed(() =>
    (this.result()?.accuracyPercentage ?? 0) >= 0.999);

  readonly shouldShowLockedMock = computed(() =>
    this.isLoginRequired() || this.isUpgradeRequired());

  readonly usageLimitMessage = computed(() =>
    this.result()?.mistakePatternMessage?.trim()
    || 'You reached today\'s Pro AI review limit. Your correction highlights are still available; only the AI mistake-pattern review is paused. You can use AI review again tomorrow.');

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
    if (comparisonIndex < 0) {
      return;
    }

    this.pinnedComparisonIndexChange.emit(comparisonIndex);
  }

  isActive(row: MistakePatternRow): boolean {
    return this.activeComparisonIndex() === row.comparisonIndex;
  }

  formatTag(tag: string): string {
    return tag.replaceAll('_', ' ');
  }

  formatTextPairSnippet(text: string): string {
    const normalizedText = text.replace(/\s+/g, ' ').trim();
    if (normalizedText.length <= MistakePatternReviewComponent.maxTextPairSnippetLength) {
      return normalizedText;
    }

    return `${normalizedText.slice(0, MistakePatternReviewComponent.maxTextPairSnippetLength - 1).trimEnd()}…`;
  }

  onLoginToUnlockClick(): void {
    this.loginToUnlockClick.emit();
  }

  onUpgradeToProClick(): void {
    this.upgradeToProClick.emit();
  }
}
