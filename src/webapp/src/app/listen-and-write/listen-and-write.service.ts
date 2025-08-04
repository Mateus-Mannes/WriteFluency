import { HttpClient } from "@angular/common/http";
import { environment } from "src/enviroments/enviroment";
import { Topics } from "./entities/topics";
import { Injectable } from "@angular/core";
import { TextComparision } from "./entities/text-comparision";
import { Proposition } from "./entities/proposition";
import { tap } from "rxjs";

@Injectable()
export class ListenAndWriteService {
  constructor(private readonly _httpClient: HttpClient) {}

  propositionRoute = `${environment.apiUrl}/proposition`;
  textComparisonRoute = `${environment.apiUrl}/text-comparison`;

  private readonly STORAGE_KEY = 'generated_proposition_ids';

  private getStoredIds(): number[] {
    const data = localStorage.getItem(this.STORAGE_KEY);
    try { return data ? JSON.parse(data) : []; } catch { return []; }
  }

  private storeId(id: number) {
    const ids = this.getStoredIds();
    if (!ids.includes(id)) {
      ids.push(id);
      localStorage.setItem(this.STORAGE_KEY, JSON.stringify(ids));
    }
  }

  getTopics() {
    return this._httpClient.get<Topics>(`${this.propositionRoute}/topics`);
  }

  generateProposition(complexity: string, subject: string) {
    const alreadyGeneratedIds = this.getStoredIds();

    return this._httpClient
      .post<Proposition>(`${this.propositionRoute}/generate-proposition`, {
        complexity,
        subject,
        alreadyGeneratedIds
      })
      .pipe(
        tap((prop) => {
          if (prop?.propositionInfo.id) this.storeId(prop.propositionInfo.id);
        })
      );
  }

  compareTexts(originalText: string, userText: string) {
    return this._httpClient.post<TextComparision[]>(
      `${this.textComparisonRoute}/compare-texts`,
      { originalText, userText }
    );
  }
}
