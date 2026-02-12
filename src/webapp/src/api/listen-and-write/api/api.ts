export * from './authentication.service';
import { AuthenticationService } from './authentication.service';
export * from './propositions.service';
import { PropositionsService } from './propositions.service';
export * from './textComparisons.service';
import { TextComparisonsService } from './textComparisons.service';
export const APIS = [AuthenticationService, PropositionsService, TextComparisonsService];
