import { Component, ViewChild, ElementRef, output, input, signal, OnDestroy, computed } from '@angular/core';
import { Proposition } from 'src/api/listen-and-write';
import { environment } from 'src/enviroments/enviroment';

@Component({
  selector: 'app-news-audio',
  imports: [],
  templateUrl: './news-audio.component.html',
  styleUrl: './news-audio.component.scss',
})
export class NewsAudioComponent implements OnDestroy {

  proposition = input<Proposition | null>();

  audioUrl = computed(() => {
    if (!this.proposition()) {
      return '';
    }
    return environment.minioUrl + '/propositions/' + this.proposition()?.audioFileId;
  });

  constructor() {
    // Auto-pause when tab is hidden
    document.addEventListener('visibilitychange', this.handleVisibilityChange);
  }

  handleVisibilityChange = () => {
    if (document.hidden) {
      this.pauseAudio();
    }
  };
  ngOnDestroy() {
    // Clean up event listener
    document.removeEventListener('visibilitychange', this.handleVisibilityChange);
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
    this.audioRef?.nativeElement.play();
  }

  pauseAudio() {
    this.audioRef?.nativeElement.pause();
  }

  rewindAudio(seconds: number) {
    const audio = this.audioRef?.nativeElement;
    if (audio) {
      audio.currentTime = Math.max(0, audio.currentTime - seconds);
    }
  }

  forwardAudio(seconds: number) {
    const audio = this.audioRef?.nativeElement;
    if (audio) {
      audio.currentTime = Math.min(audio.duration || audio.currentTime + seconds, audio.currentTime + seconds);
    }
  }
}
