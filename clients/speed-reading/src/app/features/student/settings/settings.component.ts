import { Component, OnInit, inject, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { SettingsService } from '../../../core/services/settings.service';
import { UserSettings } from '../../../core/models/settings.model';
import { ToasterService } from '../../../core/services/toaster.service';
import { ThemeService, Theme } from '../../../core/services/theme.service';
import { BaseComponent } from '../../../core/components/base.component';
import { AuthService } from '../../../core/services/auth.service';
import { UsersService } from '../../../core/services/users.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
  ],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss']
})
export class SettingsComponent extends BaseComponent implements OnInit, OnDestroy {
  private settingsService = inject(SettingsService);
  private authService = inject(AuthService);
  private usersService = inject(UsersService);
  readonly themeService   = inject(ThemeService);
  // toaster inherited from BaseComponent

  activeTab = 0;

  settings: UserSettings = {
    fontSize: 16,
    fontFamily: 'Arial',
    theme: 'light',
    lineHeight: 150,
    letterSpacing: 0,
    dailyReminder: true,
    reminderTime: '09:00',
    emailNotifications: false,
    achievementNotifications: true,
    progressReports: true,
    shareProgress: true,
    allowAnalytics: true
  };

  ngOnInit(): void {
    this.loadSettings();

  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }



  onThemeChange(theme: Theme): void {
    this.themeService.setTheme(theme);
    this.settings.theme = theme;
  }

  loadSettings(): void {
    this.settingsService.loadSettings().subscribe({
      next: (data) => {
        this.settings = data;
        // Kaydedilmiş tema varsa hemen uygula
        if (data.theme) this.themeService.setTheme(data.theme as Theme);

        // Progress sharing is account data, so hydrate it from Identity rather
        // than trusting a stale browser preference. Other settings remain local.
        if (this.authService.hasRole('Student')) {
          this.usersService.getMyProfile().subscribe({
            next: (profile) => {
              if (profile.shareProgressWithTeachers !== undefined) {
                this.settings = {
                  ...this.settings,
                  shareProgress: profile.shareProgressWithTeachers
                };
                this.previewSettings();
              }
            },
            error: (error) => console.error('Progress sharing preference could not be loaded:', error)
          });
        }
      },
      error: (error) => {
        console.error('Error loading settings:', error);
        this.toaster.error('Ayarlar yüklenemedi', 3000);
      }
    });
  }



  saveSettings(): void {
    this.settingsService.updateSettings(this.settings).subscribe({
      next: () => {
        if (!this.authService.hasRole('Student')) {
          this.toaster.success('Ayarlar kaydedildi', 3000);
          return;
        }

        this.usersService.updateMyProgressSharing(this.settings.shareProgress).subscribe({
          next: () => this.toaster.success('Ayarlar kaydedildi', 3000),
          error: (error) => {
            console.error('Progress sharing preference could not be saved:', error);
            this.toaster.error('İlerleme paylaşımı kaydedilemedi', 3000);
          }
        });
      },
      error: (error) => {
        console.error('Error saving settings:', error);
        this.toaster.error('Ayarlar kaydedilemedi', 3000);
      }
    });
  }

  /**
   * Preview settings in real-time without saving
   */
  previewSettings(): void {
    this.settingsService.applySettings(this.settings);
  }

  resetToDefaults(): void {
    this.settingsService.resetToDefaults().subscribe({
      next: () => {
        this.settings = this.settingsService.getCurrentSettings();
        this.toaster.success('Varsayılan ayarlara dönüldü', 3000);
      },
      error: (error) => {
        console.error('Error resetting settings:', error);
        this.toaster.error('Ayarlar sıfırlanamadı', 3000);
      }
    });
  }


}
