// Content Feedback Models

export interface SaveFeedbackRequest {
  contentId: string;
  contentType: 'ReadingText' | 'Exercise' | 'ProgramTemplate'; // Changed: TrainingSeries → ProgramTemplate

  // Explicit Feedback
  rating?: number;
  isLiked: boolean;
  isBookmarked: boolean;
  skipReason?: string;

  // Implicit Feedback
  completionRate: number;
  timeSpentSeconds: number;
  expectedTimeSeconds: number;
  comprehensionScore?: number;
  exerciseScore?: number;
  retryCount: number;

  // Engagement Metrics
  interactionCount: number;
  pauseCount: number;
  abandonedAtPercentage?: number;

  // Context Metadata
  deviceType: string;
  contentCategory?: string;
  contentDifficultyLevel?: number;
}

export interface FeedbackAnalytics {
  totalFeedbacks: number;
  averageCompletionRate: number;
  averagePerformanceScore: number;
  averageEngagementScore: number;
  totalLikes: number;
  totalBookmarks: number;
  totalSkips: number;
  topCategories: CategoryPreference[];
  optimalHours: OptimalHour[];
  contentTypeBreakdown: { [key: string]: number };
  weakAreas?: WeakArea[];
}

export interface WeakArea {
  category: string;
  averageScore: number;
  totalAttempts: number;
  recommendation: string;
  improvementNeeded: 'High' | 'Medium' | 'Low';
}

export interface CategoryPreference {
  category: string;
  count: number;
  averageScore: number;
}

export interface OptimalHour {
  hour: number;
  averageScore: number;
  sessionCount: number;
}

export interface RecommendedContent {
  id: string;
  title: string;
  description: string;
  contentType: string;
  category?: string;
  difficultyLevel?: number;
  recommendationScore: number;
  scoreReason: string;
}

// Feedback tracking helper class
export class FeedbackTracker {
  private startTime: Date;
  private interactionCount = 0;
  private pauseCount = 0;
  private isPaused = false;
  private totalPausedTime = 0;
  private lastPauseTime?: Date;

  constructor() {
    this.startTime = new Date();
  }

  recordInteraction(): void {
    this.interactionCount++;
  }

  pause(): void {
    if (!this.isPaused) {
      this.isPaused = true;
      this.pauseCount++;
      this.lastPauseTime = new Date();
    }
  }

  resume(): void {
    if (this.isPaused && this.lastPauseTime) {
      this.isPaused = false;
      const pauseDuration = new Date().getTime() - this.lastPauseTime.getTime();
      this.totalPausedTime += pauseDuration;
    }
  }

  getTimeSpentSeconds(): number {
    const totalTime = new Date().getTime() - this.startTime.getTime();
    return Math.floor((totalTime - this.totalPausedTime) / 1000);
  }

  getInteractionCount(): number {
    return this.interactionCount;
  }

  getPauseCount(): number {
    return this.pauseCount;
  }

  reset(): void {
    this.startTime = new Date();
    this.interactionCount = 0;
    this.pauseCount = 0;
    this.isPaused = false;
    this.totalPausedTime = 0;
    this.lastPauseTime = undefined;
  }
}
