import { ComparisonSnapshot } from './comparisonSnapshot';
import { CorrectionStageTrace } from './correctionStageTrace';

export interface CorrectionTraceEntry {
    sourceComparisonIndex?: number;
    initial?: ComparisonSnapshot;
    deterministic?: CorrectionStageTrace | null;
    ai?: CorrectionStageTrace | null;
}
