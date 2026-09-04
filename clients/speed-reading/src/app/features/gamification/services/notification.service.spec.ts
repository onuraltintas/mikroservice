import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ToasterService } from '../../../core/services/toaster.service';
import { Achievement, AchievementCategory, AchievementTier } from '../../../core/models/gamification.model';
import { GamificationNotificationService } from './notification.service';

describe('GamificationNotificationService', () => {
  it('routes achievement feedback through the shared toaster', () => {
    const toaster = jasmine.createSpyObj<ToasterService>('ToasterService', ['success', 'info', 'warning']);
    const achievement = {
      name: 'Odaklanma başlangıcı',
      category: AchievementCategory.Reading,
      tier: AchievementTier.Bronze
    } as Achievement;

    TestBed.configureTestingModule({
      providers: [
        GamificationNotificationService,
        { provide: ToasterService, useValue: toaster },
        { provide: MatSnackBar, useValue: {} },
        { provide: MatDialog, useValue: { open: jasmine.createSpy('open') } }
      ]
    });

    TestBed.inject(GamificationNotificationService).showAchievementUnlocked(achievement);

    expect(toaster.success.calls.mostRecent().args as unknown[]).toEqual([
      'Odaklanma başlangıcı',
      { title: 'Yeni başarı kazanıldı', duration: 5000 }
    ]);
  });
});
