import { Component, Input, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { StudentReadingProfile } from '../../../core/models/adaptive-text.model';
import { AdaptiveTextService } from '../../../core/services/adaptive-text.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-reading-profile-widget',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    MatChipsModule,
    MatButtonModule,
    MatTooltipModule
  ],
  templateUrl: './reading-profile-widget.component.html',
  styleUrls: ['./reading-profile-widget.component.scss']
})
export class ReadingProfileWidgetComponent implements OnInit {
  @Input() compact = false; // Compact mode for smaller displays

  adaptiveTextService = inject(AdaptiveTextService);
  authService = inject(AuthService);

  profile: StudentReadingProfile | null = null;
  loading = true;
  error: string | null = null;

  ngOnInit(): void {
    this.loadProfile();
  }

  loadProfile(): void {
    this.loadProfileData(false);
  }

  refresh(): void {
    this.loadProfileData(true);
  }

  private loadProfileData(forceRefresh: boolean): void {
    const currentUser = this.authService.currentUserValue;
    if (!currentUser?.id) {
      this.error = 'Kullanıcı bilgisi bulunamadı';
      this.loading = false;
      return;
    }

    this.loading = true;
    this.error = null;

    this.adaptiveTextService.getProfile(currentUser.id, forceRefresh).subscribe({
      next: (profile) => {
        this.profile = profile;
        this.loading = false;
      },
      error: (err) => {
        console.error('Failed to load reading profile:', err);
        this.error = 'Profil yüklenirken hata oluştu';
        this.loading = false;
      }
    });
  }

  getLevelProgress(): number {
    if (!this.profile) return 0;
    return Math.min(100, Math.max(0, (this.profile.currentReadingLevel / 10) * 100));
  }

  getComprehensionColor(): string {
    if (!this.profile) return 'primary';
    const score = this.profile.averageComprehensionScore;
    if (score >= 80) return 'success';
    if (score >= 60) return 'primary';
    if (score >= 40) return 'accent';
    return 'warn';
  }

  getSpeedIcon(): string {
    if (!this.profile || this.profile.averageReadingSpeed === 0) return 'schedule';
    const wpm = this.profile.averageReadingSpeed;
    if (wpm < 200) return 'trending_down';
    if (wpm < 300) return 'trending_flat';
    return 'trending_up';
  }
}
