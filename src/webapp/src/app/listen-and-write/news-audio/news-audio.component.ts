import { Component, ViewChild, ElementRef, output, input, signal, OnDestroy, computed, PLATFORM_ID, inject, effect, HostListener } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Proposition } from 'src/api/listen-and-write';
import { environment } from 'src/enviroments/enviroment';

@Component({
  selector: 'app-news-audio',
  imports: [],
  templateUrl: './news-audio.component.html',
  styleUrl: './news-audio.component.scss',
})
export class NewsAudioComponent implements OnDestroy {
  private platformId = inject(PLATFORM_ID);
  private isBrowser = isPlatformBrowser(this.platformId);

  proposition = input<Proposition | null>();

  audioUrl = computed(() => {
    if (!this.proposition() || !this.proposition()?.audioFileId) {
      return '';
    }
    return environment.minioUrl + '/propositions/' + this.proposition()?.audioFileId;
  });
  isAudioLoading = signal(false);
  currentTime = signal(0);
  duration = signal(0);
  isMenuOpen = signal(false);
  isSpeedMenuOpen = signal(false);
  playbackRate = signal(1);
  progressPercent = computed(() => {
    const totalDuration = this.duration();
    if (!totalDuration || totalDuration <= 0) return 0;
    return Math.max(0, Math.min(100, (this.currentTime() / totalDuration) * 100));
  });
  playbackRates = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 2];

  private readonly resetAudioLoading = effect(() => {
    const url = this.audioUrl();
    this.isAudioLoading.set(!!url);
    this.currentTime.set(0);
    this.duration.set(0);
    this.playbackRate.set(1);
    this.isMenuOpen.set(false);
    this.isSpeedMenuOpen.set(false);
  });

  ngOnDestroy() {
    // Auto-pause when component is destroyed
    this.pauseAudio();
  }
  audioPlayed = false;
  audioEnded = false;
  isAudioPlaying = signal(false);

  @ViewChild('audioRef') audioRef!: ElementRef<HTMLAudioElement>;
  @ViewChild('menuContainer') menuContainer?: ElementRef<HTMLElement>;

  playClicked = output();

  paused = output();

  listenSuggestion = input<boolean>(true);

  onPlay() {
    this.audioPlayed = true;
    this.isAudioPlaying.set(true);
    this.playClicked.emit();
  }

  onLoadStart() {
    if (this.audioUrl()) {
      this.isAudioLoading.set(true);
    }
  }

  onLoadedData() {
    this.isAudioLoading.set(false);
  }

  onLoadedMetadata() {
    const audio = this.audioRef?.nativeElement;
    this.duration.set(audio?.duration || 0);
    this.currentTime.set(audio?.currentTime || 0);
    if (audio) {
      audio.playbackRate = this.playbackRate();
    }
  }

  onTimeUpdate() {
    const audio = this.audioRef?.nativeElement;
    this.currentTime.set(audio?.currentTime || 0);
  }

  onCanPlay() {
    this.isAudioLoading.set(false);
  }

  onAudioError() {
    this.isAudioLoading.set(false);
    this.isMenuOpen.set(false);
    this.isSpeedMenuOpen.set(false);
  }

  onPause() {
    this.isAudioPlaying.set(false);
    this.paused.emit();
  }

  onEnded() {
    this.audioEnded = true;
    this.isAudioPlaying.set(false);
    this.currentTime.set(this.duration());
  }

  togglePlayPause() {
    if (!this.isBrowser) return;
    const audio = this.audioRef?.nativeElement;
    if (!audio) return;

    if (audio.paused) {
      void audio.play();
      return;
    }

    audio.pause();
  }

  onSeek(event: Event) {
    if (!this.isBrowser) return;
    const audio = this.audioRef?.nativeElement;
    const target = event.target as HTMLInputElement;
    if (!audio || !target) return;

    const nextValue = Number(target.value);
    audio.currentTime = Number.isFinite(nextValue) ? nextValue : audio.currentTime;
    this.currentTime.set(audio.currentTime);
  }

  toggleMenu(event: MouseEvent) {
    event.stopPropagation();
    const nextOpenState = !this.isMenuOpen();
    this.isMenuOpen.set(nextOpenState);
    if (!nextOpenState) {
      this.isSpeedMenuOpen.set(false);
    }
  }

  toggleSpeedMenu() {
    this.isSpeedMenuOpen.update((isOpen) => !isOpen);
  }

  setPlaybackSpeed(rate: number) {
    this.playbackRate.set(rate);
    const audio = this.audioRef?.nativeElement;
    if (audio) {
      audio.playbackRate = rate;
    }
    this.isSpeedMenuOpen.set(false);
    this.isMenuOpen.set(false);
  }

  formatPlaybackRate(rate: number) {
    return `${Number.isInteger(rate) ? rate.toFixed(0) : rate.toFixed(2).replace(/0$/, '')}x`;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    if (!this.isBrowser || !this.isMenuOpen()) return;
    const target = event.target as Node | null;
    if (!target) return;

    const menuRoot = this.menuContainer?.nativeElement;
    if (!menuRoot?.contains(target)) {
      this.isMenuOpen.set(false);
      this.isSpeedMenuOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.isMenuOpen.set(false);
    this.isSpeedMenuOpen.set(false);
  }

  playAudio() {
    if (!this.isBrowser) return;
    this.audioRef?.nativeElement?.play();
  }

  pauseAudio() {
    if (!this.isBrowser) return;
    this.audioRef?.nativeElement?.pause();
  }

  rewindAudio(seconds: number) {
    if (!this.isBrowser) return;
    const audio = this.audioRef?.nativeElement;
    if (audio) {
      audio.currentTime = Math.max(0, audio.currentTime - seconds);
      this.currentTime.set(audio.currentTime);
    }
  }

  forwardAudio(seconds: number) {
    if (!this.isBrowser) return;
    const audio = this.audioRef?.nativeElement;
    if (audio) {
      audio.currentTime = Math.min(audio.duration || audio.currentTime + seconds, audio.currentTime + seconds);
      this.currentTime.set(audio.currentTime);
    }
  }

  formatTime(seconds: number) {
    if (!Number.isFinite(seconds) || seconds <= 0) return '0:00';
    const wholeSeconds = Math.floor(seconds);
    const mins = Math.floor(wholeSeconds / 60);
    const secs = String(wholeSeconds % 60).padStart(2, '0');
    return `${mins}:${secs}`;
  }
}
