import { Component, ElementRef, OnInit, Renderer2, ViewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from 'src/enviroments/enviroment';
import { Topics } from '../entities/topics';
import { DropDownComponent } from 'src/app/shared/drop-down/drop-down.component';
import { Proposition } from '../entities/proposition';
import { forkJoin, take, timer } from 'rxjs';
import { trigger, state, style, animate, transition } from '@angular/animations';
import { AlertService } from '../../shared/services/alert-service';

@Component({
  selector: 'app-listen-and-write',
  templateUrl: './listen-and-write.component.html',
  styleUrls: ['./listen-and-write.component.css'],
  animations: [
    trigger('fadeIn', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('600ms', style({ opacity: 1 })),
      ]),
    ]),
  ]
})
export class ListenAndWriteComponent implements OnInit {

  constructor(
    private readonly _httpClient: HttpClient,
    private readonly _renderer: Renderer2,
    private readonly _alertService: AlertService
  ) {}

  private readonly route = `${environment.apiUrl}/listen-and-write`;
  @ViewChild('complexity') complexity!: DropDownComponent;
  @ViewChild('subject') subject!: DropDownComponent;
  @ViewChild('audioPlayer') audioPlayer!: ElementRef;
  @ViewChild('progress') progress!: ElementRef;
  @ViewChild('progressBar') progressBar!: ElementRef;
  complexities: string[] = [];
  subjects: string[] = [];
  loadingPage: boolean = true;
  loadingAudio: boolean = false;
  generateAudioLabel = 'New audio';
  
  ngOnInit() {
    this._httpClient.get<Topics>(`${this.route}/topics`)
      .subscribe(result  => {
        this.complexities = result.complexities;
        this.subjects = result.subjects;
        this.loadingPage = false;
      });
  }

  generateAudio(){
    this.loadingAudio = true;
    this.generateAudioLabel = 'Generating';
    let complexity = this.complexity.selectedOption;
    let subject = this.subject.selectedOption;

    // Start the progress bar animation
    this._renderer.setStyle(this.progress.nativeElement, 'width', `0%`);
    this.progressBar.nativeElement.hidden = false;
    this.audioPlayer.nativeElement.hidden = true;
    this.audioPlayer.nativeElement.src = undefined;

    const duration = 10; // duration in seconds
    const interval = 100; // update interval in milliseconds
    const steps = duration * 1000 / interval;
    const increment = 100 / steps;
    let width = 0;

    const timer$ = timer(0, interval).pipe(take(steps - 10));
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
      },
      error: (error) => {
        this._alertService.alert('Was not possible to generate the text. Please try again later', 'danger');
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
