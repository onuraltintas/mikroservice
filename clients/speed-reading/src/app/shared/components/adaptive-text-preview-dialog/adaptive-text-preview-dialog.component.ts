import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { AdaptiveTextRecommendation } from '../../../core/models/adaptive-text.model';
import { AdaptiveTextService } from '../../../core/services/adaptive-text.service';

export interface AdaptiveTextPreviewDialogData {
  recommendation: AdaptiveTextRecommendation;
  showFullContent?: boolean;
}

@Component({
  selector: 'app-adaptive-text-preview-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressBarModule,
    MatCardModule,
    MatDividerModule
  ],
  templateUrl: './adaptive-text-preview-dialog.component.html',
  styleUrls: ['./adaptive-text-preview-dialog.component.scss']
})
export class AdaptiveTextPreviewDialogComponent implements OnInit {
  data = inject<AdaptiveTextPreviewDialogData>(MAT_DIALOG_DATA);
  dialogRef = inject(MatDialogRef<AdaptiveTextPreviewDialogComponent>);
  adaptiveTextService = inject(AdaptiveTextService);

  recommendation!: AdaptiveTextRecommendation;
  scoreBreakdownData: any[] = [];
  showFullContent = false;

  ngOnInit(): void {
    this.recommendation = this.data.recommendation;
    this.showFullContent = this.data.showFullContent || false;
    this.scoreBreakdownData = this.adaptiveTextService.getScoreBreakdownData(
      this.recommendation.scoreBreakdown
    );
  }

  getContentPreview(): string {
    if (this.showFullContent) {
      return this.recommendation.content;
    }
    // Show first 300 characters
    return this.recommendation.content.length > 300
      ? this.recommendation.content.substring(0, 300) + '...'
      : this.recommendation.content;
  }

  toggleContent(): void {
    this.showFullContent = !this.showFullContent;
  }

  onAccept(): void {
    this.dialogRef.close({ accepted: true, recommendation: this.recommendation });
  }

  onReject(): void {
    this.dialogRef.close({ accepted: false });
  }

  getLevelBadgeClass(level: number): string {
    if (level <= 3) return 'level-beginner';
    if (level <= 6) return 'level-intermediate';
    return 'level-advanced';
  }

  getScoreColor(score: number): string {
    if (score >= 80) return 'success';
    if (score >= 60) return 'primary';
    if (score >= 40) return 'accent';
    return 'warn';
  }
}
