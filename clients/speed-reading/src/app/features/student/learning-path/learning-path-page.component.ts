import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { LearningPathService } from '../../../core/services/learning-path.service';
import {
  PersonalizedLearningPathDto,
  PersonalizedLearningPathItemDto,
  PersonalizedLearningPathHelper,
  LearningPathProgressDto,
  PersonalizedPathAvailabilityDto
} from '../../../core/models/learning-path.model';

@Component({
  selector: 'app-learning-path-page',
  standalone: true,
  imports: [
    CommonModule
  ],
  templateUrl: './learning-path-page.component.html',
  styleUrls: ['./learning-path-page.component.scss']
})
export class LearningPathPageComponent implements OnInit, OnDestroy {
  readonly Math = Math;
  private destroy$ = new Subject<void>();

  learningPath: PersonalizedLearningPathDto | null = null;
  progressSummary: LearningPathProgressDto | null = null;
  loading = true;
  availability: PersonalizedPathAvailabilityDto | null = null;
  error = false;

  currentPage = 0;
  pageSize = 5;
  totalItems = 0;
  generating = false;

  constructor(
    private learningPathService: LearningPathService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.learningPathService.getPersonalizedPathAvailability()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: availability => {
          this.availability = availability;
          if (!availability.isAvailable) {
            this.loading = false;
            return;
          }
          this.loadProgressSummary();
          this.loadLearningPath();
        },
        error: () => {
          this.error = true;
          this.loading = false;
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadProgressSummary(): void {
    this.learningPathService.getPersonalizedLearningPathProgress()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (progress) => {
          this.progressSummary = progress;
        },
        error: (err) => {
          console.error('Error loading progress summary:', err);
        }
      });
  }

  loadLearningPath(): void {
    this.loading = true;
    const pageNumber = this.currentPage + 1; // Backend 1-indexed
    this.learningPathService.getPersonalizedLearningPath(pageNumber, this.pageSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (path) => {
          this.learningPath = path;
          this.totalItems = path.totalItems ?? 0;
          this.loading = false;
        },
        error: (err) => {
          console.error('Error loading learning path:', err);
          this.loading = false;
        }
      });
  }

  onPageChange(pageIndex: number, pageSize: number): void {
    this.currentPage = pageIndex;
    this.pageSize = pageSize;
    this.loadLearningPath();
  }

  isCurrentItem(item: PersonalizedLearningPathItemDto): boolean {
    if (!this.progressSummary?.nextItem) return false;
    return item.id === this.progressSummary.nextItem.id;
  }

  navigateToItem(item: PersonalizedLearningPathItemDto): void {
    if (!item.isUnlocked || item.isCompleted) return;
    const route = PersonalizedLearningPathHelper.getContentRoute(
      item.contentType,
      item.contentId
    );

    // Pass pathItemId as query param so content can mark it complete
    this.router.navigate(route, {
      queryParams: { pathItemId: item.id }
    });
  }

  getContentTypeIcon(contentType: string): string {
    return PersonalizedLearningPathHelper.getContentTypeIcon(contentType);
  }

  getContentTypeLabel(contentType: string): string {
    return PersonalizedLearningPathHelper.getContentTypeLabel(contentType);
  }

  getContentTypeClass(contentType: string): string {
    const type = contentType.toLowerCase();
    if (type.includes('reading')) return 'reading';
    if (type.includes('exercise')) return 'exercise';
    if (type.includes('series')) return 'series';
    return '';
  }

  getDifficultyLabel(level: number): string {
    return PersonalizedLearningPathHelper.getDifficultyLabel(level);
  }

  getDifficultyColor(level: number): string {
    return PersonalizedLearningPathHelper.getDifficultyColor(level);
  }

  formatDuration(minutes: number): string {
    return PersonalizedLearningPathHelper.formatDuration(minutes);
  }

  formatDate(date: Date | null): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString('tr-TR', {
      day: 'numeric',
      month: 'short'
    });
  }

  getEmptyStateTitle(): string {
    return 'Şu an ek çalışma önerilmiyor';
  }

  getEmptyStateMessage(): string {
    return 'Ana programına devam et. Ölçümlerinde destek ihtiyacı görülürse burada kısa, hedefli öneriler belirecek.';
  }

  generatePath(): void {
    if (!this.availability?.isAvailable) return;
    this.generating = true;
    this.learningPathService.generatePersonalizedPath()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.generating = false;
          this.currentPage = 0;
          this.loadProgressSummary();
          this.loadLearningPath();
        },
        error: () => {
          this.generating = false;
        }
      });
  }

  goBack(): void {
    this.router.navigate(['/student/dashboard']);
  }
}
