import { Component, signal, ViewChild, HostListener, effect, OnDestroy, Optional, afterNextRender, computed, inject } from '@angular/core';
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
import { TextComparisonResult } from 'src/api/listen-and-write';
import { BrowserService } from '../core/services/browser.service';
import { ExerciseSessionTrackingService } from './services/exercise-session-tracking.service';
import { ExerciseProgressTrackingService, ProgressSyncNotification } from './services/exercise-progress-tracking.service';
import { ExerciseAudioAccessService, type AudioAccessCallbacks } from './services/exercise-audio-access.service';
import { ExerciseAudioControllerService } from './services/exercise-audio-controller.service';
import { ExerciseFeedbackFlowService } from './services/exercise-feedback-flow.service';
import { ExerciseSeoService } from './services/exercise-seo.service';
import { ExerciseStateRestoreService } from './services/exercise-state-restore.service';
import { ExerciseSubmissionService, type SubmitAudioState } from './services/exercise-submission.service';
import { GuestExerciseLoginPromptService } from './services/guest-exercise-login-prompt.service';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { Insights } from '../../telemetry/insights.service';
import * as feedbackModal from '../shared/feedback-modal/feedback-modal.component';
import * as tutorialVideoModal from '../shared/tutorial-video-modal/tutorial-video-modal.component';
import * as tutorialVideoConfig from '../core/config/tutorial-video.config';
import * as constants from './listen-and-write.constants';
import * as models from './listen-and-write.models';

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
    feedbackModal.FeedbackModalComponent,
    tutorialVideoModal.TutorialVideoModalComponent
  ],
  templateUrl: './listen-and-write.component.html',
  styleUrls: ['./listen-and-write.component.scss'],
  providers: [
    ExerciseAudioAccessService,
    ExerciseAudioControllerService,
    ExerciseFeedbackFlowService,
    ExerciseSeoService,
    ExerciseStateRestoreService,
    ExerciseSubmissionService,
    GuestExerciseLoginPromptService,
  ],
})
export class ListenAndWriteComponent implements OnDestroy {
  private readonly exerciseAudioAccess = inject(ExerciseAudioAccessService);
  private readonly exerciseAudioController = inject(ExerciseAudioControllerService);
  private readonly exerciseFeedbackFlow = inject(ExerciseFeedbackFlowService);
  private readonly exerciseSeo = inject(ExerciseSeoService);
  private readonly exerciseStateRestore = inject(ExerciseStateRestoreService);
  private readonly exerciseSubmission = inject(ExerciseSubmissionService);
  private readonly guestExerciseLoginPrompt = inject(GuestExerciseLoginPromptService);

  @ViewChild(ExerciseSectionComponent) exerciseSectionComponent!: ExerciseSectionComponent;

  private newsAudioComponentRef: NewsAudioComponent | null = null;
  private hasCompletedInitialBrowserRender = false;

  @ViewChild(NewsAudioComponent)
  set newsAudioComponent(component: NewsAudioComponent | undefined) {
    this.newsAudioComponentRef = component ?? null;
    this.exerciseAudioController.applyPendingPausedTimeIfNeeded(this.newsAudioComponentRef);
  }

  get newsAudioComponent(): NewsAudioComponent {
    return this.newsAudioComponentRef as NewsAudioComponent;
  }

  exerciseState = signal<models.ExerciseState>('intro');

  stateAnimOn = signal(false);
  stateAnimEnabled = signal(false);

  proposition = signal<Proposition | null>(null);
  exerciseAudioUrl = this.exerciseAudioAccess.audioUrl;

  result = signal<TextComparisonResult | null>(null);

  isSubmitting = this.exerciseSubmission.isSubmitting;
  isBeginningExercise = this.exerciseAudioAccess.isBeginningExercise;
  isResolvingAudioAccess = this.exerciseAudioAccess.isResolving;
  shouldShowAudioPanel = computed(() =>
    Boolean(this.exerciseAudioUrl())
    || this.isResolvingAudioAccess()
    || (this.exerciseState() === 'intro' && Boolean(this.proposition())));
  isRestoringExercise = this.exerciseStateRestore.isRestoring;
  isGuestLoginModalOpen = this.guestExerciseLoginPrompt.isOpen;
  isProUpgradeModalOpen = signal<boolean>(false);

  initialText = signal<string | null>(null);
  
  initialAutoPause = signal<number | null>(null);

  userText = signal<string>('');

  isFeedbackModalOpen = this.exerciseFeedbackFlow.isModalOpen;
  isTutorialVideoModalOpen = signal<boolean>(false);

  exerciseId: number | null = null;
  readonly tutorialVideoEmbedUrl = tutorialVideoConfig.tutorialVideoConfig.embedUrl;
  readonly tutorialVideoWatchUrl = tutorialVideoConfig.tutorialVideoConfig.watchUrl;
  readonly tutorialVideoTitle = tutorialVideoConfig.tutorialVideoConfig.title;

  private hasTrackedResultsLoginCtaView = false;
  private exerciseStartedAtMs: number | null = null;
  private shouldResetCompletedStateOnNextStart = false;
  private hasLoggedTutorialSuppressedException = false;
  private readonly tutorialBackfillRequestedUsers = new Set<string>();
  private tutorialVideoSource: tutorialVideoConfig.TutorialVideoSource | null = null;
  private hasHydrated = false;

  constructor(
    private listenFirstTourService: ListenFirstTourService,
    private exerciseTourService: ExerciseTourService,
    private submitTour: SubmitTourService,
    private route: ActivatedRoute,
    private router: Router,
    private propositionsService: PropositionsService,
    private browserService: BrowserService,
    private exerciseSessionTracking: ExerciseSessionTrackingService,
    private exerciseProgressTracking: ExerciseProgressTrackingService,
    private authSessionStore: AuthSessionStore,
    @Optional() private readonly insights: Insights | null = null,
  ) {
    let lastState: models.ExerciseState | null = null;
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
        this.guestExerciseLoginPrompt.reset();
        this.exerciseSessionTracking.startSession({
          exerciseId: this.exerciseId,
          source: 'route_navigation'
        });
        this.hasTrackedResultsLoginCtaView = false;
        this.exerciseStartedAtMs = null;
        this.shouldResetCompletedStateOnNextStart = false;
        this.exerciseState.set('intro');
        this.stateAnimOn.set(false);
        this.isSubmitting.set(false);
        this.isRestoringExercise.set(false);
        this.initialText.set(null);
        this.initialAutoPause.set(null);
        this.userText.set('');
        this.exerciseAudioController.reset();
        this.exerciseAudioAccess.reset();
        this.exerciseStateRestore.resetForExercise(this.exerciseId);
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
        if (!this.browserService.isBrowserEnvironment() || this.hasCompletedInitialBrowserRender) {
          void this.restoreExerciseState();
        }
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
      this.hasCompletedInitialBrowserRender = true;
      this.hasHydrated = true;
      this.tryResolveAudioAccessAfterHydration();
      this.exerciseAudioController.applyPendingPausedTimeIfNeeded(this.newsAudioComponentRef);
      queueMicrotask(() => this.stateAnimEnabled.set(true));
      void this.restoreExerciseState();
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
    proposition.newsUrl = initialProposition.newsUrl;
    return proposition;
  }

  private getExerciseStateKey(exerciseId: number | null = this.exerciseId): string | null {
    if (!exerciseId) return null;
    return `exercise-section-state-${exerciseId}`;
  }

  private getStateKey(exerciseId: number | null = this.exerciseId): string | null {
    if (!exerciseId) return null;
    return `listen-write-state-${exerciseId}`;
  }

  loadProposition(id: number): void {
    this.propositionsService.apiPropositionIdGet(id).subscribe({
      next: (data) => {
        if (this.exerciseId !== id) {
          return;
        }

        this.proposition.set(data);
        this.exerciseSessionTracking.updateSessionContext({
          exerciseId: id
        });
        this.exerciseSessionTracking.trackEvent('exercise_content_loaded', {
          title: data.title,
          subject: data.subjectId ?? '',
          complexity: data.complexityId ?? ''
        }, {
          audio_duration_seconds: data.audioDurationSeconds ?? 0
        });
        this.exerciseSeo.applyExerciseSeo(id, data);
        this.syncCompletedResultAfterRestoreIfNeeded(data);
        this.tryResolveAudioAccessAfterHydration();
      },
      error: (error) => {
        if (this.exerciseId !== id) {
          return;
        }
        
        console.error('Error loading proposition:', error);
        // notify the user and back to the home page
        alert('Error loading exercise. Returning to home page.');
        this.browserService.navigateTo('/');
      }
    });
  }

  async restoreExerciseState(): Promise<void> {
    await this.exerciseStateRestore.restore({
      exerciseId: this.exerciseId,
      getStateKey: (exerciseId) => this.getStateKey(exerciseId),
      getExerciseStateKey: (exerciseId) => this.getExerciseStateKey(exerciseId),
      applySnapshot: (snapshot) => this.applyRestoredExerciseSnapshot(snapshot),
      resetPendingPausedTime: () => this.exerciseAudioController.resetPendingPausedTime(),
    });

    this.syncCompletedResultAfterRestoreIfNeeded(this.proposition());
  }

  private applyRestoredExerciseSnapshot(snapshot: models.RestoredExerciseSnapshot): void {
    if (snapshot.state) {
      this.exerciseState.set(snapshot.state);
      if (snapshot.state === 'exercise' && this.exerciseStartedAtMs === null) {
        this.exerciseStartedAtMs = Date.now();
      }
    }

    this.initialText.set(snapshot.userText);
    this.initialAutoPause.set(snapshot.autoPauseSeconds ?? 2);
    this.setPendingPausedTime(snapshot.pausedTimeSeconds);
    this.result.set(snapshot.result);
  }

  private syncCompletedResultAfterRestoreIfNeeded(proposition: Proposition | null): void {
    this.exerciseStateRestore.syncCompletedResultAfterRestoreIfNeeded({
      proposition,
      exerciseState: this.exerciseState(),
      result: this.result(),
    });
  }

  private setPendingPausedTime(pausedTimeSeconds: number | null): void {
    this.exerciseAudioController.setPendingPausedTime(pausedTimeSeconds, this.newsAudioComponentRef);
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyboardEvent(event: KeyboardEvent) {
    this.exerciseAudioController.handleKeyboardEvent(event, this.getAudioControlContext());
  }

  getBeginExerciseLoadingTooltip(): string {
    return this.isResolvingAudioAccess()
      ? 'Loading audio...'
      : 'Starting exercise...';
  }

  playAudioWithAutoPause() {
    this.exerciseAudioController.playAudioWithAutoPause(this.getAudioControlContext());
  }

  pauseAudioWithTimerClear() {
    this.exerciseAudioController.pauseAudioWithTimerClear(this.newsAudioComponentRef);
  }

  clearAutoPauseTimer() {
    this.exerciseAudioController.clearAutoPauseTimer();
  }

  beginExercise() {
    const audioEndedBeforeBegin = this.newsAudioComponentRef?.audioEnded ?? false;

    const beginPrompt = this.guestExerciseLoginPrompt.prepareBeginExercise({
      isFirstTimeUser: this.isFirstTime(),
      audioEndedBeforeBegin,
    });

    if (beginPrompt.decision.shouldShow) {
      this.guestExerciseLoginPrompt.open(beginPrompt.context);
      return;
    }

    this.startExerciseFromContext(beginPrompt.context);
  }

  onGuestLoginModalSignIn(): void {
    this.guestExerciseLoginPrompt.signIn(this.getPostLoginReturnUrl());
  }

  onGuestLoginModalContinueAsGuest(): void {
    const beginContext = this.guestExerciseLoginPrompt.continueAsGuest();
    if (beginContext) {
      this.startExerciseFromContext(beginContext);
    }
  }

  onGuestLoginModalBackdropClick(): void {
    const beginContext = this.guestExerciseLoginPrompt.dismissBackdrop();
    if (beginContext) {
      this.startExerciseFromContext(beginContext);
    }
  }

  private startExerciseFromContext(context: models.BeginExerciseContext): void {
    if (!this.exerciseId) {
      return;
    }

    if (this.exerciseAudioUrl()) {
      this.startWritingExercise(context);
      return;
    }

    this.resolveExerciseAudioAccess({
      context,
      startWritingWhenGranted: true,
    });
  }

  private tryResolveAudioAccessAfterHydration(): void {
    this.exerciseAudioAccess.tryResolveAfterHydration({
      exerciseId: this.exerciseId,
      hasHydrated: this.hasHydrated,
      isBrowserEnvironment: this.browserService.isBrowserEnvironment(),
      hasProposition: Boolean(this.proposition()),
      exerciseState: this.exerciseState(),
      callbacks: this.buildAudioAccessCallbacks(),
    });
  }

  private resolveExerciseAudioAccess(options: {
    startWritingWhenGranted: boolean;
    context?: models.BeginExerciseContext;
  }): void {
    if (!this.exerciseId) {
      return;
    }

    this.exerciseAudioAccess.resolve({
      exerciseId: this.exerciseId,
      startWritingWhenGranted: options.startWritingWhenGranted,
      context: options.context,
      callbacks: this.buildAudioAccessCallbacks(),
    });
  }

  private buildAudioAccessCallbacks(): AudioAccessCallbacks {
    return {
      onMetadata: (metadata: Proposition) => this.proposition.set(metadata),
      onProRequired: () => {
        this.exerciseAudioController.clearAutoPauseTimer();
        if (this.exerciseState() !== 'intro') {
          this.setNewState('intro');
        }
        this.openProUpgradeModal();
      },
      onGranted: (context?: models.BeginExerciseContext) => {
        if (context) {
          this.startWritingExercise(context);
        }
      },
      onMissingAudio: (startWritingWhenGranted: boolean) => {
        if (startWritingWhenGranted) {
          alert('Error starting exercise. Please try again.');
        }
      },
      onError: (_error: unknown, startWritingWhenGranted: boolean) => {
        if (startWritingWhenGranted) {
          alert('Error starting exercise. Please try again.');
        }
      },
    };
  }

  private startWritingExercise(context: models.BeginExerciseContext): void {
    if (!this.exerciseAudioUrl()) {
      this.resolveExerciseAudioAccess({
        context,
        startWritingWhenGranted: true,
      });
      return;
    }

    this.exerciseAudioController.pauseAndResetAudio(this.newsAudioComponentRef);

    if (context.audioEndedBeforeBegin) {
      this.completeExerciseStart(context, 'audio_already_completed');
      return;
    }

    if (context.isFirstTimeUser) {
      this.exerciseSessionTracking.trackEvent('listen_first_prompt_shown', {
        guest_login_modal_shown_before_start: context.guestLoginModalShownBeforeStart,
      }, {
        guest_begin_attempt_count: context.guestBeginAttemptCount ?? 0,
      });
      this.listenFirstTourService.prompt(
        '#newsAudio',
        () => this.exerciseAudioController.playAudioFromListenFirstPrompt(this.newsAudioComponentRef),
        () => this.completeExerciseStart(context, 'skip_listen_first_prompt'),
      );
      return;
    }

    this.completeExerciseStart(context, 'direct_start');
  }

  private completeExerciseStart(
    context: models.BeginExerciseContext,
    startMode: 'audio_already_completed' | 'skip_listen_first_prompt' | 'direct_start',
  ): void {
    this.exerciseSessionTracking.trackEvent('exercise_started', {
      start_mode: startMode,
      guest_login_modal_shown_before_start: context.guestLoginModalShownBeforeStart,
      audio_url_expires_at_utc: this.exerciseAudioAccess.getAudioExpiresAtUtc() ?? '',
    }, {
      guest_begin_attempt_count: context.guestBeginAttemptCount ?? 0,
    });
    this.trackExerciseStart();
    this.setNewState('exercise');
    this.browserService.scrollToTop();
  }

  openProUpgradeModal(): void {
    this.isProUpgradeModalOpen.set(true);
  }

  closeProUpgradeModal(): void {
    this.isProUpgradeModalOpen.set(false);
  }

  async viewAvailablePlans(): Promise<void> {
    this.closeProUpgradeModal();
    await this.router.navigate(['/plans'], {
      queryParams: {
        source: 'pro_exercise_begin',
      },
    });
  }

  async keepPracticingFreeExercises(): Promise<void> {
    this.closeProUpgradeModal();
    await this.router.navigate(['/exercises'], {
      queryParams: {
        source: 'pro_exercise_begin',
      },
    });
  }

  private trackExerciseStart(): void {
    if (this.shouldResetCompletedStateOnNextStart) {
      this.shouldResetCompletedStateOnNextStart = false;
      this.exerciseProgressTracking.trackStart(this.proposition(), { resetCompletedState: true });
      return;
    }

    this.exerciseProgressTracking.trackStart(this.proposition());
  }

  onAudioPlayClicked() {
    this.exerciseAudioController.onAudioPlayClicked(this.getAudioControlContext());
  }

  cancelTour() {
    this.listenFirstTourService.cancelTour();
  }

  startExerciseTour() {
    this.exerciseTourService.startTour();
  }

  applyAutoPause() {
    this.exerciseAudioController.applyAutoPause(this.getAudioControlContext());
  }

  onExerciseSubmit() {
    const submitWarning = this.getSubmitWarningMessage();
    this.exerciseSubmission.trackSubmitClicked(Boolean(submitWarning));

    if (submitWarning) {
      queueMicrotask(() => {
        this.submitTour.startRecommendationTour(submitWarning, () => this.submitExercise());
      });
      return;
    }

    this.submitExercise();
  }

  private submitExercise() {
    const proposition = this.proposition();
    const submittedUserText = this.exerciseSectionComponent.text();
    this.exerciseAudioController.pauseAndResetAudio(this.newsAudioComponentRef);

    this.exerciseSubmission.submit({
      proposition,
      exerciseId: this.exerciseId,
      submittedUserText,
      exerciseTimeUsedMs: this.getExerciseTimeUsedMs(),
      onSuccess: (result, finalSubmittedUserText) => this.applySubmitSuccess(
        proposition,
        result,
        finalSubmittedUserText,
      ),
      onProRequired: () => this.openProUpgradeModal(),
      onFailure: () => alert('Error processing your exercise. Please try again.'),
    });
  }

  private applySubmitSuccess(
    proposition: Proposition | null,
    result: TextComparisonResult,
    submittedUserText: string,
  ): void {
    if (!proposition) {
      return;
    }

    this.userText.set(submittedUserText);
    this.result.set(result);
    this.exerciseProgressTracking.trackComplete(proposition, result);
    this.onSaveExerciseState();
    this.setNewState('results');
    this.browserService.scrollToTop();
  }

  private getSubmitWarningMessage(): string | null {
    return this.exerciseSubmission.getSubmitWarningMessage(
      this.getSubmitAudioState(),
      this.proposition()?.originalWordCount,
      this.exerciseSectionComponent?.wordCount ?? 0,
    );
  }

  private hasCompletedAudioPlayback(): boolean {
    return this.exerciseSubmission.hasCompletedAudioPlayback(this.getSubmitAudioState());
  }

  private getSubmitAudioState(): SubmitAudioState {
    const audio = this.newsAudioComponent?.audioRef?.nativeElement;

    return {
      audioEnded: this.newsAudioComponent?.audioEnded ?? false,
      currentTime: audio?.currentTime ?? null,
      duration: audio?.duration ?? null,
    };
  }

  private getExerciseTimeUsedMs(): number | null {
    if (this.exerciseStartedAtMs === null) {
      return null;
    }

    return Math.max(0, Date.now() - this.exerciseStartedAtMs);
  }

  ngOnDestroy(): void {
    this.exerciseFeedbackFlow.destroy();
    this.exerciseSessionTracking.endSession('component_destroyed');
    this.exerciseAudioController.reset();
  }

  canDeactivateFromRoute(): boolean | Promise<boolean> {
    return this.exerciseFeedbackFlow.canDeactivateFromRoute(this.exerciseState());
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

  shouldShowResultsLoginCta(): boolean {
    return this.exerciseState() === 'results'
      && !this.authSessionStore.isAuthenticated();
  }

  onSignInToSaveProgress(): void {
    const returnUrl = this.getPostLoginReturnUrl();
    this.exerciseStateRestore.setPendingCompletedSyncRequest(this.exerciseId);
    this.exerciseSessionTracking.trackEvent('results_login_cta_clicked', {
      return_url: returnUrl,
      exercise_id: String(this.exerciseId ?? ''),
      source: 'results_inline_cta',
    });

    void this.router.navigate(['/auth/login'], {
      queryParams: {
        returnUrl,
        source: 'results_save_cta',
      }
    });
  }

  onAudioPaused() {
    this.exerciseAudioController.onAudioPaused(this.getAudioControlContext());
  }

  onAudioSeeked() {
    this.exerciseAudioController.onAudioSeeked(this.getAudioControlContext());
  }

  onSaveExerciseState() {
    if (this.exerciseState() !== 'exercise') {
      return;
    }

    const stateKey = this.getExerciseStateKey();
    if (!stateKey) return;
    
    const state = {
      userText: this.exerciseSectionComponent?.text(),
      autoPause: this.exerciseSectionComponent?.selectedAutoPause(),
      pausedTime: this.newsAudioComponent?.audioRef?.nativeElement?.currentTime || 0,
      result: this.result(),
      savedAtUtc: new Date().toISOString(),
    };

    this.browserService.setItem(stateKey, JSON.stringify(state));

    if (this.result() === null) {
      this.exerciseProgressTracking.saveState(this.proposition(), {
        exerciseState: 'exercise',
        userText: state.userText ?? null,
        autoPauseSeconds: state.autoPause ?? null,
        pausedTimeSeconds: state.pausedTime ?? null,
      });
    }
  }

  getProgressSyncNotification(): ProgressSyncNotification | null {
    return this.exerciseProgressTracking.syncNotification();
  }

  dismissProgressSyncNotification(): void {
    this.exerciseProgressTracking.dismissSyncNotification();
  }

  onExerciseTextChanged(text: string) {
    this.exerciseSessionTracking.trackTextChanged(text);
  }

  onExerciseTutorialVideoRequested(): void {
    this.openTutorialVideoFromSource('exercise_help_icon');
  }

  onTutorialVideoModalOpened(): void {
    if (!this.tutorialVideoSource) {
      return;
    }

    this.exerciseSessionTracking.trackEvent('tutorial_video_opened', {
      source: this.tutorialVideoSource,
    });
  }

  onTutorialVideoModalClosed(): void {
    if (this.tutorialVideoSource) {
      this.exerciseSessionTracking.trackEvent('tutorial_video_closed', {
        source: this.tutorialVideoSource,
      });
    }

    this.tutorialVideoSource = null;
    this.isTutorialVideoModalOpen.set(false);
  }

  setNewState(state: models.ExerciseState) {
    const previousState = this.exerciseState();

    if (state === 'intro') {
      this.exerciseStartedAtMs = null;
    }

    if (state === 'exercise' && previousState !== 'exercise') {
      this.exerciseStartedAtMs = Date.now();
    }

    if (state !== 'results') {
      this.hasTrackedResultsLoginCtaView = false;
    }

    this.exerciseState.set(state);
    // set local storage state
    const stateKey = this.getStateKey();
    if (stateKey) {
      this.browserService.setItem(stateKey, state);
    }

    if (state === 'results' && this.shouldShowResultsLoginCta() && !this.hasTrackedResultsLoginCtaView) {
      this.exerciseSessionTracking.trackEvent('results_login_cta_viewed', {
        exercise_id: String(this.exerciseId ?? ''),
        source: 'results_inline_cta',
      });
      this.hasTrackedResultsLoginCtaView = true;
    }

    if(this.isFirstTime() && state === 'exercise') {
      const openTutorialVideoFromTour = () => this.openTutorialVideoFromSource('exercise_tour_final_step');
      if (this.isMobileLayout()) {
        this.exerciseTourService.startMobileTour({ onWatchTutorialVideo: openTutorialVideoFromTour });
      } else {
        this.exerciseTourService.startTour({ onWatchTutorialVideo: openTutorialVideoFromTour });
      }
    }
  }

  isFirstTime(): boolean {
    const localTutorialKey = this.browserService.getItem(constants.listenWriteFirstTimeKey);
    const hasLocalDone = localTutorialKey !== null;
    const isAuthenticated = this.authSessionStore.isAuthenticated();
    const hasReliableSessionState = this.authSessionStore.hasReliableSessionState();
    const serverTutorialCompleted = this.authSessionStore.listenWriteTutorialCompleted();
    const userId = this.authSessionStore.userId();

    if (hasLocalDone) {
      this.tryBackfillTutorialCompletionIfNeeded(
        isAuthenticated,
        hasReliableSessionState,
        serverTutorialCompleted,
        userId,
      );
      return false;
    }

    if (!hasReliableSessionState) {
      this.trackTutorialSuppressedForUnreliableSessionState(isAuthenticated);
      return false;
    }

    if (!isAuthenticated) {
      return true;
    }

    return serverTutorialCompleted !== true;
  }

  private tryBackfillTutorialCompletionIfNeeded(
    isAuthenticated: boolean,
    hasReliableSessionState: boolean,
    serverTutorialCompleted: boolean | null,
    userId: string | null,
  ): void {
    if (!isAuthenticated || !hasReliableSessionState || serverTutorialCompleted === true) {
      return;
    }

    const backfillKey = userId ?? '__authenticated_unknown_user__';
    if (this.tutorialBackfillRequestedUsers.has(backfillKey)) {
      return;
    }

    this.tutorialBackfillRequestedUsers.add(backfillKey);
    this.authSessionStore.markListenWriteTutorialCompletedInBackground();
  }

  private trackTutorialSuppressedForUnreliableSessionState(isAuthenticated: boolean): void {
    if (this.hasLoggedTutorialSuppressedException) {
      return;
    }

    this.hasLoggedTutorialSuppressedException = true;
    this.insights?.trackException(
      new Error('Tutorial suppressed because auth session state is unresolved.'),
      {
        properties: {
          component: 'ListenAndWriteComponent',
          tutorial_key_present: 'false',
          has_reliable_session_state: 'false',
          is_authenticated: String(isAuthenticated),
        },
        severityLevel: 1,
      },
    );
  }

  private isMobileLayout(): boolean {
    const width = this.browserService.getWindowWidth();
    return width > 0 && width <= 900;
  }

  private getAudioControlContext() {
    return {
      exerciseState: this.exerciseState(),
      audio: this.newsAudioComponentRef,
      section: this.exerciseSectionComponent ?? null,
      isMobileLayout: this.isMobileLayout(),
      onSaveExerciseState: () => this.onSaveExerciseState(),
      onCancelListenFirstTour: () => this.cancelTour(),
      onFinishExerciseTour: () => this.exerciseTourService.finishTour(),
    };
  }

  onFeedbackDismissed(_reason: 'not_now' | 'close'): void {
    this.exerciseFeedbackFlow.onDismissed(_reason);
  }

  onFeedbackSubmitted(submission: feedbackModal.FeedbackModalSubmission): void {
    this.exerciseFeedbackFlow.onSubmitted(submission, {
      proposition: this.proposition(),
      exerciseId: this.exerciseId,
    });
  }

  onFeedbackInteraction(event: feedbackModal.FeedbackModalInteractionEvent): void {
    this.exerciseFeedbackFlow.onInteraction(event);
  }

  onFeedbackClosedAfterSubmit(): void {
    this.exerciseFeedbackFlow.onClosedAfterSubmit();
  }

  onFeedbackFindAnotherExercise(): void {
    this.exerciseFeedbackFlow.onFindAnotherExercise(() => this.navigateToExercises());
  }

  private attemptLeaveResults(action: () => void): void {
    this.exerciseFeedbackFlow.attemptLeaveResults(this.exerciseState(), action);
  }

  private resetExerciseForTryAgain(): void {
    const stateKey = this.getExerciseStateKey();
    if (stateKey) {
      this.browserService.removeItem(stateKey);
    }

    this.initialText.set(null);
    this.initialAutoPause.set(null);
    this.result.set(null);
    this.shouldResetCompletedStateOnNextStart = true;
    if (this.newsAudioComponentRef?.audioRef?.nativeElement) {
      this.newsAudioComponentRef.audioRef.nativeElement.currentTime = 0;
    }
    this.setNewState('intro');
  }

  private navigateToExercises(): void {
    this.exerciseSessionTracking.endSession('navigate_to_exercises');
    this.router.navigate(['/exercises']);
  }

  private getPostLoginReturnUrl(): string {
    if (this.exerciseId) {
      return `/english-writing-exercise/${this.exerciseId}`;
    }

    const currentUrl = this.router.url;
    if (currentUrl.startsWith('/') && !currentUrl.startsWith('//')) {
      return currentUrl;
    }

    return '/exercises';
  }

  private openTutorialVideoFromSource(source: tutorialVideoConfig.TutorialVideoSource): void {
    this.tutorialVideoSource = source;
    this.exerciseSessionTracking.trackEvent('tutorial_video_cta_clicked', {
      source,
    });
    this.isTutorialVideoModalOpen.set(true);
  }
}
