import { environment } from '../enviroments/enviroment';


export function shouldUseAppInsights(): boolean {
    return environment.production == true || !!environment.instrumentationKey;
}