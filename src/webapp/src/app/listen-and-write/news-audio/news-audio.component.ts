import { Component, ViewChild, ElementRef, Output, EventEmitter } from '@angular/core';

@Component({
  selector: 'app-news-audio',
  imports: [],
  templateUrl: './news-audio.component.html',
  styleUrl: './news-audio.component.scss',
})
export class NewsAudioComponent {
  audioPlayed = false;
  audioEnded = false;

  @ViewChild('audioRef') audioRef!: ElementRef<HTMLAudioElement>;

  @Output() playClicked = new EventEmitter<void>();

  onPlay() {
    this.audioPlayed = true;
    this.playClicked.emit();
  }

  onEnded() {
    this.audioEnded = true;
  }

  playAudio() {
    this.audioRef?.nativeElement.play();
  }

  pauseAudio() {
    this.audioRef?.nativeElement.pause();
  }
}
