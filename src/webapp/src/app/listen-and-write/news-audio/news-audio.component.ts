import { Component, ViewChild, ElementRef, output, input, signal, OnDestroy, computed, PLATFORM_ID, inject } from '@angular/core';
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
    if (!this.proposition()) {
      return '';
    }
    return environment.minioUrl + '/propositions/' + this.proposition()?.audioFileId;
  });

  ngOnDestroy() {
    // Auto-pause when component is destroyed
    this.pauseAudio();
  }
  audioPlayed = false;
  audioEnded = false;
  isAudioPlaying = signal(false);

  @ViewChild('audioRef') audioRef!: ElementRef<HTMLAudioElement>;

  playClicked = output();

  paused = output();

  listenSuggestion = input<boolean>(true);

  onPlay() {
    this.audioPlayed = true;
    this.isAudioPlaying.set(true);
    this.playClicked.emit();
  }

  onPause() {
    this.isAudioPlaying.set(false);
    this.paused.emit();
  }

  onEnded() {
    this.audioEnded = true;
    this.isAudioPlaying.set(false);
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
    }
  }

  forwardAudio(seconds: number) {
    if (!this.isBrowser) return;
    const audio = this.audioRef?.nativeElement;
    if (audio) {
      audio.currentTime = Math.min(audio.duration || audio.currentTime + seconds, audio.currentTime + seconds);
    }
  }
}
