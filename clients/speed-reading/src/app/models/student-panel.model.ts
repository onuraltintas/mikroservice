// Series Access Models
export interface SeriesAccessDto {
  seriesId: string;
  seriesName: string;
  seriesTitle: string;
  description: string;
  sequenceOrder: number;
  isUnlocked: boolean;
  canUnlock: boolean;
  lockReason?: string;
  prerequisiteSeriesId?: string;
  prerequisiteSeriesTitle?: string;
  prerequisiteSeriesName?: string;
  prerequisiteCompletionPercentage?: number;
  requiredCompletionPercentage?: number;
  totalDays: number;
  estimatedDurationDays: number;
  minimumLevel: string;
  maximumLevel: string;
}

export interface PrerequisiteCheckResult {
  isMet: boolean;
  message: string;
  requiredSeriesId?: string;
  requiredSeriesName?: string;
  requiredCompletionPercentage: number;
  currentCompletionPercentage: number;
}

export interface UnlockSeriesResult {
  success: boolean;
  message: string;
  unlockedSeriesId?: string;
}

// Review Models
export interface ReviewExerciseDto {
  reviewItemId: string;
  exerciseId: string;
  exerciseTitle: string;
  exerciseDescription: string;
  difficultyLevel: number;
  seriesName: string;
  exerciseType: string;
  seriesTitle: string;
  firstCompletedAt: Date;
  lastReviewedAt?: Date;
  nextReviewDate: Date;
  reviewCount: number;
  currentIntervalDays: number;
  easinessFactor: number;
  daysOverdue: number;
  lastReviewScore: number;
  averageReviewScore: number;
  isMastered: boolean;
  isOverdue: boolean;
}

export interface ReviewStatisticsDto {
  totalReviewItems: number;
  totalInReviewQueue: number;
  dueToday: number;
  totalDueToday: number;
  overdue: number;
  totalOverdue: number;
  reviewedToday: number;
  masteredExercises: number;
  masteredCount: number;
  dueThisWeek: number;
  averageReviewScore: number;
  averageInterval: number;
  currentStreak: number;
  totalCompletedReviews: number;
  nextReviewDate?: Date;
  lastReviewDate?: Date;
}

export interface SubmitReviewResult {
  success: boolean;
  message: string;
  nextReviewDate: Date;
  intervalDays: number;
  isMastered: boolean;
  easinessFactor: number;
}

export interface ReviewHistoryDto {
  reviewedAt: Date;
  score: number;
  intervalDays: number;
  reviewNumber: number;
}

// Assessment Models
export interface AssessmentExerciseItemDto {
  id: string;
  typeName: string;
  name: string;
  description: string;
  isCompleted: boolean;
}

export interface AssessmentExercisesDto {
  comprehensionExerciseId: string;
  visualExpansionExerciseId: string;
  focusExerciseId: string;
  comprehensionCompleted: boolean;
  visualExpansionCompleted: boolean;
  focusCompleted: boolean;
  message: string;
  exercises?: AssessmentExerciseItemDto[];
}

export interface AssessmentResultDto {
  recommendedLevel: string;
  averageScore: number;
  comprehensionScore: number;
  tachistoscopeScore: number;
  visualExpansionScore: number;
  fixationScore: number;
  focusScore: number; // Deprecated - kept for backward compatibility
  recommendedSeriesId?: string;
  recommendedProgressId?: string;
  recommendedSeriesName: string;
  message: string;
}
