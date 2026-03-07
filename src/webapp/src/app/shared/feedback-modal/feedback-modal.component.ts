import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';

export interface FeedbackModalSubmission {
  rating: number;
  tags: string[];
  comment: string | null;
}

export interface FeedbackModalInteractionEvent {
  action:
    | 'opened'
    | 'rating_selected'
    | 'tag_toggled'
    | 'comment_blurred'
    | 'submit_clicked'
    | 'dismissed_not_now'
    | 'dismissed_close'
    | 'thanks_closed'
    | 'find_another_exercise_clicked';
  rating?: number;
  tag?: string;
  tagSelected?: boolean;
  tagsCount?: number;
  commentLength?: number;
  hasComment?: boolean;
}

interface FeedbackTagOption {
  value: string;
  label: string;
}

type FeedbackModalView = 'form' | 'thanks';

@Component({
  selector: 'app-feedback-modal',
  standalone: true,
  templateUrl: './feedback-modal.component.html',
  styleUrl: './feedback-modal.component.scss'
})
export class FeedbackModalComponent implements OnChanges {
  @Input() isOpen = false;

  @Output() dismissed = new EventEmitter<'not_now' | 'close'>();
  @Output() submitted = new EventEmitter<FeedbackModalSubmission>();
  @Output() closedAfterSubmit = new EventEmitter<void>();
  @Output() findAnotherExercise = new EventEmitter<void>();
  @Output() interaction = new EventEmitter<FeedbackModalInteractionEvent>();

  protected readonly tagOptions: FeedbackTagOption[] = [
    { value: 'audio_speed_issue', label: 'Audio was too fast or slow' },
    { value: 'audio_hard_to_understand', label: 'Hard to understand the audio' },
    { value: 'too_easy', label: 'Too easy' },
    { value: 'too_difficult', label: 'Too difficult' },
    { value: 'vocabulary_too_advanced', label: 'Vocabulary too advanced' },
    { value: 'corrections_seemed_wrong', label: 'The corrections seemed wrong' },
    { value: 'ui_confusing', label: 'Controls/UI were confusing' },
    { value: 'audio_controls_confusing', label: 'Audio controls were confusing' },
    { value: 'shortcuts_not_useful', label: 'Shortcuts were not useful' },
    { value: 'content_not_interesting', label: 'The content was not interesting' },
    { value: 'other', label: 'Other' }
  ];

  protected view: FeedbackModalView = 'form';
  protected rating = 0;
  protected comment = '';
  protected selectedTags = new Set<string>();

  ngOnChanges(changes: SimpleChanges): void {
    const isOpenChanged = changes['isOpen'];
    if (isOpenChanged?.currentValue && !isOpenChanged.previousValue) {
      this.resetState();
      this.interaction.emit({
        action: 'opened'
      });
    }
  }

  protected setRating(value: number): void {
    this.rating = value;
    this.interaction.emit({
      action: 'rating_selected',
      rating: value
    });
  }

  protected isTagSelected(tag: string): boolean {
    return this.selectedTags.has(tag);
  }

  protected onTagToggled(tag: string, event: Event): void {
    const target = event.target as HTMLInputElement | null;
    if (!target) {
      return;
    }

    if (target.checked) {
      this.selectedTags.add(tag);
    } else {
      this.selectedTags.delete(tag);
    }

    this.interaction.emit({
      action: 'tag_toggled',
      tag,
      tagSelected: target.checked,
      tagsCount: this.selectedTags.size
    });
  }

  protected onCommentChanged(event: Event): void {
    const target = event.target as HTMLTextAreaElement | null;
    this.comment = target?.value ?? '';
  }

  protected onCommentBlur(): void {
    const trimmedComment = this.comment.trim();
    this.interaction.emit({
      action: 'comment_blurred',
      commentLength: trimmedComment.length,
      hasComment: trimmedComment.length > 0
    });
  }

  protected dismissNotNow(): void {
    this.interaction.emit({
      action: 'dismissed_not_now'
    });
    this.dismissed.emit('not_now');
  }

  protected close(): void {
    if (this.view === 'thanks') {
      this.interaction.emit({
        action: 'thanks_closed'
      });
      this.closedAfterSubmit.emit();
      return;
    }

    this.interaction.emit({
      action: 'dismissed_close'
    });
    this.dismissed.emit('close');
  }

  protected submit(): void {
    if (this.rating < 1 || this.rating > 5) {
      return;
    }

    const trimmedComment = this.comment.trim();
    const tags = this.tagOptions
      .map((option) => option.value)
      .filter((tag) => this.selectedTags.has(tag));

    this.interaction.emit({
      action: 'submit_clicked',
      rating: this.rating,
      tagsCount: tags.length,
      hasComment: trimmedComment.length > 0,
      commentLength: trimmedComment.length
    });

    this.submitted.emit({
      rating: this.rating,
      tags,
      comment: trimmedComment.length > 0 ? trimmedComment : null
    });

    this.view = 'thanks';
  }

  protected onFindAnotherExercise(): void {
    this.interaction.emit({
      action: 'find_another_exercise_clicked'
    });
    this.findAnotherExercise.emit();
  }

  private resetState(): void {
    this.view = 'form';
    this.rating = 0;
    this.comment = '';
    this.selectedTags = new Set<string>();
  }
}
