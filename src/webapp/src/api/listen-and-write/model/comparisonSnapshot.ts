import { TextRange } from './textRange';

export interface ComparisonSnapshot {
    originalTextRange?: TextRange;
    originalText?: string | null;
    userTextRange?: TextRange;
    userText?: string | null;
}
