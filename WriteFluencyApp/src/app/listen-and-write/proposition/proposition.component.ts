import { Component } from '@angular/core';
import { ListenAndWriteService } from '../listen-and-write.service';

@Component({
  selector: 'app-proposition',
  templateUrl: './proposition.component.html',
  styleUrls: ['./proposition.component.css']
})
export class PropositionComponent {

  constructor(
    private readonly _service: ListenAndWriteService,
    private readonly _renderer: Renderer2,
    private readonly _alertService: AlertService) 
    { 

    }

  complexities: string[] = [];
  subjects: string[] = [];
  loadingAudio = false;
  propositionText = '';

  ngOnInit() {
    this._service.getTopics()
      .subscribe(
        result  => {
          this.complexities = result.complexities;
          this.subjects = result.subjects;
        },
        error => {
          this._alertService.alert('Error. Please try again later', 'danger')
        }
        );
  }

  generateAudio() {

    this.loadingAudio = true;
    this.originalText = '';
    let complexity = this.complexity.selectedOption;
    let subject = this.subject.selectedOption; 

    // Start the progress bar animation
    this._renderer.setStyle(this.progress.nativeElement, 'width', `0%`);
    this.progressBar.nativeElement.hidden = false;
    this.audioPlayer.nativeElement.hidden = true;
    this.audioPlayer.nativeElement.src = '';

    const duration = 10; // duration in seconds
    const interval = 100; // update interval in milliseconds
    const steps = duration * 1000 / interval;
    const increment = 100 / steps;
    let width = 0;

    const stop$ = new Subject<void>();
    const timer$ = timer(0, interval).pipe(take(steps - 10), takeUntil(stop$));
    const post$ = this._httpClient.post<Proposition>(`${this.route}/generate-proposition`, {complexity, subject});

    timer$.subscribe(() => {
      width += increment;
      this._renderer.setStyle(this.progress.nativeElement, 'width', `${width}%`);
    });

    forkJoin([timer$, post$]).subscribe({
      next: ([_, result]) => {
        width += increment*10;
        this._renderer.setStyle(this.progress.nativeElement, 'width', `${width}%`);
        this.audioPlayer.nativeElement.src = 'data:audio/ogg;base64,' + result.audio;
        this.originalText = result.text;
      },
      error: (error) => {
        this._alertService.alert('Was not possible to generate the text. Please try again later', 'danger');
        stop$.next();
        this._renderer.setStyle(this.progress.nativeElement, 'width', `0%`);
        this.loadingAudio = false;
        this.generateAudioLabel = 'New audio'
      },
      complete: () => {
        setTimeout(() => {
          this.progressBar.nativeElement.hidden = true;
          this.audioPlayer.nativeElement.hidden = false;
          this.loadingAudio = false;
          this.generateAudioLabel = 'New audio'
        }, 1500);
      }
    });
  }

}
