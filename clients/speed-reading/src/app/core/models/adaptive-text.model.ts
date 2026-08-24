/**
 * Adaptive text recommendation with scoring details
 */
export interface AdaptiveTextRecommendation {
  textId: string;
  title: string;
  content: string;
  category: string;
  difficultyLevel: number;
  wordCount: number;
  estimatedReadingTimeMinutes: number;
  tags: string[];
  recommendedMinLevel: number;
  recommendedMaxLevel: number;
  averageComprehensionScore: number;
  timesRead: number;
  totalScore: number;
  confidenceScore: number;
  scoreBreakdown: { [key: string]: number };
  reasoning: string;
}

/**
 * Student reading profile for adaptive text selection
 */
export interface StudentReadingProfile {
  studentId: string;
  currentReadingLevel: number;
  averageComprehensionScore: number;
  averageReadingSpeed: number;
  totalTextsRead: number;
  totalReadingTimeSeconds: number;
  preferredCategories: string[];
  difficultCategories: string[];
  lastCalculatedAt: Date;
}

/**
 * Request to update student profile after reading
 */
export interface UpdateProfileRequest {
  studentId: string;
  readingTextId: string;
  comprehensionScore: number;
  readingTimeSeconds: number;
  readingSpeedWpm: number;
}

/**
 * Request to record a text recommendation
 */
export interface RecordRecommendationRequest {
  studentId: string;
  readingTextId: string;
  trainingSeriesId?: string;
  confidenceScore: number;
  reasoningJson: string;
}

/**
 * Selection criteria for filtering/scoring texts
 */
export interface SelectionCriteria {
  category?: string;
  difficultyLevel?: number;
  minWordCount?: number;
  maxWordCount?: number;
  tags?: string[];
  targetAgeGroupId?: string;
}
