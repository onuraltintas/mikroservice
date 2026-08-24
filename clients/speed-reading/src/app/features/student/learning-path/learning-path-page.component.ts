import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { LearningPathService } from '../../../core/services/learning-path.service';
import {
  PersonalizedLearningPathDto,
  PersonalizedLearningPathItemDto,
  PersonalizedLearningPathHelper,
  LearningPathProgressDto
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

  selectedTabIndex = 0; // 0: incomplete, 1: completed, 2: all
  currentPage = 0;
  pageSize = 20;
  totalItems = 0;
  generating = false;

  constructor(
    private learningPathService: LearningPathService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.loadProgressSummary();
    this.loadLearningPath();
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
    const onlyCompleted = this.selectedTabIndex === 1;

    this.learningPathService.getPersonalizedLearningPath(pageNumber, this.pageSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (path) => {
          // Client-side incomplete/completed filtering
          if (onlyCompleted) {
            path.items = path.items?.filter(item => item.isCompleted) ?? [];
          } else if (this.selectedTabIndex === 0) {
            path.items = path.items?.filter(item => !item.isCompleted) ?? [];
          }

          this.learningPath = path;
          this.totalItems = this.selectedTabIndex === 0 ? (path.remainingItems ?? path.items?.length ?? 0) :
            onlyCompleted ? (path.completedItems ?? path.items?.length ?? 0) :
              (path.totalItems ?? 0);
          this.loading = false;
        },
        error: (err) => {
          console.error('Error loading learning path:', err);
          this.loading = false;
        }
      });
  }

  onTabChange(index: number): void {
    this.selectedTabIndex = index;
    this.currentPage = 0;
    this.loadLearningPath();
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
    if (this.selectedTabIndex === 0) return 'Harika İş!';
    if (this.selectedTabIndex === 1) return 'Henüz Tamamlanmış İçerik Yok';
    return 'Öğrenme Yolu Bulunamadı';
  }

  getEmptyStateMessage(): string {
    if (this.selectedTabIndex === 0) {
      return 'Tüm içerikleri tamamladın! Yeni içerikler yakında eklenecek.';
    }
    if (this.selectedTabIndex === 1) {
      return 'Henüz hiç içerik tamamlamadın. İlk adımını atmaya hazır mısın?';
    }
    return 'Öğrenme yolun oluşturulmadı. Lütfen profilini tamamla.';
  }

  generatePath(): void {
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
