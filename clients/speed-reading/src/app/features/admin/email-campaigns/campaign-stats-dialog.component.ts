import { Component, OnInit, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EmailCampaignsService } from '../../../core/services/email-campaigns.service';
import { CampaignStats } from '../../../core/models/email-campaign.model';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-campaign-stats-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatProgressBarModule
  ],
  templateUrl: './campaign-stats-dialog.component.html',
  styleUrls: ['./campaign-stats-dialog.component.scss']
})
export class CampaignStatsDialogComponent extends BaseComponent implements OnInit {
  private campaignsService = inject(EmailCampaignsService);
  private dialogRef = inject(MatDialogRef<CampaignStatsDialogComponent>);

  stats: CampaignStats | null = null;
  // loading inherited from BaseComponent
  error: string | null = null;
  campaignName = '';

  constructor(@Inject(MAT_DIALOG_DATA) public data: { campaignId: string; campaignName: string }) {
    super();
    this.campaignName = data.campaignName;
  }

  ngOnInit(): void {
    this.loadStats();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  loadStats(): void {
    this.campaignsService.getCampaignStats(this.data.campaignId).subscribe({
      next: (stats) => {
        this.stats = stats;
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading stats:', err);
        this.error = 'İstatistikler yüklenirken hata oluştu';
        this.loading.set(false);
      }
    });
  }

  getSentPercentage(): number {
    if (!this.stats || this.stats.totalRecipients === 0) return 0;
    return Math.round((this.stats.sentCount / this.stats.totalRecipients) * 100);
  }

  getOpenRate(): number {
    if (!this.stats || this.stats.sentCount === 0) return 0;
    return Math.round((this.stats.openedCount / this.stats.sentCount) * 100);
  }

  getClickRate(): number {
    if (!this.stats || this.stats.sentCount === 0) return 0;
    return Math.round((this.stats.clickedCount / this.stats.sentCount) * 100);
  }
}
