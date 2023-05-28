import { Component, ElementRef, OnInit, Renderer2, ViewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from 'src/enviroments/enviroment';
import { Topics } from '../entities/topics';
import { DropDownComponent } from 'src/app/shared/drop-down/drop-down.component';
import { Proposition } from '../entities/proposition';
import { Subject, forkJoin, take, takeUntil, timer } from 'rxjs';
import { trigger, state, style, animate, transition } from '@angular/animations';
import { AlertService } from '../../shared/services/alert-service';
import { TextComparision } from '../entities/text-comparision';
import { TextPart } from '../entities/text-part';

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
  @ViewChild('textarea') textArea!: ElementRef;
  @ViewChild('highlighter') highlighter!: ElementRef;
  complexities: string[] = [];
  subjects: string[] = [];
  loadingPage: boolean = true;
  loadingVerify: boolean = false;
  loadingAudio: boolean = false;
  generateAudioLabel = 'New audio';
  originalText: string = '';
  textParts: TextPart[] = [];
  
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
    this.textArea.nativeElement.readOnly = false;
    this.textArea.nativeElement.value = '';
    this.highlighter.nativeElement.hidden = true;
    this._renderer.setStyle(this.highlighter.nativeElement, 'pointer-events', `none`);
    let complexity = this.complexity.selectedOption;
    let subject = this.subject.selectedOption;

    // Start the progress bar animation
    this._renderer.setStyle(this.progress.nativeElement, 'width', `0%`);
    this.progressBar.nativeElement.hidden = false;
    this.audioPlayer.nativeElement.hidden = true;
    this.audioPlayer.nativeElement.src = null;

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

  VerifyText()
  {
    this.HighlightText();
    this.loadingVerify = true;
    let userText = this.textArea.nativeElement.value;

    if(userText.length/this.originalText.length < 0.6)
    {
      this._alertService.alert('The text is too short. Try to transcribe at least 70% of the audio, write the words as you think they are. You can also generate other audio', 'warning', 20000);
      this.loadingVerify = false;
      return;
    }

    let originalText = this.originalText;
    this.textArea.nativeElement.readOnly = true;
    this._httpClient.post<TextComparision[]>(`${this.route}/compare-texts`, {originalText, userText})
      .subscribe(
        result => 
        { 
          this.loadingVerify = false;          
        },
        error =>
        {
          this._alertService.alert('Was not possible to verify the text. Please try again later', 'danger');
          this.loadingVerify = false;
        });
  }

  HighlightText()
  {
    var text = this.textArea.nativeElement.value;
    this._renderer.setStyle(this.highlighter.nativeElement, 'pointer-events', `all`);

    let highlights = [{start: 7, end: 13, correction: 'universe'}, {start: 18, end: 22, correction: 'another'}];
    
    let lastEnd = 0;

    for (let highlight of highlights) {
      let { start, end, correction } = highlight;
      this.textParts.push({ 
        text: text.slice(lastEnd, start), highlight: false });
      this.textParts.push({ text: text.slice(start, end), highlight: true, correction });
      lastEnd = end;
    }

    this.textParts.push({ text: text.slice(lastEnd), highlight: false });

  }
}
