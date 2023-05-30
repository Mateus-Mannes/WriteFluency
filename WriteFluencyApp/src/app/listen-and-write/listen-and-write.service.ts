import { HttpClient } from "@angular/common/http";
import { environment } from "src/enviroments/enviroment";
import { Topics } from "./entities/topics";
import { Injectable } from "@angular/core";

@Injectable()
export class ListenAndWriteService {
    
    constructor(private readonly _httpClient: HttpClient) { }

    route = `${environment.apiUrl}/listen-and-write`;

    getTopics() {
        return this._httpClient.get<Topics>(`${this.route}/topics`);
    }
}