import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { SafeHtmlPipe } from '../../pipes/safe-html.pipe';
import { AnnouncementService } from '../../../core/services/announcement.service';
import { AnnouncementDto, AnnouncementPriority } from '../../../core/models/announcement.model';

@Component({
  selector: 'app-announcement-banner',
  standalone: true,
  imports: [CommonModule, SafeHtmlPipe],
  templateUrl: './announcement-banner.component.html',
  styleUrls: ['./announcement-banner.component.scss']
})
export class AnnouncementBannerComponent implements OnInit {
  private announcementService = inject(AnnouncementService);
  private router = inject(Router);

  announcements: AnnouncementDto[] = [];
  Priority = AnnouncementPriority;

  ngOnInit(): void {
    this.loadAnnouncements();

    // Subscribe to announcement updates
    this.announcementService.activeAnnouncements$.subscribe(announcements => {
      this.announcements = announcements.filter(a => !a.hasDismissed);

      // Auto-record views for non-viewed announcements
      announcements
        .filter(a => !a.hasViewed)
        .forEach(a => {
          this.announcementService.recordView(a.id).subscribe();
        });
    });
  }

  loadAnnouncements(): void {
    this.announcementService.getMyAnnouncements(false, false).subscribe();
  }

  handleAction(announcement: AnnouncementDto): void {
    // Record click
    this.announcementService.recordClick(announcement.id).subscribe();

    // Navigate to action URL
    if (announcement.actionUrl) {
      if (announcement.actionUrl.startsWith('http')) {
        window.open(announcement.actionUrl, '_blank');
      } else {
        this.router.navigate([announcement.actionUrl]);
      }
    }
  }

  dismiss(announcement: AnnouncementDto): void {
    this.announcementService.dismissAnnouncement(announcement.id).subscribe(() => {
      this.announcements = this.announcements.filter(a => a.id !== announcement.id);
    });
  }
}
