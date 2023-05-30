import { Component } from '@angular/core';
import { ListenAndWriteService } from '../listen-and-write.service';

@Component({
  selector: 'app-proposition',
  templateUrl: './proposition.component.html',
  styleUrls: ['./proposition.component.css']
})
export class PropositionComponent {

  constructor(private readonly _service: ListenAndWriteService) { }

  complexities: string[] = [];
  subjects: string[] = [];
  loadingAudio = false;

  ngOnInit() {
    this._service.getTopics()
      .subscribe(result  => {
        this.complexities = result.complexities;
        this.subjects = result.subjects;
      });
  }

  generateAudio() {

  }

}
