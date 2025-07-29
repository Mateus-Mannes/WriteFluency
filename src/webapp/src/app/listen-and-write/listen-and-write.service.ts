import { HttpClient } from "@angular/common/http";
import { environment } from "src/enviroments/enviroment";
import { Topics } from "./entities/topics";
import { Injectable } from "@angular/core";
import { Proposition } from "./entities/proposition";
import { TextComparision } from "./entities/text-comparision";

@Injectable()
export class ListenAndWriteService {
    
    constructor(private readonly _httpClient: HttpClient) { }

    propositionRoute = `${environment.apiUrl}/proposition`;
    textComparisonRoute = `${environment.apiUrl}/text-comparison`;

    getTopics() {
        return this._httpClient.get<Topics>(`${this.propositionRoute}/topics`);
    }

    generateProposition(complexity: string, subject: string) {
        return this._httpClient.post<Proposition>(`${this.propositionRoute}/generate-proposition`, {complexity, subject});
    }

    compareTexts(originalText: string, userText: string) {
        return this._httpClient.post<TextComparision[]>(`${this.textComparisonRoute}/compare-texts`, {originalText, userText});
    }
}