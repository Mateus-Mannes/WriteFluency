import { Component, ElementRef, EventEmitter, Output, Renderer2, ViewChild } from '@angular/core';
import { ListenAndWriteService } from '../listen-and-write.service';
import { AlertService } from 'src/app/shared/services/alert-service';
import { DropDownComponent } from 'src/app/shared/drop-down/drop-down.component';
import { Subject, forkJoin, take, takeUntil, timer } from 'rxjs';

@Component({
  selector: 'app-proposition',
  templateUrl: './proposition.component.html',
  styleUrls: ['./proposition.component.css']
})
export class PropositionComponent {

  constructor(
    private readonly _service: ListenAndWriteService,
    private readonly _renderer: Renderer2,
    private readonly _alertService: AlertService) { }

  @ViewChild('complexity') complexity!: DropDownComponent;
  @ViewChild('subject') subject!: DropDownComponent;
  @ViewChild('progress') progress!: ElementRef;
  @ViewChild('progressBar') progressBar!: ElementRef;
  @ViewChild('audioPlayer') audioPlayer!: ElementRef;
  @Output() audioPlayOrPause = new EventEmitter<void>();

  complexities: string[] = [];
  subjects: string[] = [];
  loadingAudio = false;
  propositionText = '';

  ngOnInit() {
    this._service.getTopics()
      .subscribe({
        next: result => {
          this.complexities = result.complexities;
          this.subjects = result.subjects;
        },
        error: () => {
          this._alertService.alert('Was not possible to load the page. Please try again later', 'danger');
        }
      });
  }

  ngAfterViewInit() {
    this.audioPlayer.nativeElement.onplay = () => { this.audioPlayOrPause.emit(); };
    this.audioPlayer.nativeElement.onpause  = () => { this.audioPlayOrPause.emit(); };
  }

  generateAudio() {
    this.resetAudio();

    const progress = this.startProgress();
    const post$ = this._service.generateProposition(this.complexity.selectedOption, this.subject.selectedOption);

    forkJoin([progress.timer$, post$]).subscribe({
      next: ([_, result]) => {
        this._renderer.setStyle(this.progress.nativeElement, 'width', `100%`);
        this.audioPlayer.nativeElement.src = 'data:audio/ogg;base64,' + result.audio;
        this.propositionText = result.text;
      },
      error: (error) => {
        this._alertService.alert('Was not possible to generate the text. Please try again later', 'danger');
        progress.stop$.next();
        this._renderer.setStyle(this.progress.nativeElement, 'width', `0%`);
        this.loadingAudio = false;
      },
      complete: () => {
        setTimeout(() => {
          this.progressBar.nativeElement.hidden = true;
          this.audioPlayer.nativeElement.hidden = false;
          this.loadingAudio = false;
        }, 1500);
      }
    });
  }

  resetAudio() {
    this.loadingAudio = true;
    this.propositionText = '';

    // Start the progress bar animation
    this._renderer.setStyle(this.progress.nativeElement, 'width', `0%`);
    this.progressBar.nativeElement.hidden = false;
    this.audioPlayer.nativeElement.hidden = true;
    this.audioPlayer.nativeElement.src = '';
  }

  startProgress() {
    const duration = 10; // duration in seconds
    const interval = 100; // update interval in milliseconds
    const steps = duration * 1000 / interval;
    const increment = 100 / steps;

    const stop$ = new Subject<void>();
    const timer$ = timer(0, interval).pipe(take(steps - 10), takeUntil(stop$));

    let width = 0;
    timer$.subscribe(() => {
      width += increment;
      this._renderer.setStyle(this.progress.nativeElement, 'width', `${width}%`);
    });

    return { timer$, stop$ }
  }

}
