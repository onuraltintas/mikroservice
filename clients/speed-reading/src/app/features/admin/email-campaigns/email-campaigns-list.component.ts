import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatChipsModule } from '@angular/material/chips';
import { EmailCampaignsService } from '../../../core/services/email-campaigns.service';
import { EmailCampaign, CampaignStatus, CampaignStatusLabels } from '../../../core/models/email-campaign.model';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil } from 'rxjs/operators';
import { CampaignDialogComponent } from './campaign-dialog.component';
import { CampaignStatsDialogComponent } from './campaign-stats-dialog.component';

@Component({
  selector: 'app-email-campaigns-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatDialogModule,
    MatChipsModule
  ],
  templateUrl: './email-campaigns-list.component.html',
  styleUrls: ['./email-campaigns-list.component.scss']
})
export class EmailCampaignsListComponent extends BaseComponent implements OnInit {
  private campaignsService = inject(EmailCampaignsService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  campaigns: EmailCampaign[] = [];
  displayedColumns = ['name', 'status', 'targeting', 'stats', 'date', 'actions'];
  selectedStatus?: CampaignStatus;

  CampaignStatus = CampaignStatus;

  ngOnInit(): void {
    this.loadCampaigns();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadCampaigns(): void {
    this.campaignsService.getCampaigns(this.selectedStatus)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (campaigns) => {
          this.campaigns = campaigns;
        },
        error: (err) => {
          this.handleError(err, 'Kampanyalar yüklenirken hata oluştu');
        }
      });
  }

  createCampaign(): void {
    const dialogRef = this.dialog.open(CampaignDialogComponent, {
      width: '95vw',
      height: '90vh',
      panelClass: 'campaign-dialog-container',
      autoFocus: false,
      data: { mode: 'create' }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadCampaigns();
      }
    });
  }

  editCampaign(campaign: EmailCampaign): void {
    const dialogRef = this.dialog.open(CampaignDialogComponent, {
      width: '95vw',
      height: '90vh',
      panelClass: 'campaign-dialog-container',
      autoFocus: false,
      data: { mode: 'edit', campaignId: campaign.id }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadCampaigns();
      }
    });
  }

  async sendCampaign(campaign: EmailCampaign): Promise<void> {
    const confirmed = await this.confirm(
      `"${campaign.name}" kampanyasını göndermek istediğinizden emin misiniz?`
    );

    if (confirmed) {
      this.campaignsService.sendCampaign(campaign.id, { sendNow: true })
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (response) => {
            this.handleSuccess(response.message);
            this.loadCampaigns();
          },
          error: (err) => {
            this.handleError(err, 'Kampanya gönderilirken hata oluştu');
          }
        });
    }
  }

  async deleteCampaign(campaign: EmailCampaign): Promise<void> {
    const confirmed = await this.confirm(
      `"${campaign.name}" kampanyasını silmek istediğinizden emin misiniz?`
    );

    if (confirmed) {
      this.campaignsService.deleteCampaign(campaign.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess('Kampanya başarıyla silindi');
            this.loadCampaigns();
          },
          error: (err) => {
            this.handleError(err, 'Kampanya silinirken hata oluştu');
          }
        });
    }
  }

  viewStats(campaign: EmailCampaign): void {
    this.dialog.open(CampaignStatsDialogComponent, {
      width: '700px',
      data: { campaignId: campaign.id, campaignName: campaign.name }
    });
  }

  getStatusLabel(status: CampaignStatus): string {
    return CampaignStatusLabels[status];
  }

  parseRoles(rolesJson: string | null): string {
    if (!rolesJson) return '';
    try {
      const roles = JSON.parse(rolesJson);
      return roles.join(', ');
    } catch {
      return rolesJson;
    }
  }
}
