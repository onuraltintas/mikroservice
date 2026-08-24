import { Injectable } from '@angular/core';

/**
 * ExerciseTrackingService
 * 
 * Comprehensive ML-ready data collection service for exercise tracking.
 * Automatically tracks:
 * - Time-series events (actions, responses, timestamps)
 * - User interactions (clicks, pauses, focus changes)
 * - Error patterns (mistakes, corrections, context)
 * - Performance metrics (quartile analysis, difficulty breakdown)
 * 
 * Usage:
 * 1. Call startTracking() when exercise begins
 * 2. Use logTimeSeries() for each user action/response
 * 3. Use logInteraction() for UI events (clicks, hovers, etc.)
 * 4. Use logError() when user makes a mistake
 * 5. Call getStandardizedResult() when exercise ends
 */
@Injectable({
  providedIn: 'root'
})
export class ExerciseTrackingService {
  private exerciseStartTime: number = 0;
  private lastActionTime: number = 0;
  private timeSeriesData: TimeSeriesEntry[] = [];
  private interactionEvents: InteractionEvent[] = [];
  private errorPatterns: ErrorPattern[] = [];
  private pauseCount: number = 0;
  private totalPausedMs: number = 0;
  private readonly PAUSE_THRESHOLD_MS = 2000; // 2 seconds idle = pause

  constructor() { }

  /**
   * Start tracking for a new exercise session
   * Resets all tracking data and initializes timestamps
   */
  startTracking(): void {
    this.exerciseStartTime = Date.now();
    this.lastActionTime = this.exerciseStartTime;
    this.timeSeriesData = [];
    this.interactionEvents = [];
    this.errorPatterns = [];
    this.pauseCount = 0;
    this.totalPausedMs = 0;
  }

  /**
   * Log a time-series event (user action or response)
   * Automatically detects pauses and calculates response time
   * 
   * @param action - Type of action (e.g., 'WordClick', 'AnswerSubmit', 'CellSelect')
   * @param isCorrect - Whether the action was correct
   * @param metadata - Additional context (optional)
   */
  logTimeSeries(action: string, isCorrect: boolean, metadata?: any): number {
    const now = Date.now();
    const responseTimeMs = now - this.lastActionTime;

    // Detect pause (>2s idle)
    if (responseTimeMs > this.PAUSE_THRESHOLD_MS) {
      this.pauseCount++;
      this.totalPausedMs += responseTimeMs - this.PAUSE_THRESHOLD_MS;
    }

    // Calculate focus level (inversely related to response time)
    // Fast responses (< 1s) = high focus (>0.9)
    // Normal responses (1-3s) = medium focus (0.7-0.9)
    // Slow responses (>3s) = low focus (<0.7)
    const focusLevel = this.calculateFocusLevel(responseTimeMs);

    const entry: TimeSeriesEntry = {
      timestamp: new Date(now).toISOString(),
      action,
      isCorrect,
      responseTimeMs,
      focusLevel,
      metadata
    };

    this.timeSeriesData.push(entry);
    this.lastActionTime = now;

    return responseTimeMs;
  }

  /**
   * Log a user interaction event (UI-level tracking)
   * 
   * @param eventType - Type of event ('Click', 'Hover', 'KeyPress', 'Focus', 'Blur')
   * @param target - Target element or identifier
   * @param metadata - Additional context (position, value, etc.)
   */
  logInteraction(eventType: string, target: string, metadata?: any): void {
    const event: InteractionEvent = {
      timestamp: new Date().toISOString(),
      eventType,
      target,
      metadata
    };

    this.interactionEvents.push(event);
  }

  /**
   * Log an error pattern for ML analysis
   * 
   * @param errorType - Type of error ('Wrong', 'Timeout', 'Skip', 'Sequence', 'Comprehension')
   * @param context - What was the user trying to do
   * @param correctedImmediately - Did user self-correct?
   * @param metadata - Additional error context
   */
  logError(errorType: string, context: string, correctedImmediately: boolean = false, metadata?: any): void {
    const error: ErrorPattern = {
      timestamp: new Date().toISOString(),
      errorType,
      context,
      correctedImmediately,
      metadata
    };

    this.errorPatterns.push(error);
  }

  /**
   * Get standardized result data for API submission
   * Calculates all response time statistics and performance breakdowns
   * 
   * @param typeSpecificMetrics - Exercise-specific metrics (e.g., WPM for speed reading)
   * @returns Complete StandardizedResultData object
   */
  getStandardizedResult(typeSpecificMetrics: any): StandardizedResultData {
    const responseTimes = this.timeSeriesData
      .filter(entry => entry.responseTimeMs > 0)
      .map(entry => entry.responseTimeMs);

    // Calculate response time statistics
    const stats = this.calculateResponseTimeStats(responseTimes);

    // Calculate performance breakdown
    const correctCount = this.timeSeriesData.filter(e => e.isCorrect).length;
    const incorrectCount = this.timeSeriesData.filter(e => !e.isCorrect).length;
    const totalAttempts = correctCount + incorrectCount;

    const performance = this.calculatePerformanceBreakdown(
      this.timeSeriesData,
      correctCount,
      totalAttempts
    );

    return {
      typeSpecific: typeSpecificMetrics,
      timeSeries: this.timeSeriesData,
      interactions: this.interactionEvents,
      errorPatterns: this.errorPatterns,
      performance,
      responseTimeStats: stats,
      correctCount,
      incorrectCount,
      totalAttempts,
      pauseCount: this.pauseCount,
      totalPausedSeconds: Math.round(this.totalPausedMs / 1000)
    };
  }

  /**
   * Calculate response time statistics (avg, median, stddev)
   * @private
   */
  private calculateResponseTimeStats(responseTimes: number[]): ResponseTimeStats {
    if (responseTimes.length === 0) {
      return { average: 0, median: 0, stdDev: 0 };
    }

    const average = responseTimes.reduce((a, b) => a + b, 0) / responseTimes.length;

    const sorted = [...responseTimes].sort((a, b) => a - b);
    const median = sorted.length % 2 === 0
      ? (sorted[sorted.length / 2 - 1] + sorted[sorted.length / 2]) / 2
      : sorted[Math.floor(sorted.length / 2)];

    const variance = responseTimes.reduce((sum, time) => sum + Math.pow(time - average, 2), 0) / responseTimes.length;
    const stdDev = Math.sqrt(variance);

    return {
      average: Math.round(average * 100) / 100,
      median: Math.round(median * 100) / 100,
      stdDev: Math.round(stdDev * 100) / 100
    };
  }

  /**
   * Calculate performance breakdown for ML analysis
   * @private
   */
  private calculatePerformanceBreakdown(
    timeSeries: TimeSeriesEntry[],
    correctCount: number,
    totalAttempts: number
  ): PerformanceBreakdown {
    // Split into quartiles
    const quartileSize = Math.ceil(timeSeries.length / 4);
    const byQuartile = [];
    
    for (let i = 0; i < 4; i++) {
      const start = i * quartileSize;
      const end = Math.min((i + 1) * quartileSize, timeSeries.length);
      const quartileData = timeSeries.slice(start, end);
      const correct = quartileData.filter(e => e.isCorrect).length;
      const score = quartileData.length > 0 ? (correct / quartileData.length) * 100 : 0;
      byQuartile.push(Math.round(score * 100) / 100);
    }

    // Calculate learning rate (improvement across quartiles)
    const learningRate = byQuartile.length >= 2
      ? Math.round(((byQuartile[3] - byQuartile[0]) / 3) * 100) / 100
      : 0;

    // Calculate consistency score (inverse coefficient of variation)
    const avgQuartile = byQuartile.reduce((a, b) => a + b, 0) / byQuartile.length;
    const stdDevQuartile = Math.sqrt(
      byQuartile.reduce((sum, score) => sum + Math.pow(score - avgQuartile, 2), 0) / byQuartile.length
    );
    const cv = avgQuartile > 0 ? stdDevQuartile / avgQuartile : 0;
    const consistencyScore = Math.max(0, Math.min(100, (0.5 - cv) * 200));

    return {
      byQuartile,
      byDifficulty: {}, // Exercise-specific, can be populated by caller
      learningRate,
      consistencyScore: Math.round(consistencyScore * 100) / 100
    };
  }

  /**
   * Calculate focus level based on response time
   * @private
   */
  private calculateFocusLevel(responseTimeMs: number): number {
    if (responseTimeMs < 1000) return 0.95; // Very focused
    if (responseTimeMs < 2000) return 0.85; // Focused
    if (responseTimeMs < 3000) return 0.75; // Moderate focus
    if (responseTimeMs < 5000) return 0.60; // Low focus
    return 0.40; // Distracted
  }

  /**
   * Get current session duration in seconds
   */
  getSessionDurationSeconds(): number {
    return Math.round((Date.now() - this.exerciseStartTime) / 1000);
  }

  /**
   * Get current pause statistics
   */
  getPauseStats(): { pauseCount: number; totalPausedSeconds: number } {
    return {
      pauseCount: this.pauseCount,
      totalPausedSeconds: Math.round(this.totalPausedMs / 1000)
    };
  }
}

// ============================================================================
// TypeScript Interfaces for ML-Ready Data Structures
// ============================================================================

export interface TimeSeriesEntry {
  timestamp: string;
  action: string;
  isCorrect: boolean;
  responseTimeMs: number;
  focusLevel: number;
  metadata?: any;
}

export interface InteractionEvent {
  timestamp: string;
  eventType: string;
  target: string;
  metadata?: any;
}

export interface ErrorPattern {
  timestamp: string;
  errorType: string;
  context: string;
  correctedImmediately: boolean;
  metadata?: any;
}

export interface PerformanceBreakdown {
  byQuartile: number[];
  byDifficulty: { [key: string]: number };
  learningRate: number;
  consistencyScore: number;
}

export interface ResponseTimeStats {
  average: number;
  median: number;
  stdDev: number;
}

export interface StandardizedResultData {
  typeSpecific: any;
  timeSeries: TimeSeriesEntry[];
  interactions: InteractionEvent[];
  errorPatterns: ErrorPattern[];
  performance: PerformanceBreakdown;
  responseTimeStats: ResponseTimeStats;
  correctCount: number;
  incorrectCount: number;
  totalAttempts: number;
  pauseCount: number;
  totalPausedSeconds: number;
}
