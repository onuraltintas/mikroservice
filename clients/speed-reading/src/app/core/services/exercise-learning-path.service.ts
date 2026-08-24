import { Injectable, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LearningPathService } from './learning-path.service';
import { ToasterService } from './toaster.service';
import { filter, take } from 'rxjs/operators';

/**
 * Centralized service for handling learning path completion from exercises and reading texts.
 *
 * Usage in exercise components:
 * ```typescript
 * private learningPathCompletion = inject(ExerciseLearningPathService);
 *
 * finishExercise() {
 *   this.learningPathCompletion.completeIfFromPath(this.performanceScore);
 * }
 * ```
 *
 * This service automatically:
 * - Checks if the exercise/reading was started from learning path (pathItemId query param)
 * - Calls the completion API if applicable
 * - Shows success toast with progress
 * - Handles errors silently (background operation)
 */
@Injectable({
  providedIn: 'root'
})
export class ExerciseLearningPathService {
  private route = inject(ActivatedRoute);
  private learningPathService = inject(LearningPathService);
  private toaster = inject(ToasterService);

  /**
   * Complete learning path item if this content was started from learning path.
   * This method reads the pathItemId from query parameters and completes the item if it exists.
   *
   * @param achievedScore - The performance score achieved (0-100)
   * @param contentType - Optional content type for logging (default: 'İçerik')
   */
  completeIfFromPath(achievedScore: number, contentType: string = 'İçerik'): void {
    this.route.queryParams
      .pipe(
        take(1), // Only take the first emission
        filter(params => params['pathItemId']) // Only proceed if pathItemId exists
      )
      .subscribe(params => {
        const pathItemId = params['pathItemId'];

        this.learningPathService.completePersonalizedPathItem(pathItemId, achievedScore)
          .subscribe({
            next: (response) => {

              if (response.success) {
                // Show progress toast
                this.toaster.success(
                  `🎉 Tebrikler! ${response.completedCount}/${response.totalCount} ${contentType.toLowerCase()} tamamlandı`,
                  3000
                );

                // If unlocked next item, show notification
                if (response.nextItemId) {
                  setTimeout(() => {
                    this.toaster.info(
                      `🔓 Yeni içerik açıldı! Dashboard'dan devam edebilirsiniz.`,
                      4000
                    );
                  }, 1000);
                }
              }
            },
            error: (err) => {
              console.error('❌ Error completing learning path item:', err);
              // Don't show error to user - this is a background operation
              // User already completed the exercise/reading successfully
            }
          });
      });
  }

  /**
   * Check if the current route has a pathItemId query parameter.
   * Useful for conditional UI elements.
   *
   * @returns Observable<boolean> - Emits true if pathItemId exists
   */
  isFromLearningPath(): boolean {
    let isFromPath = false;
    this.route.queryParams
      .pipe(take(1))
      .subscribe(params => {
        isFromPath = !!params['pathItemId'];
      });
    return isFromPath;
  }
}
