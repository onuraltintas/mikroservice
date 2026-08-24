import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntil } from 'rxjs/operators';
import { AnnouncementService } from '../../../core/services/announcement.service';
import {
  AnnouncementDetailDto,
  AnnouncementPriority,
  AnnouncementAudience,
  AnnouncementDisplayType
} from '../../../core/models/announcement.model';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-announcements-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './announcements-list.component.html',
  styleUrls: ['./announcements-list.component.scss']
})
export class AnnouncementsListComponent extends BaseComponent implements OnInit {
  private announcementService = inject(AnnouncementService);
  private router = inject(Router);

  announcements: AnnouncementDetailDto[] = [];
  filterActive?: boolean;
  filterAudience?: AnnouncementAudience;
  includeExpired = false;

  Priority = AnnouncementPriority;
  Audience = AnnouncementAudience;

  ngOnInit(): void {
    this.loadAnnouncements();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadAnnouncements(): void {
    this.announcementService.getAllAnnouncements({
      isActive: this.filterActive,
      targetAudience: this.filterAudience,
      includeExpired: this.includeExpired
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (announcements) => {
          this.announcements = announcements;
        },
        error: (error) => {
          this.handleError(error, 'Duyurular yüklenirken hata oluştu');
        }
      });
  }

  getPriorityLabel(priority: AnnouncementPriority): string {
    return AnnouncementPriority[priority];
  }

  getPriorityClass(priority: AnnouncementPriority): string {
    return AnnouncementPriority[priority].toLowerCase();
  }

  getAudienceLabel(audience: AnnouncementAudience): string {
    return AnnouncementAudience[audience];
  }

  getDisplayTypeLabel(displayType: AnnouncementDisplayType): string {
    return AnnouncementDisplayType[displayType];
  }

  calculateCTR(announcement: AnnouncementDetailDto): string {
    if (announcement.viewCount === 0) return '0.0';
    return ((announcement.clickCount / announcement.viewCount) * 100).toFixed(1);
  }

  createNew(): void {
    this.router.navigate(['/admin/announcements/create']);
  }

  edit(id: string): void {
    this.router.navigate(['/admin/announcements/edit', id]);
  }

  viewStats(id: string): void {
    this.router.navigate(['/admin/announcements/stats', id]);
  }

  async delete(id: string): Promise<void> {
    const confirmed = await this.confirm('Bu duyuruyu silmek istediğinizden emin misiniz?');
    if (confirmed) {
      this.announcementService.deleteAnnouncement(id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess('Duyuru silindi');
            this.loadAnnouncements();
          },
          error: (error) => {
            this.handleError(error, 'Duyuru silinirken hata oluştu');
          }
        });
    }
  }
}
