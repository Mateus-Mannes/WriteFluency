import { ComparisonSnapshot } from './comparisonSnapshot';

export interface CorrectionStageTrace {
    action?: string | null;
    reasonCode?: string | null;
    output?: Array<ComparisonSnapshot> | null;
    validationStatus?: string | null;
    proposedOutput?: Array<ComparisonSnapshot> | null;
    validationFailureReason?: string | null;
}
