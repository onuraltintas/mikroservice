import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReviewService } from '../../../services/review.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { ReviewExerciseDto, ReviewHistoryDto } from '../../../models/student-panel.model';

@Component({
  selector: 'app-review-exercises',
  standalone: true,
  imports: [
    CommonModule
  ],
  templateUrl: './review-exercises.component.html',
  styleUrls: ['./review-exercises.component.scss']
})
export class ReviewExercisesComponent implements OnInit {
  private reviewService = inject(ReviewService);
  private toaster = inject(ToasterService);
  private router = inject(Router);

  dueReviews = this.reviewService.dueReviews;
  allReviews = signal<ReviewExerciseDto[]>([]);
  statistics = this.reviewService.statistics;
  loading = signal(false);
  history = signal<ReviewHistoryDto[] | null>(null);
  historyTitle = signal('');
  activeTab = 0;

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    this.reviewService.getAllReviews().subscribe({
      next: items => this.allReviews.set(items),
      error: () => this.toaster.error('Tekrar geçmişi yüklenemedi. Lütfen yeniden deneyin.', 3000)
    });
    this.reviewService.getDueReviews().subscribe({
      next: () => {
        this.reviewService.getStatistics().subscribe({
          complete: () => this.loading.set(false),
          error: () => this.loading.set(false)
        });
      },
      error: (error) => {
        console.error('Error loading review data:', error);
        this.toaster.error('Veriler yüklenirken hata oluştu', 3000);
        this.loading.set(false);
      }
    });
  }

  startReview(review: ReviewExerciseDto) {
    // Navigate to exercise player
    this.router.navigate(['/student/exercises/universal-player', review.exerciseId], {
      queryParams: {
        reviewItemId: review.reviewItemId,
        mode: 'review'
      }
    });
  }

  viewHistory(review: ReviewExerciseDto) {
    this.reviewService.getReviewHistory(review.exerciseId).subscribe({
      next: (items) => {
        this.historyTitle.set(review.exerciseTitle);
        this.history.set(items);
      },
      error: (error) => {
        console.error('Error loading history:', error);
        this.history.set(null);
        this.toaster.error('Geçmiş yüklenirken hata oluştu', 3000);
      }
    });
  }

  getExerciseIcon(exerciseType: string): string {
    const icons: Record<string, string> = {
      'Comprehension': 'psychology',
      'PeripheralVision': 'visibility',
      'SpeedReading': 'speed',
      'Tachistoscope': 'flash_on',
      'Saccade': 'swap_horiz',
      'Focus': 'center_focus_strong',
      'WordGroups': 'view_array',
      'Chunking': 'view_column',
      'EyeTracking': 'remove_red_eye',
      'VerticalReading': 'vertical_align_center',
      'Skimming': 'format_align_left',
      'VisualExpansion': 'zoom_out_map',
      'Visualization': 'image'
    };
    return icons[exerciseType] || 'school';
  }

  getExerciseColor(exerciseType: string): string {
    const colors: Record<string, string> = {
      'Comprehension': '#9C27B0',
      'PeripheralVision': '#42A5F5',
      'SpeedReading': '#1976D2',
      'Tachistoscope': '#FF9800',
      'Saccade': '#43A047',
      'Focus': '#607D8B',
      'WordGroups': '#009688',
      'Chunking': '#795548',
      'EyeTracking': '#E91E63',
      'VerticalReading': '#673AB7',
      'Skimming': '#3F51B5',
      'VisualExpansion': '#00BCD4',
      'Visualization': '#8BC34A'
    };
    return colors[exerciseType] || '#757575';
  }
}
