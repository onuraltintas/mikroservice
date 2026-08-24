export interface ReadingBaselineDto {
    hasBaseline: boolean;
    averageWPM: number;
    bestWPM: number;
    averageComprehension: number;
    recommendedRateMs: number;
    sessionCount: number;
    lastSessionDate?: Date;
}
