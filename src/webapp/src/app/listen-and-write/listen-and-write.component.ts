import { Component, signal, ViewChild, HostListener, effect, OnDestroy, Optional, afterNextRender, computed, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
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
import { ExerciseProgressTrackingService, ProgressSyncNotification } from './services/exercise-progress-tracking.service';
import { ExerciseAudioAccessService, type AudioAccessCallbacks } from './services/exercise-audio-access.service';
import { AuthSessionStore } from '../auth/services/auth-session.store';
import { ProgressStateResponse } from '../user/models/user-progress.model';
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
  providers: [ExerciseAudioAccessService],
})
export class ListenAndWriteComponent implements OnDestroy {
  private readonly exerciseAudioAccess = inject(ExerciseAudioAccessService);

  @ViewChild(ExerciseSectionComponent) exerciseSectionComponent!: ExerciseSectionComponent;

  private newsAudioComponentRef: NewsAudioComponent | null = null;
  private pendingPausedTimeSeconds: number | null = null;
  private restoreRequestToken = 0;
  private hasCompletedInitialBrowserRender = false;

  @ViewChild(NewsAudioComponent)
  set newsAudioComponent(component: NewsAudioComponent | undefined) {
    this.newsAudioComponentRef = component ?? null;
    this.applyPendingPausedTimeIfNeeded();
  }

  get newsAudioComponent(): NewsAudioComponent {
    return this.newsAudioComponentRef as NewsAudioComponent;
  }

  private autoPauseTimer: any = null;
  private pendingAudioPlaySource: models.AudioPlaySource | null = null;
  private pendingAudioPlaySourceTimer: ReturnType<typeof setTimeout> | null = null;

  exerciseState = signal<models.ExerciseState>('intro');

  stateAnimOn = signal(false);
  stateAnimEnabled = signal(false);

  proposition = signal<Proposition | null>(null);
  exerciseAudioUrl = this.exerciseAudioAccess.audioUrl;

  result = signal<TextComparisonResult | null>(null);

  isSubmitting = signal<boolean>(false);
  isBeginningExercise = this.exerciseAudioAccess.isBeginningExercise;
  isResolvingAudioAccess = this.exerciseAudioAccess.isResolving;
  shouldShowAudioPanel = computed(() =>
    Boolean(this.exerciseAudioUrl())
    || this.isResolvingAudioAccess()
    || (this.exerciseState() === 'intro' && Boolean(this.proposition())));
  isRestoringExercise = signal<boolean>(false);
  isGuestLoginModalOpen = signal<boolean>(false);
  isProUpgradeModalOpen = signal<boolean>(false);

  initialText = signal<string | null>(null);
  
  initialAutoPause = signal<number | null>(null);

  userText = signal<string>('');

  isFeedbackModalOpen = signal<boolean>(false);
  isTutorialVideoModalOpen = signal<boolean>(false);

  exerciseId: number | null = null;
  readonly tutorialVideoEmbedUrl = tutorialVideoConfig.tutorialVideoConfig.embedUrl;
  readonly tutorialVideoWatchUrl = tutorialVideoConfig.tutorialVideoConfig.watchUrl;
  readonly tutorialVideoTitle = tutorialVideoConfig.tutorialVideoConfig.title;

  private pendingLeaveAction: (() => void) | null = null;
  private pendingRouteLeaveResolver: ((allow: boolean) => void) | null = null;
  private pendingBeginExerciseContext: models.BeginExerciseContext | null = null;
  private hasTrackedResultsLoginCtaView = false;
  private exerciseStartedAtMs: number | null = null;
  private shouldSyncCompletedResultAfterRestore = false;
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
    private textComparisonsService: TextComparisonsService,
    private browserService: BrowserService,
    private seoService: SeoService,
    private exerciseSessionTracking: ExerciseSessionTrackingService,
    private feedbackService: FeedbackService,
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
        this.isGuestLoginModalOpen.set(false);
        this.pendingBeginExerciseContext = null;
        this.exerciseSessionTracking.startSession({
          exerciseId: this.exerciseId,
          source: 'route_navigation'
        });
        this.hasTrackedResultsLoginCtaView = false;
        this.exerciseStartedAtMs = null;
        this.shouldSyncCompletedResultAfterRestore = false;
        this.shouldResetCompletedStateOnNextStart = false;
        this.exerciseState.set('intro');
        this.stateAnimOn.set(false);
        this.isSubmitting.set(false);
        this.isRestoringExercise.set(false);
        this.initialText.set(null);
        this.initialAutoPause.set(null);
        this.userText.set('');
        this.exerciseAudioAccess.reset();
        this.result.set(null);
        this.pendingPausedTimeSeconds = null;
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
      this.applyPendingPausedTimeIfNeeded();
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
        
        // Update SEO meta tags for this specific exercise
        const complexityDesc = data.complexityId || 'Intermediate';
        const subjectDesc = data.subjectId || 'News';
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

  private getExerciseImageUrl(imageFileId?: string | null): string | undefined {
    if (!imageFileId) {
      return undefined;
    }

    return `${environment.minioUrl}/images/${imageFileId}`;
  }

  async restoreExerciseState(): Promise<void> {
    const exerciseId = this.exerciseId;
    if (!exerciseId) {
      return;
    }

    const restoreToken = ++this.restoreRequestToken;
    this.pendingPausedTimeSeconds = null;

    if (!this.authSessionStore.isAuthenticated()) {
      this.isRestoringExercise.set(false);
      this.restoreStateFromLocalStorage(exerciseId, restoreToken);
      return;
    }

    this.isRestoringExercise.set(true);

    try {
      const serverState = await this.loadServerStateWithTimeout(exerciseId);
      if (!this.isRestoreTokenActive(restoreToken, exerciseId)) {
        return;
      }

      const localSnapshot = this.readLocalExerciseSnapshot(exerciseId);
      const shouldSyncCompletedAfterLogin = this.consumePendingCompletedSyncRequest(exerciseId, localSnapshot);
      if (shouldSyncCompletedAfterLogin) {
        this.applyLocalExerciseSnapshot(localSnapshot!);
        this.shouldSyncCompletedResultAfterRestore = true;
        const proposition = this.proposition();
        if (proposition) {
          this.syncCompletedResultAfterRestoreIfNeeded(proposition);
        }
        return;
      }

      if (this.shouldPreferLocalCompletedState(serverState, localSnapshot)) {
        this.applyLocalExerciseSnapshot(localSnapshot!);
        this.shouldSyncCompletedResultAfterRestore = false;
        return;
      }

      if (serverState) {
        this.shouldSyncCompletedResultAfterRestore = false;
        this.applyServerState(serverState);
        return;
      }

      if (localSnapshot) {
        this.shouldSyncCompletedResultAfterRestore = false;
        this.applyLocalExerciseSnapshot(localSnapshot);
      } else {
        this.shouldSyncCompletedResultAfterRestore = false;
        this.restoreStateFromLocalStorage(exerciseId, restoreToken);
      }
    } finally {
      if (this.isRestoreTokenActive(restoreToken, exerciseId)) {
        this.isRestoringExercise.set(false);
      }
    }
  }

  private async loadServerStateWithTimeout(exerciseId: number) {
    let timeoutHandle: ReturnType<typeof setTimeout> | null = null;
    const timeoutPromise = new Promise<null>((resolve) => {
      timeoutHandle = setTimeout(() => resolve(null), constants.restoreServerStateTimeoutMs);
    });

    try {
      return await Promise.race([
        this.exerciseProgressTracking.loadState(exerciseId),
        timeoutPromise,
      ]);
    } finally {
      if (timeoutHandle) {
        clearTimeout(timeoutHandle);
      }
    }
  }

  private applyServerState(serverState: ProgressStateResponse): void {
    const restoredExerciseState = this.normalizeExerciseState(serverState.exerciseState);
    if (restoredExerciseState) {
      this.exerciseState.set(restoredExerciseState);
      if (restoredExerciseState === 'exercise' && this.exerciseStartedAtMs === null) {
        this.exerciseStartedAtMs = Date.now();
      }
    }

    this.initialText.set(serverState.userText ?? null);
    this.initialAutoPause.set(serverState.autoPauseSeconds ?? 2);
    this.result.set(this.buildRestoredResult(serverState, restoredExerciseState));
    this.setPendingPausedTime(serverState.pausedTimeSeconds);
  }

  private buildRestoredResult(
    serverState: ProgressStateResponse,
    restoredExerciseState: models.ExerciseState | null,
  ): TextComparisonResult | null {
    if (restoredExerciseState !== 'results') {
      return null;
    }

    return {
      originalText: serverState.originalText ?? null,
      userText: serverState.userText,
      comparisons: serverState.comparisons ?? [],
      accuracyPercentage: serverState.accuracyPercentage ?? 0,
    };
  }

  private restoreStateFromLocalStorage(exerciseId: number, restoreToken: number): void {
    if (!this.isRestoreTokenActive(restoreToken, exerciseId)) {
      return;
    }

    const localSnapshot = this.readLocalExerciseSnapshot(exerciseId);
    if (!localSnapshot) {
      return;
    }

    this.applyLocalExerciseSnapshot(localSnapshot);
  }

  private applyLocalExerciseSnapshot(localSnapshot: models.LocalExerciseSnapshot): void {
    if (localSnapshot.state) {
      this.exerciseState.set(localSnapshot.state);
      if (localSnapshot.state === 'exercise' && this.exerciseStartedAtMs === null) {
        this.exerciseStartedAtMs = Date.now();
      }
    }

    this.initialText.set(localSnapshot.userText);
    this.initialAutoPause.set(localSnapshot.autoPauseSeconds ?? 2);
    this.setPendingPausedTime(localSnapshot.pausedTimeSeconds);
    this.result.set(localSnapshot.result);
  }

  private readLocalExerciseSnapshot(exerciseId: number): models.LocalExerciseSnapshot | null {
    const stateKey = this.getStateKey(exerciseId);
    const exerciseState = stateKey
      ? this.normalizeExerciseState(this.browserService.getItem(stateKey))
      : null;

    const exerciseStateKey = this.getExerciseStateKey(exerciseId);
    if (!exerciseStateKey) {
      return null;
    }

    const serializedState = this.browserService.getItem(exerciseStateKey);
    if (!serializedState) {
      return exerciseState
        ? {
            state: exerciseState,
            userText: null,
            autoPauseSeconds: null,
            pausedTimeSeconds: null,
            result: null,
            savedAtUtc: null,
          }
        : null;
    }

    try {
      const parsed = JSON.parse(serializedState) as {
        userText?: string | null;
        autoPause?: number | null;
        pausedTime?: number | null;
        result?: TextComparisonResult | null;
        savedAtUtc?: string | null;
      };

      return {
        state: exerciseState,
        userText: parsed.userText || null,
        autoPauseSeconds: parsed.autoPause ?? null,
        pausedTimeSeconds: parsed.pausedTime ?? null,
        result: parsed.result ?? null,
        savedAtUtc: parsed.savedAtUtc ?? null,
      };
    } catch {
      this.browserService.removeItem(exerciseStateKey);
      return exerciseState
        ? {
            state: exerciseState,
            userText: null,
            autoPauseSeconds: null,
            pausedTimeSeconds: null,
            result: null,
            savedAtUtc: null,
          }
        : null;
    }
  }

  private shouldPreferLocalCompletedState(
    serverState: ProgressStateResponse | null,
    localSnapshot: models.LocalExerciseSnapshot | null,
  ): boolean {
    if (!serverState || !localSnapshot) {
      return false;
    }

    if (localSnapshot.state !== 'results' || !localSnapshot.result) {
      return false;
    }

    const localSavedAtMs = this.parseTimestamp(localSnapshot.savedAtUtc);
    const serverUpdatedAtMs = this.parseTimestamp(serverState.updatedAtUtc);
    if (localSavedAtMs !== null && serverUpdatedAtMs !== null) {
      return localSavedAtMs > serverUpdatedAtMs;
    }

    if (localSavedAtMs !== null && serverUpdatedAtMs === null) {
      return true;
    }

    const normalizedServerState = this.normalizeExerciseState(serverState.exerciseState);
    const serverHasDraftText = Boolean(serverState.userText?.trim());
    const serverHasRestorableDraft =
      normalizedServerState === 'exercise'
      || serverHasDraftText
      || serverState.autoPauseSeconds !== null
      || serverState.pausedTimeSeconds !== null;

    return !serverHasRestorableDraft;
  }

  private parseTimestamp(value: string | null | undefined): number | null {
    if (!value) {
      return null;
    }

    const parsed = Date.parse(value);
    if (!Number.isFinite(parsed)) {
      return null;
    }

    return parsed;
  }

  private setPendingCompletedSyncRequest(exerciseId: number | null): void {
    if (!exerciseId || !this.browserService.isBrowserEnvironment()) {
      return;
    }

    try {
      window.sessionStorage.setItem(
        constants.postLoginCompleteSyncStorageKey,
        JSON.stringify({
          exerciseId,
          createdAtUtc: new Date().toISOString(),
        }));
    } catch {
      // noop
    }
  }

  private consumePendingCompletedSyncRequest(
    exerciseId: number,
    localSnapshot: models.LocalExerciseSnapshot | null,
  ): boolean {
    if (!this.browserService.isBrowserEnvironment()) {
      return false;
    }

    let pendingExerciseId: number | null = null;
    try {
      const rawValue = window.sessionStorage.getItem(constants.postLoginCompleteSyncStorageKey);
      if (!rawValue) {
        return false;
      }

      const parsed = JSON.parse(rawValue) as { exerciseId?: unknown };
      if (typeof parsed.exerciseId === 'number' && Number.isFinite(parsed.exerciseId)) {
        pendingExerciseId = parsed.exerciseId;
      }
    } catch {
      // noop
    }

    if (pendingExerciseId !== exerciseId) {
      return false;
    }

    try {
      window.sessionStorage.removeItem(constants.postLoginCompleteSyncStorageKey);
    } catch {
      // noop
    }

    return localSnapshot?.state === 'results' && localSnapshot.result !== null;
  }

  private syncCompletedResultAfterRestoreIfNeeded(proposition: Proposition): void {
    if (!this.shouldSyncCompletedResultAfterRestore || !this.authSessionStore.isAuthenticated()) {
      return;
    }

    this.shouldSyncCompletedResultAfterRestore = false;

    const restoredResult = this.result();
    if (this.exerciseState() !== 'results' || !restoredResult) {
      return;
    }

    this.exerciseProgressTracking.trackComplete(proposition, restoredResult);
  }

  private setPendingPausedTime(pausedTimeSeconds: number | null): void {
    if (!pausedTimeSeconds || pausedTimeSeconds <= 0) {
      this.pendingPausedTimeSeconds = null;
      return;
    }

    this.pendingPausedTimeSeconds = pausedTimeSeconds;
    this.applyPendingPausedTimeIfNeeded();
  }

  private applyPendingPausedTimeIfNeeded(): void {
    if (!this.pendingPausedTimeSeconds || !this.newsAudioComponentRef) {
      return;
    }

    this.newsAudioComponentRef.forwardAudio(this.pendingPausedTimeSeconds);
    this.pendingPausedTimeSeconds = null;
  }

  private isRestoreTokenActive(restoreToken: number, exerciseId: number): boolean {
    return this.restoreRequestToken === restoreToken && this.exerciseId === exerciseId;
  }

  private normalizeExerciseState(value: string | null | undefined): models.ExerciseState | null {
    if (!value) {
      return null;
    }

    if (value === 'intro' || value === 'exercise' || value === 'results') {
      return value;
    }

    return null;
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

  getBeginExerciseLoadingTooltip(): string {
    return this.isResolvingAudioAccess()
      ? 'Loading audio...'
      : 'Starting exercise...';
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
    const beginContext = this.buildBeginExerciseContext();
    const modalDecision = this.evaluateGuestLoginModalDecision(beginContext);

    this.exerciseSessionTracking.trackEvent('begin_exercise_clicked', {
      is_first_time: beginContext.isFirstTimeUser,
      audio_ended_before_begin: beginContext.audioEndedBeforeBegin,
      is_authenticated: this.authSessionStore.isAuthenticated(),
      guest_begin_attempt_count: beginContext.guestBeginAttemptCount,
      guest_login_modal_decision: modalDecision.reason,
    }, {
      guest_begin_attempt_count: beginContext.guestBeginAttemptCount ?? 0,
      guest_login_modal_cooldown_remaining_minutes: Math.ceil(modalDecision.cooldownRemainingMs / 60000),
    });

    if (modalDecision.shouldShow) {
      this.openGuestLoginModal(beginContext);
      return;
    }

    if (!this.authSessionStore.isAuthenticated() && modalDecision.reason !== 'authenticated') {
      this.exerciseSessionTracking.trackEvent('guest_login_modal_not_shown', {
        reason: modalDecision.reason,
        guest_begin_attempt_count: beginContext.guestBeginAttemptCount,
      }, {
        guest_begin_attempt_count: beginContext.guestBeginAttemptCount ?? 0,
        guest_login_modal_cooldown_remaining_minutes: Math.ceil(modalDecision.cooldownRemainingMs / 60000),
      });
    }

    this.startExerciseFromContext(beginContext);
  }

  onGuestLoginModalSignIn(): void {
    const beginContext = this.pendingBeginExerciseContext;
    const returnUrl = this.getPostLoginReturnUrl();

    this.exerciseSessionTracking.trackEvent('guest_login_modal_login_clicked', {
      source: 'begin_exercise',
      return_url: returnUrl,
      guest_begin_attempt_count: beginContext?.guestBeginAttemptCount,
    }, {
      guest_begin_attempt_count: beginContext?.guestBeginAttemptCount ?? 0,
    });

    this.isGuestLoginModalOpen.set(false);
    this.pendingBeginExerciseContext = null;

    void this.router.navigate(['/auth/login'], {
      queryParams: {
        returnUrl,
        source: 'begin_exercise_modal',
      }
    });
  }

  onGuestLoginModalContinueAsGuest(): void {
    this.dismissGuestLoginModal('continue_as_guest');
  }

  onGuestLoginModalBackdropClick(): void {
    this.dismissGuestLoginModal('backdrop');
  }

  private dismissGuestLoginModal(reason: models.GuestLoginModalDismissReason): void {
    const beginContext = this.pendingBeginExerciseContext;

    this.exerciseSessionTracking.trackEvent('guest_login_modal_dismissed', {
      reason,
      source: 'begin_exercise',
      guest_begin_attempt_count: beginContext?.guestBeginAttemptCount,
    }, {
      guest_begin_attempt_count: beginContext?.guestBeginAttemptCount ?? 0,
    });

    this.isGuestLoginModalOpen.set(false);
    this.pendingBeginExerciseContext = null;

    if (beginContext) {
      this.startExerciseFromContext({
        ...beginContext,
        guestLoginModalShownBeforeStart: true,
      });
    }
  }

  private buildBeginExerciseContext(): models.BeginExerciseContext {
    return {
      isFirstTimeUser: this.isFirstTime(),
      audioEndedBeforeBegin: this.newsAudioComponentRef?.audioEnded ?? false,
      guestBeginAttemptCount: this.incrementGuestBeginAttemptCountIfGuest(),
      guestLoginModalShownBeforeStart: false,
    };
  }

  private evaluateGuestLoginModalDecision(context: models.BeginExerciseContext): models.GuestLoginModalDecision {
    if (this.authSessionStore.isAuthenticated()) {
      return {
        shouldShow: false,
        reason: 'authenticated',
        cooldownRemainingMs: 0,
      };
    }

    const guestAttempt = context.guestBeginAttemptCount ?? 0;
    if (guestAttempt < constants.guestBeginLoginModalAttemptThreshold) {
      return {
        shouldShow: false,
        reason: 'below_threshold',
        cooldownRemainingMs: 0,
      };
    }

    const cooldownRemainingMs = this.getGuestLoginModalCooldownRemainingMs(Date.now());
    if (cooldownRemainingMs > 0) {
      return {
        shouldShow: false,
        reason: 'cooldown_active',
        cooldownRemainingMs,
      };
    }

    return {
      shouldShow: true,
      reason: 'eligible',
      cooldownRemainingMs: 0,
    };
  }

  private openGuestLoginModal(context: models.BeginExerciseContext): void {
    this.pendingBeginExerciseContext = context;
    this.isGuestLoginModalOpen.set(true);
    this.browserService.setItem(constants.guestBeginLoginModalLastShownStorageKey, new Date().toISOString());

    this.exerciseSessionTracking.trackEvent('guest_login_modal_shown', {
      source: 'begin_exercise',
      guest_begin_attempt_count: context.guestBeginAttemptCount,
      is_first_time: context.isFirstTimeUser,
      audio_ended_before_begin: context.audioEndedBeforeBegin,
    }, {
      guest_begin_attempt_count: context.guestBeginAttemptCount ?? 0,
      guest_login_modal_cooldown_hours: constants.guestBeginLoginModalCooldownMs / (60 * 60 * 1000),
    });
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
        this.clearAutoPauseTimer();
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

    this.clearAutoPauseTimer();

    this.exerciseSessionTracking.trackEvent('exercise_started', {
      start_mode: 'direct_start',
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

  private incrementGuestBeginAttemptCountIfGuest(): number | null {
    if (this.authSessionStore.isAuthenticated()) {
      return null;
    }

    const currentAttempt = this.readPositiveIntFromStorage(constants.guestBeginAttemptCountStorageKey);
    const nextAttempt = currentAttempt + 1;
    this.browserService.setItem(constants.guestBeginAttemptCountStorageKey, String(nextAttempt));
    return nextAttempt;
  }

  private getGuestLoginModalCooldownRemainingMs(nowMs: number): number {
    const rawLastShown = this.browserService.getItem(constants.guestBeginLoginModalLastShownStorageKey);
    if (!rawLastShown) {
      return 0;
    }

    const lastShownMs = Date.parse(rawLastShown);
    if (!Number.isFinite(lastShownMs)) {
      return 0;
    }

    const elapsedMs = Math.max(0, nowMs - lastShownMs);
    return Math.max(0, constants.guestBeginLoginModalCooldownMs - elapsedMs);
  }

  private readPositiveIntFromStorage(key: string): number {
    const raw = this.browserService.getItem(key);
    if (!raw) {
      return 0;
    }

    const parsed = Number.parseInt(raw, 10);
    if (!Number.isFinite(parsed) || parsed < 0) {
      return 0;
    }

    return parsed;
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
      this.onSaveExerciseState();
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
    const proposition = this.proposition();
    if (!proposition) {
      this.exerciseSessionTracking.trackEvent('exercise_submit_failed', {
        reason: 'missing_proposition'
      });
      alert('Error processing your exercise. Please try again.');
      return;
    }

    if (!proposition.id) {
      this.exerciseSessionTracking.trackEvent('exercise_submit_failed', {
        reason: 'missing_proposition_id'
      });
      alert('Error processing your exercise. Please try again.');
      return;
    }

    const submittedUserText = this.exerciseSectionComponent.text();
    this.newsAudioComponentRef?.pauseAudio();
    this.isSubmitting.set(true);
    this.exerciseSessionTracking.markSubmitted();
    this.exerciseSessionTracking.trackTextChanged(submittedUserText);

    const submitConfirmedMetadata = this.buildSubmitTelemetryMetadata(
      submittedUserText,
      null,
      null
    );
    this.exerciseSessionTracking.trackEvent('exercise_submit_confirmed', {
      ...submitConfirmedMetadata.properties,
      text_snapshot: submittedUserText.slice(0, 1200),
      text_truncated: submittedUserText.length > 1200
    }, {
      ...submitConfirmedMetadata.measurements,
      text_char_count: submittedUserText.length,
      text_word_count: this.countWords(submittedUserText)
    });
    const submitRequestedAtMs = Date.now();
    const minLoadingTime = 2000; // 2 seconds minimum

    this.textComparisonsService.apiTextComparisonCompareTextsPost({
      propositionId: proposition.id,
      userText: submittedUserText
    }).subscribe({
      next: (result: TextComparisonResult) => {
        const apiElapsedMs = Date.now() - submitRequestedAtMs;
        const remainingTime = Math.max(0, minLoadingTime - apiElapsedMs);
        
        setTimeout(() => {
          const finalUserText = result.userText ?? submittedUserText;
          const finalOriginalText = result.originalText ?? null;
          const submitSuccessMetadata = this.buildSubmitTelemetryMetadata(
            finalUserText,
            finalOriginalText,
            result.accuracyPercentage
          );

          this.trackExerciseSubmitConversion(result.accuracyPercentage);
          this.exerciseSessionTracking.trackEvent('exercise_submit_succeeded', {
            ...submitSuccessMetadata.properties,
            comparison_count: result.comparisons?.length ?? 0,
          }, {
            ...submitSuccessMetadata.measurements,
            submit_api_latency_ms: apiElapsedMs,
            submit_flow_elapsed_ms: Date.now() - submitRequestedAtMs,
          });
          this.userText.set(submittedUserText);
          this.result.set(result);
          this.exerciseProgressTracking.trackComplete(proposition, result);
          this.onSaveExerciseState();
          this.setNewState('results');
          this.browserService.scrollToTop();
          this.isSubmitting.set(false);
        }, remainingTime);
      },
      error: (error) => {
        const apiElapsedMs = Date.now() - submitRequestedAtMs;
        const remainingTime = Math.max(0, minLoadingTime - apiElapsedMs);
        
        setTimeout(() => {
          const submitFailureMetadata = this.buildSubmitTelemetryMetadata(
            submittedUserText,
            null,
            null
          );

          this.exerciseSessionTracking.trackEvent('exercise_submit_failed', {
            ...submitFailureMetadata.properties,
            error: error?.message ?? 'unknown_error'
          }, {
            ...submitFailureMetadata.measurements,
            submit_api_latency_ms: apiElapsedMs,
            submit_flow_elapsed_ms: Date.now() - submitRequestedAtMs,
          });
          this.isSubmitting.set(false);
          if (this.isProRequiredError(error)) {
            this.openProUpgradeModal();
          } else {
            alert('Error processing your exercise. Please try again.');
          }
        }, remainingTime);
      }
    });
  }

  private isProRequiredError(error: unknown): boolean {
    return error instanceof HttpErrorResponse
      && error.status === 403
      && error.error?.access === constants.proRequiredAccess;
  }

  private trackExerciseSubmitConversion(accuracyPercentage: number | null | undefined): void {
    if (!Number.isFinite(accuracyPercentage) || (accuracyPercentage ?? 0) < 0.1) {
      return;
    }

    if (!this.browserService.isBrowserEnvironment()) {
      return;
    }

    const gtag = (globalThis as typeof globalThis & { gtag?: models.GtagEvent }).gtag;
    if (typeof gtag !== 'function') {
      return;
    }

    gtag('event', 'conversion', {
      send_to: constants.exerciseSubmitConversionSendTo,
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

    if (warnings.length === 0) {
      return null;
    }

    warnings.unshift('Goal: write as much of the full audio text as you can.');

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

    return audio.currentTime >= Math.max(0, duration - constants.submitAudioRemainingToleranceSeconds);
  }

  private countWords(text: string | null | undefined): number {
    return (text || '').trim().split(/\s+/).filter(Boolean).length;
  }

  private buildSubmitTelemetryMetadata(
    userText: string | null | undefined,
    originalText: string | null | undefined,
    accuracyPercentage: number | null | undefined
  ): {
    properties: Record<string, string | number | boolean>;
    measurements: Record<string, number>;
  } {
    const normalizedUserText = userText ?? '';
    const normalizedOriginalText = originalText ?? '';
    const userTextTelemetry = this.toTelemetryText(normalizedUserText);
    const originalTextTelemetry = this.toTelemetryText(normalizedOriginalText);
    const exerciseTimeUsedMs = this.getExerciseTimeUsedMs();

    const properties: Record<string, string | number | boolean> = {
      exercise_id: this.exerciseId ?? this.proposition()?.id ?? '',
      proposition_id: this.proposition()?.id ?? '',
      user_text: userTextTelemetry.text,
      user_text_truncated: userTextTelemetry.truncated,
      original_text: originalTextTelemetry.text,
      original_text_truncated: originalTextTelemetry.truncated,
      has_exercise_time_used: exerciseTimeUsedMs !== null,
    };

    const measurements: Record<string, number> = {
      user_text_char_count: normalizedUserText.length,
      user_text_word_count: this.countWords(normalizedUserText),
      original_text_char_count: normalizedOriginalText.length,
      original_text_word_count: this.countWords(normalizedOriginalText),
    };

    if (exerciseTimeUsedMs !== null) {
      measurements['exercise_time_used_ms'] = exerciseTimeUsedMs;
      measurements['exercise_time_used_seconds'] = Number((exerciseTimeUsedMs / 1000).toFixed(2));
    }

    if (typeof accuracyPercentage === 'number' && Number.isFinite(accuracyPercentage)) {
      measurements['accuracy_percentage'] = accuracyPercentage;
    }

    return {
      properties,
      measurements
    };
  }

  private toTelemetryText(text: string): { text: string; truncated: boolean } {
    if (text.length <= constants.submitTelemetryTextMaxLength) {
      return { text, truncated: false };
    }

    return {
      text: text.slice(0, constants.submitTelemetryTextMaxLength),
      truncated: true
    };
  }

  private getExerciseTimeUsedMs(): number | null {
    if (this.exerciseStartedAtMs === null) {
      return null;
    }

    return Math.max(0, Date.now() - this.exerciseStartedAtMs);
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

  shouldShowResultsLoginCta(): boolean {
    return this.exerciseState() === 'results'
      && !this.authSessionStore.isAuthenticated();
  }

  onSignInToSaveProgress(): void {
    const returnUrl = this.getPostLoginReturnUrl();
    this.setPendingCompletedSyncRequest(this.exerciseId);
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
    if (this.exerciseState() !== 'exercise') return;
    this.onSaveExerciseState();
    // When the user pauses via native controls, restore focus so shortcuts keep working.
    this.exerciseSectionComponent?.focusTextArea();
  }

  onAudioSeeked() {
    if (this.exerciseState() !== 'exercise') {
      return;
    }

    this.onSaveExerciseState();
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

  private markNextAudioPlaySource(source: models.AudioPlaySource): void {
    this.clearPendingAudioPlaySource();
    this.pendingAudioPlaySource = source;
    this.pendingAudioPlaySourceTimer = setTimeout(() => {
      this.pendingAudioPlaySource = null;
      this.pendingAudioPlaySourceTimer = null;
    }, 2000);
  }

  private consumePendingAudioPlaySource(): models.AudioPlaySource {
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

  onFeedbackSubmitted(submission: feedbackModal.FeedbackModalSubmission): void {
    const proposition = this.proposition();
    const feedbackEvent: ExerciseFeedbackEvent = {
      rating: submission.rating,
      tags: submission.tags,
      comment: submission.comment,
      exerciseId: String(proposition?.id ?? this.exerciseId ?? ''),
      difficulty: proposition?.complexityId ?? '',
      topic: proposition?.subjectId ?? '',
      sessionId: this.exerciseSessionTracking.getCurrentSessionId() ?? '',
      userId: this.authSessionStore.userId() ?? '',
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

  onFeedbackInteraction(event: feedbackModal.FeedbackModalInteractionEvent): void {
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
