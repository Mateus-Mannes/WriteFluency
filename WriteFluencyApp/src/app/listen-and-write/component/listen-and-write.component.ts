import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from 'src/enviroments/enviroment';
import { Topics } from '../entities/topics';

@Component({
  selector: 'app-listen-and-write',
  templateUrl: './listen-and-write.component.html',
  styleUrls: ['./listen-and-write.component.css']
})
export class ListenAndWriteComponent {

  constructor(
    private readonly _httpClient: HttpClient,
  ) {}

  private readonly route = `${environment.apiUrl}/listen-and-write`;
  complexities: string[] = [];
  subjects: string[] = [];
  
  ngOnInit() {
    this._httpClient.get<Topics>(`${this.route}/topics`)
      .subscribe(result  => {
        this.complexities = result.complexities;
        this.subjects = result.subjects;
      });
  }
}
