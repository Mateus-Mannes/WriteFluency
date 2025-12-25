import { Component, ViewChild, ElementRef, output, input, signal } from '@angular/core';

@Component({
  selector: 'app-news-audio',
  imports: [],
  templateUrl: './news-audio.component.html',
  styleUrl: './news-audio.component.scss',
})
export class NewsAudioComponent {
  audioPlayed = false;
  audioEnded = false;
  isAudioPlaying = signal(false);

  @ViewChild('audioRef') audioRef!: ElementRef<HTMLAudioElement>;

  playClicked = output();

  listenSuggestion = input<boolean>(true);

  onPlay() {
    this.audioPlayed = true;
    this.isAudioPlaying.set(true);
    this.playClicked.emit();
  }

  onPause() {
    this.isAudioPlaying.set(false);
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
}
