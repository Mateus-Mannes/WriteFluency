import { HttpClient } from "@angular/common/http";
import { environment } from "src/enviroments/enviroment";
import { Topics } from "./entities/topics";
import { Injectable } from "@angular/core";
import { Proposition } from "./entities/proposition";

@Injectable()
export class ListenAndWriteService {
    
    constructor(private readonly _httpClient: HttpClient) { }

    route = `${environment.apiUrl}/listen-and-write`;

    getTopics() {
        return this._httpClient.get<Topics>(`${this.route}/topics`);
    }

    GenerateProposition(complexity: string, subject: string) {
        return this._httpClient.post<Proposition>(`${this.route}/generate-proposition`, {complexity, subject});
    }
}