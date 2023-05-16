import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from 'src/enviroments/enviroment';
import { Topics } from '../entities/topics';
import { DropDownComponent } from 'src/app/shared/drop-down/drop-down.component';
import { Proposition } from '../entities/proposition';

@Component({
  selector: 'app-listen-and-write',
  templateUrl: './listen-and-write.component.html',
  styleUrls: ['./listen-and-write.component.css']
})
export class ListenAndWriteComponent implements OnInit {

  constructor(
    private readonly _httpClient: HttpClient,
  ) {}

  private readonly route = `${environment.apiUrl}/listen-and-write`;
  @ViewChild('complexity') complexity!: DropDownComponent;
  @ViewChild('subject') subject!: DropDownComponent;
  @ViewChild('audioPlayer') audioPlayer!: ElementRef;
  complexities: string[] = [];
  subjects: string[] = [];
  
  ngOnInit() {
    this._httpClient.get<Topics>(`${this.route}/topics`)
      .subscribe(result  => {
        this.complexities = result.complexities;
        this.subjects = result.subjects;
      });
  }

  generateAudio(){
    let complexity = this.complexity.selectedOption;
    let subject = this.subject.selectedOption;
    this._httpClient.post<Proposition>(`${this.route}/generate-proposition`, {complexity, subject})
      .subscribe(result => {
        this.audioPlayer.nativeElement.src = 'data:audio/ogg;base64,' + result.audio;
      });
  }
}
