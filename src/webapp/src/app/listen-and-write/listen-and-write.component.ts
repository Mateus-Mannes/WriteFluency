import { Component, signal, ViewChild, HostListener, effect, OnDestroy, afterNextRender } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { NewsInfoComponent } from './news-info/news-info.component';
import { NewsImageComponent } from './news-image/news-image.component';
import { NewsAudioComponent } from './news-audio/news-audio.component';
import { ExerciseIntroSectionComponent } from './exercise-intro-section/exercise-intro-section.component';
import { ExerciseSectionComponent } from './exercise-section/exercise-section.component';
import { ExerciseResultsSectionComponent } from './exercise-results-section/exercise-results-section.component';
import { ListenFirstTourService } from './services/listen-first-tour.service';
import { ExerciseTourService } from './services/exercise-tour.service';
import { SubmitTourService } from './services/submit-tour.service';
import { NewsHighlightedTextComponent } from './news-highlighted-text/news-highlighted-text.component';
import { PropositionsService } from '../../api/listen-and-write/api/propositions.service';
import { Proposition } from '../../api/listen-and-write/model/proposition';
import { TextComparisonResult, TextComparisonsService } from 'src/api/listen-and-write';
import { environment } from 'src/enviroments/enviroment';
import { BrowserService } from '../core/services/browser.service';
import { SeoService } from '../core/services/seo.service';
import { ExerciseSessionTrackingService } from './services/exercise-session-tracking.service';
import { FeedbackService, ExerciseFeedbackEvent } from './services/feedback.service';
import {
  FeedbackModalComponent,
  FeedbackModalInteractionEvent,
  FeedbackModalSubmission
} from '../shared/feedback-modal/feedback-modal.component';

export type ExerciseState = 'intro' | 'exercise' | 'results';

export const LISTEN_WRITE_FIRST_TIME_KEY = 'listen-write-first-time';
const EXERCISE_SUBMIT_CONVERSION_SEND_TO = 'AW-17978787910/WruICPy4xoAcEMaQ-vxC';
type GtagEvent = (command: 'event', eventName: string, params: Record<string, unknown>) => void;
type AudioPlaySource = 'manual_click' | 'keyboard_shortcut' | 'listen_first_prompt';

@Component({
  selector: 'app-listen-and-write',
  imports: [
    NewsInfoComponent,
    NewsImageComponent,
    NewsAudioComponent,
    ExerciseIntroSectionComponent,
    ExerciseSectionComponent,
    ExerciseResultsSectionComponent,
    NewsHighlightedTextComponent,
    FeedbackModalComponent
  ],
  templateUrl: './listen-and-write.component.html',
  styleUrls: ['./listen-and-write.component.scss'],
})
export class ListenAndWriteComponent implements OnDestroy {

  @ViewChild(ExerciseSectionComponent) exerciseSectionComponent!: ExerciseSectionComponent;

  @ViewChild(NewsAudioComponent) newsAudioComponent!: NewsAudioComponent;

  private autoPauseTimer: any = null;
  private pendingAudioPlaySource: AudioPlaySource | null = null;
  private pendingAudioPlaySourceTimer: ReturnType<typeof setTimeout> | null = null;

  exerciseState = signal<ExerciseState>('intro');

  stateAnimOn = signal(false);
  stateAnimEnabled = signal(false);

  proposition = signal<Proposition | null>(null);

  result = signal<TextComparisonResult | null>(null);

  isSubmitting = signal<boolean>(false);

  initialText = signal<string | null>(null);
  
  initialAutoPause = signal<number | null>(null);

  userText = signal<string>('');

  isFeedbackModalOpen = signal<boolean>(false);

  exerciseId: number | null = null;

  private pendingLeaveAction: (() => void) | null = null;
  private pendingRouteLeaveResolver: ((allow: boolean) => void) | null = null;

  constructor(
    private listenFirstTourService: ListenFirstTourService,
    private exerciseTourService: ExerciseTourService,
    private submitTour: SubmitTourService,
    private route: ActivatedRoute,
    private router: Router,
    private propositionsService: PropositionsService,
    private textComparisonsService: TextComparisonsService,
    private browserService: BrowserService,
    private seoService: SeoService,
    private exerciseSessionTracking: ExerciseSessionTrackingService,
    private feedbackService: FeedbackService
  ) {
    let lastState: ExerciseState | null = null;
    // Get the exercise ID from route parameters
    this.route.params.pipe(
      takeUntilDestroyed()
    ).subscribe(params => {
      const id = params['id'];
      if (id) {
        const parsedId = Number(id);
        if (!Number.isFinite(parsedId)) {
          return;
        }

        this.exerciseSessionTracking.endSession('route_changed');
        this.exerciseId = parsedId;
        this.exerciseSessionTracking.startSession({
          exerciseId: this.exerciseId,
          source: 'route_navigation'
        });
        this.initialText.set(null);
        this.initialAutoPause.set(null);
        this.result.set(null);
        if(this.browserService.isBrowserEnvironment()) {
          const initialProposition = this.getInitialPropositionFromState();
          if (initialProposition && initialProposition.id === this.exerciseId) {
            this.proposition.set(initialProposition);
          } else {
            this.proposition.set(null);
          }
        }
        this.loadProposition(this.exerciseId);
      }
    });

    // Animate state changes
    effect(() => {
      const state = this.exerciseState();
      const animEnabled = this.stateAnimEnabled();
      if (lastState === null) {
        lastState = state;
        return;
      }
      if (!animEnabled) {
        lastState = state;
        return;
      }
      if (state === lastState) return;
      lastState = state;
      this.stateAnimOn.set(false);
      queueMicrotask(() => this.stateAnimOn.set(true));
    });

    afterNextRender(() => {
      this.restoreExerciseState();
      queueMicrotask(() => this.stateAnimEnabled.set(true));
    });
  }

  private getInitialPropositionFromState(): Proposition | null {
    const state = this.router.currentNavigation()?.extras?.state ?? history.state;
    const initialProposition = state?.initialProposition;
    if (!initialProposition || typeof initialProposition !== 'object') {
      return null;
    }
    const proposition = initialProposition as Proposition;
    proposition.publishedOn = initialProposition.date;
    proposition.newsInfo = { url: initialProposition.newsUrl };
    return proposition;
  }

  private getExerciseStateKey(): string | null {
    if (!this.exerciseId) return null;
    return `exercise-section-state-${this.exerciseId}`;
  }

  private getStateKey(): string | null {
    if (!this.exerciseId) return null;
    return `listen-write-state-${this.exerciseId}`;
  }

  loadProposition(id: number): void {
    this.propositionsService.apiPropositionIdGet(id).subscribe({
      next: (data) => {
        this.proposition.set(data);
        this.exerciseSessionTracking.updateSessionContext({
          exerciseId: id
        });
        this.exerciseSessionTracking.trackEvent('exercise_content_loaded', {
          title: data.title,
          subject: data.subject?.description ?? '',
          complexity: data.complexity?.description ?? ''
        }, {
          audio_duration_seconds: data.audioDurationSeconds ?? 0
        });
        
        // Update SEO meta tags for this specific exercise
        const complexityDesc = data.complexity?.description || 'Intermediate';
        const subjectDesc = data.subject?.description || 'News';
        const duration = data.audioDurationSeconds 
          ? `${Math.ceil(data.audioDurationSeconds / 60)} min`
          : '1-2 min';
        const exerciseImageUrl = this.getExerciseImageUrl(data.imageFileId);
        
        this.seoService.updateMetaTags({
          title: `${data.title} - ${complexityDesc} Level Exercise | WriteFluency`,
          description: `Practice your English writing with this ${complexityDesc.toLowerCase()} level listening exercise about ${subjectDesc}. Listen to real news audio and improve your dictation skills. Duration: ${duration}.`,
          keywords: `${subjectDesc}, ${complexityDesc} level, English writing exercise, listening comprehension, dictation practice`,
          type: 'article',
          url: `/english-writing-exercise/${id}`,
          image: exerciseImageUrl,
          publishedTime: data.publishedOn || undefined
        });

        // Add structured data for this exercise
        const exerciseData = this.seoService.generateExerciseStructuredData({
          id: id,
          title: data.title || 'English Writing Exercise',
          topic: subjectDesc,
          level: complexityDesc,
          duration: duration,
          imageUrl: exerciseImageUrl,
          description: `Practice your English writing with this ${complexityDesc.toLowerCase()} level listening exercise.`
        });

        // Add breadcrumb structured data
        const breadcrumbData = this.seoService.generateBreadcrumbStructuredData([
          { name: 'Home', url: '/' },
          { name: 'Exercises', url: '/exercises' },
          { name: data.title || 'Exercise', url: `/english-writing-exercise/${id}` }
        ]);

        // Combine structured data
        const structuredData = {
          '@context': 'https://schema.org',
          '@graph': [exerciseData, breadcrumbData]
        };
        this.seoService.addStructuredData(structuredData);
      },
      error: (error) => {
        console.error('Error loading proposition:', error);
        // notify the user and back to the home page
        alert('Error loading exercise. Returning to home page.');
        this.browserService.navigateTo('/');
      }
    });
  }

  private getExerciseImageUrl(imageFileId?: string | null): string | undefined {
    if (!imageFileId) {
      return undefined;
    }

    return `${environment.minioUrl}/images/${imageFileId}`;
  }

  restoreExerciseState() 
  {
    const stateKey = this.getStateKey();
    if (stateKey) {
      const storedState = this.browserService.getItem(stateKey);
      if (storedState) {
        this.exerciseState.set(storedState as ExerciseState);
      }
    }

    const exerciseStateKey = this.getExerciseStateKey();
    if (!exerciseStateKey) return;
    
    const state = this.browserService.getItem(exerciseStateKey);
    if (state) {
      const parsed = JSON.parse(state);

      this.initialText.set(parsed.userText || null);
      this.initialAutoPause.set(parsed.autoPause ?? 2);

      if (this.newsAudioComponent && parsed.pausedTime) {
        this.newsAudioComponent.forwardAudio(parsed.pausedTime);
      }

      this.result.set(parsed.result || null);
    }
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyboardEvent(event: KeyboardEvent) {
    if (this.exerciseState() !== 'exercise') return;
    
    const modifierKey = event.ctrlKey || event.metaKey;
    
    // Play/Pause: Ctrl+Enter (Windows/Linux) or Cmd+Enter (Mac)
    if (modifierKey && event.key === 'Enter') {
      event.preventDefault();
      this.exerciseSessionTracking.trackEvent('audio_shortcut_used', {
        shortcut: 'ctrl_or_cmd_enter',
        action: this.newsAudioComponent.isAudioPlaying() ? 'pause' : 'play'
      });
      if (this.newsAudioComponent.isAudioPlaying()) {
        this.pauseAudioWithTimerClear();
      } else {
        this.markNextAudioPlaySource('keyboard_shortcut');
        this.playAudioWithAutoPause();
        // Finish the tutorial if active
        this.exerciseTourService.finishTour();
      }
    }
    // Rewind: Ctrl+ArrowLeft (Windows/Linux) or Cmd+ArrowLeft (Mac)
    if (modifierKey && event.code === 'ArrowLeft') {
      const seekSeconds = this.getShortcutSeekSeconds();
      event.preventDefault();
      this.exerciseSessionTracking.trackEvent('audio_shortcut_used', {
        shortcut: 'ctrl_or_cmd_arrow_left',
        action: 'rewind'
      }, {
        seek_seconds: seekSeconds
      });
      this.newsAudioComponent.rewindAudio(seekSeconds);
    }
    // Forward: Ctrl+ArrowRight (Windows/Linux) or Cmd+ArrowRight (Mac)
    if (modifierKey && event.code === 'ArrowRight') {
      const seekSeconds = this.getShortcutSeekSeconds();
      event.preventDefault();
      this.exerciseSessionTracking.trackEvent('audio_shortcut_used', {
        shortcut: 'ctrl_or_cmd_arrow_right',
        action: 'forward'
      }, {
        seek_seconds: seekSeconds
      });
      this.newsAudioComponent.forwardAudio(seekSeconds);
    }
  }

  private getShortcutSeekSeconds(): number {
    const selectedAutoPause = this.exerciseSectionComponent?.selectedAutoPause?.() ?? 0;
    return selectedAutoPause > 0 ? selectedAutoPause : 3;
  }

  playAudioWithAutoPause() {
    this.newsAudioComponent.playAudio();
    this.applyAutoPause();
  }

  pauseAudioWithTimerClear() {
    this.newsAudioComponent.pauseAudio();
    this.clearAutoPauseTimer();
  }

  clearAutoPauseTimer() {
    if (this.autoPauseTimer) {
      clearTimeout(this.autoPauseTimer);
      this.autoPauseTimer = null;
    }
  }

  beginExercise() {
    const isFirstTimeUser = this.isFirstTime();
    this.exerciseSessionTracking.trackEvent('begin_exercise_clicked', {
      is_first_time: isFirstTimeUser,
      audio_ended_before_begin: this.newsAudioComponent.audioEnded
    });
    this.newsAudioComponent.pauseAudio();
    this.newsAudioComponent.audioRef.nativeElement.currentTime = 0;
    this.clearAutoPauseTimer();

    if (this.newsAudioComponent.audioEnded) {
      this.exerciseSessionTracking.trackEvent('exercise_started', {
        start_mode: 'audio_already_completed'
      });
      this.setNewState('exercise');
      this.browserService.scrollToTop();
      return;
    }

    if (isFirstTimeUser) {
      this.exerciseSessionTracking.trackEvent('listen_first_prompt_shown');
      this.listenFirstTourService.prompt(
        '#newsAudio',
        () => {
          this.markNextAudioPlaySource('listen_first_prompt');
          this.newsAudioComponent.playAudio();
        },
        () => {
          this.exerciseSessionTracking.trackEvent('exercise_started', {
            start_mode: 'skip_listen_first_prompt'
          });
          this.setNewState('exercise');
          this.browserService.scrollToTop();
        }
      );
    } else {
      this.exerciseSessionTracking.trackEvent('exercise_started', {
        start_mode: 'direct_start'
      });
      this.setNewState('exercise');
      this.browserService.scrollToTop();
    }
  }

  onAudioPlayClicked() {
    const playSource = this.consumePendingAudioPlaySource();
    this.exerciseSessionTracking.trackEvent('audio_play_clicked', {
      exercise_state: this.exerciseState(),
      play_source: playSource
    });
    this.cancelTour();
    
    // Apply auto-pause logic if in exercise state
    if (this.exerciseState() === 'exercise') {
      this.applyAutoPause();
    }
  }

  cancelTour() {
    this.listenFirstTourService.cancelTour();
  }

  startExerciseTour() {
    this.exerciseTourService.startTour();
  }

  applyAutoPause() {
    this.clearAutoPauseTimer();
    if (this.isMobileLayout()) return;
    // Remove focus from textarea when audio starts
    this.exerciseSectionComponent?.blurTextArea();
    // Native audio controls can keep focus and swallow shortcuts after a mouse click.
    this.browserService.blurActiveElement();
    // Get auto-pause duration from exercise section
    const duration = this.exerciseSectionComponent?.selectedAutoPause?.() ?? 0;
    if (duration > 0) {
      this.autoPauseTimer = setTimeout(() => {
        if (this.newsAudioComponent.isAudioPlaying()) {
          this.newsAudioComponent.pauseAudio();
        }
        this.clearAutoPauseTimer();
      }, duration * 1000);
    }
  }

  onExerciseSubmit() {
    const submitWarning = this.getSubmitWarningMessage();
    this.exerciseSessionTracking.trackEvent('exercise_submit_clicked', {
      has_submit_warning: Boolean(submitWarning)
    });

    if (submitWarning) {
      queueMicrotask(() => {
        this.submitTour.startRecommendationTour(submitWarning, () => this.submitExercise());
      });
      return;
    }

    this.submitExercise();
  }

  private submitExercise() {
    const userText = this.exerciseSectionComponent.text();
    this.newsAudioComponent.pauseAudio();
    this.isSubmitting.set(true);
    this.exerciseSessionTracking.markSubmitted();
    this.exerciseSessionTracking.trackTextChanged(userText);
    this.exerciseSessionTracking.trackEvent('exercise_submit_confirmed', {
      text_snapshot: userText.slice(0, 1200),
      text_truncated: userText.length > 1200
    }, {
      text_char_count: userText.length,
      text_word_count: this.countWords(userText)
    });
    const startTime = Date.now();
    const minLoadingTime = 2000; // 2 seconds minimum

    this.textComparisonsService.apiTextComparisonCompareTextsPost({
      originalText: this.proposition()!.text,
      userText: this.exerciseSectionComponent.text(),
      propositionId: this.proposition()?.id ?? null
    }).subscribe({
      next: (result: TextComparisonResult) => {
        const elapsed = Date.now() - startTime;
        const remainingTime = Math.max(0, minLoadingTime - elapsed);
        
        setTimeout(() => {
          this.trackExerciseSubmitConversion(result.accuracyPercentage);
          this.exerciseSessionTracking.trackEvent('exercise_submit_succeeded');
          this.userText.set(this.exerciseSectionComponent.text());
          this.result.set(result);
          this.onSaveExerciseState();
          this.setNewState('results');
          this.browserService.scrollToTop();
          this.isSubmitting.set(false);
        }, remainingTime);
      },
      error: (error) => {
        const elapsed = Date.now() - startTime;
        const remainingTime = Math.max(0, minLoadingTime - elapsed);
        
        setTimeout(() => {
          this.exerciseSessionTracking.trackEvent('exercise_submit_failed', {
            error: error?.message ?? 'unknown_error'
          });
          this.isSubmitting.set(false);
          alert('Error processing your exercise. Please try again.');
        }, remainingTime);
      }
    });
  }

  private trackExerciseSubmitConversion(accuracyPercentage: number | null | undefined): void {
    if (!Number.isFinite(accuracyPercentage) || (accuracyPercentage ?? 0) < 0.1) {
      return;
    }

    if (!this.browserService.isBrowserEnvironment()) {
      return;
    }

    const gtag = (globalThis as typeof globalThis & { gtag?: GtagEvent }).gtag;
    if (typeof gtag !== 'function') {
      return;
    }

    gtag('event', 'conversion', {
      send_to: EXERCISE_SUBMIT_CONVERSION_SEND_TO,
      value: 1.0,
      currency: 'BRL'
    });
  }

  private getSubmitWarningMessage(): string | null {
    const warnings: string[] = [];

    if (!this.hasCompletedAudioPlayback()) {
      warnings.push('We strongly recommend listening through the audio before submitting. You can skip words or write what you think you heard.');
      warnings.push('If you submit too early, the accuracy percentage can be less precise and you may lose points.');
      warnings.push('If auto-pause is enabled, press play again after each pause using Ctrl/Cmd + Enter.');
    }

    const minimumWords = this.getMinimumWordsForSubmit();
    const userWords = this.exerciseSectionComponent?.wordCount ?? 0;
    if (minimumWords > 0 && userWords < minimumWords) {
      warnings.push(`Your text is still short (${userWords} words). Partial answers can reduce correction accuracy.`);
    }

    if (warnings.length === 0) {
      return null;
    }

    return `Quick reminder before submitting:\n\n- ${warnings.join('\n\n- ')}`;
  }

  private hasCompletedAudioPlayback(): boolean {
    if (this.newsAudioComponent?.audioEnded) {
      return true;
    }

    const audio = this.newsAudioComponent?.audioRef?.nativeElement;
    if (!audio) {
      return false;
    }

    const duration = audio.duration;
    if (!Number.isFinite(duration) || duration <= 0) {
      return false;
    }

    return audio.currentTime >= Math.max(0, duration - 0.25);
  }

  private getMinimumWordsForSubmit(): number {
    const originalWords = this.countWords(this.proposition()?.text);
    if (originalWords === 0) {
      return 0;
    }

    return Math.max(3, Math.ceil(originalWords * 0.2));
  }

  private countWords(text: string | null | undefined): number {
    return (text || '').trim().split(/\s+/).filter(Boolean).length;
  }

  ngOnDestroy(): void {
    if (this.pendingRouteLeaveResolver) {
      this.pendingRouteLeaveResolver(true);
      this.pendingRouteLeaveResolver = null;
    }

    this.pendingLeaveAction = null;
    this.clearPendingAudioPlaySource();
    this.exerciseSessionTracking.endSession('component_destroyed');
    // Clean up timer to prevent memory leaks
    this.clearAutoPauseTimer();
  }

  canDeactivateFromRoute(): boolean | Promise<boolean> {
    if (!this.shouldPromptFeedbackOnLeave()) {
      return true;
    }

    if (!this.openFeedbackModalIfEligible()) {
      return true;
    }

    return new Promise<boolean>((resolve) => {
      this.pendingRouteLeaveResolver = resolve;
    });
  }

  onTryAgain() {
    this.exerciseSessionTracking.trackEvent('exercise_post_submit_click', {
      action: 'try_again'
    });
    this.attemptLeaveResults(() => this.resetExerciseForTryAgain());
  }

  onFindAnotherExercise() {
    this.exerciseSessionTracking.trackEvent('exercise_post_submit_click', {
      action: 'find_another_exercise'
    });
    this.attemptLeaveResults(() => this.navigateToExercises());
  }

  onAudioPaused() {
    this.onSaveExerciseState();
    if (this.exerciseState() !== 'exercise') return;
    // When the user pauses via native controls, restore focus so shortcuts keep working.
    this.exerciseSectionComponent?.focusTextArea();
  }

  onSaveExerciseState() {
    const stateKey = this.getExerciseStateKey();
    if (!stateKey) return;
    
    const state = {
      userText: this.exerciseSectionComponent?.text(),
      autoPause: this.exerciseSectionComponent?.selectedAutoPause(),
      pausedTime: this.newsAudioComponent?.audioRef?.nativeElement?.currentTime || 0,
      result: this.result()
    };

    this.browserService.setItem(stateKey, JSON.stringify(state));
  }

  onExerciseTextChanged(text: string) {
    this.exerciseSessionTracking.trackTextChanged(text);
  }

  setNewState(state: ExerciseState) {
    this.exerciseState.set(state);
    // set local storage state
    const stateKey = this.getStateKey();
    if (stateKey) {
      this.browserService.setItem(stateKey, state);
    }

    if(this.isFirstTime() && state === 'exercise') {
      if (this.isMobileLayout()) {
        this.exerciseTourService.startMobileTour();
      } else {
        this.exerciseTourService.startTour();
      }
    }
  }

  isFirstTime() : boolean {
    const firstTime = this.browserService.getItem(LISTEN_WRITE_FIRST_TIME_KEY);
    if (firstTime === null) {
      return true;
    }
    return false;
  }

  private isMobileLayout(): boolean {
    const width = this.browserService.getWindowWidth();
    return width > 0 && width <= 900;
  }

  private markNextAudioPlaySource(source: AudioPlaySource): void {
    this.clearPendingAudioPlaySource();
    this.pendingAudioPlaySource = source;
    this.pendingAudioPlaySourceTimer = setTimeout(() => {
      this.pendingAudioPlaySource = null;
      this.pendingAudioPlaySourceTimer = null;
    }, 2000);
  }

  private consumePendingAudioPlaySource(): AudioPlaySource {
    const source = this.pendingAudioPlaySource ?? 'manual_click';
    this.clearPendingAudioPlaySource();
    return source;
  }

  private clearPendingAudioPlaySource(): void {
    if (this.pendingAudioPlaySourceTimer) {
      clearTimeout(this.pendingAudioPlaySourceTimer);
      this.pendingAudioPlaySourceTimer = null;
    }
    this.pendingAudioPlaySource = null;
  }

  onFeedbackDismissed(_reason: 'not_now' | 'close'): void {
    this.exerciseSessionTracking.trackEvent('feedback_modal_dismissed', {
      reason: _reason
    });
    this.feedbackService.markDismissed();
    this.closeFeedbackModal();
    this.continuePendingLeaveFlow(true);
  }

  onFeedbackSubmitted(submission: FeedbackModalSubmission): void {
    const proposition = this.proposition();
    const feedbackEvent: ExerciseFeedbackEvent = {
      rating: submission.rating,
      tags: submission.tags,
      comment: submission.comment,
      exerciseId: String(proposition?.id ?? this.exerciseId ?? ''),
      difficulty: proposition?.complexity?.description ?? '',
      topic: proposition?.subject?.description ?? '',
      sessionId: this.exerciseSessionTracking.getCurrentSessionId() ?? '',
      timestamp: new Date().toISOString()
    };

    this.exerciseSessionTracking.trackEvent('feedback_modal_submitted', {
      rating: submission.rating,
      tags_count: submission.tags.length,
      has_comment: Boolean(submission.comment)
    }, {
      feedback_rating: submission.rating,
      feedback_tags_count: submission.tags.length,
      feedback_comment_length: submission.comment?.length ?? 0
    });

    this.feedbackService.submitFeedback(feedbackEvent);
  }

  onFeedbackInteraction(event: FeedbackModalInteractionEvent): void {
    this.exerciseSessionTracking.trackEvent('feedback_modal_interaction', {
      action: event.action,
      rating: event.rating,
      tag: event.tag,
      tag_selected: event.tagSelected,
      tags_count: event.tagsCount,
      has_comment: event.hasComment
    }, {
      feedback_comment_length: event.commentLength ?? 0
    });
  }

  onFeedbackClosedAfterSubmit(): void {
    this.exerciseSessionTracking.trackEvent('feedback_modal_closed_after_submit');
    this.closeFeedbackModal();
    this.continuePendingLeaveFlow(true);
  }

  onFeedbackFindAnotherExercise(): void {
    this.exerciseSessionTracking.trackEvent('feedback_modal_find_another_exercise_clicked');
    this.closeFeedbackModal();
    this.continuePendingLeaveFlow(false);
    this.navigateToExercises();
  }

  private attemptLeaveResults(action: () => void): void {
    if (!this.shouldPromptFeedbackOnLeave()) {
      action();
      return;
    }

    if (!this.openFeedbackModalIfEligible()) {
      action();
      return;
    }

    this.pendingLeaveAction = action;
  }

  private shouldPromptFeedbackOnLeave(): boolean {
    return this.exerciseState() === 'results' && this.feedbackService.shouldShowPrompt();
  }

  private openFeedbackModalIfEligible(): boolean {
    if (this.isFeedbackModalOpen()) {
      return true;
    }

    if (!this.feedbackService.consumePromptOpportunity()) {
      return false;
    }

    this.exerciseSessionTracking.trackEvent('feedback_modal_opened');
    this.isFeedbackModalOpen.set(true);
    return true;
  }

  private closeFeedbackModal(): void {
    this.isFeedbackModalOpen.set(false);
  }

  private continuePendingLeaveFlow(allowPendingAction: boolean): void {
    const routeResolver = this.pendingRouteLeaveResolver;
    this.pendingRouteLeaveResolver = null;

    if (routeResolver) {
      routeResolver(allowPendingAction);
      this.pendingLeaveAction = null;
      return;
    }

    const leaveAction = this.pendingLeaveAction;
    this.pendingLeaveAction = null;

    if (allowPendingAction) {
      leaveAction?.();
    }
  }

  private resetExerciseForTryAgain(): void {
    const stateKey = this.getExerciseStateKey();
    if (stateKey) {
      this.browserService.removeItem(stateKey);
    }

    this.initialText.set(null);
    this.initialAutoPause.set(null);
    this.result.set(null);
    this.newsAudioComponent.audioRef.nativeElement.currentTime = 0;
    this.setNewState('intro');
  }

  private navigateToExercises(): void {
    this.exerciseSessionTracking.endSession('navigate_to_exercises');
    this.router.navigate(['/exercises']);
  }
}
