import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { NEVER, of, Subject, throwError } from 'rxjs';
import { GamificationService } from '../../../core/services/gamification.service';
import { AchievementsComponent } from './achievements.component';

describe('AchievementsComponent', () => {
  it('renders completed requests without another user interaction', () => {
    const achievements = new Subject<any[]>();
    TestBed.configureTestingModule({
      imports: [AchievementsComponent],
      providers: [{ provide: GamificationService, useValue: {
        getAllAchievements: () => achievements,
        getUserAchievements: () => of([])
      } }]
    });
    const fixture = TestBed.createComponent(AchievementsComponent);
    fixture.detectChanges();

    achievements.next([]);
    achievements.complete();

    expect(fixture.nativeElement.textContent).not.toContain('Başarımlar yükleniyor');
  });

  it('shows a retry action when achievements cannot be loaded', () => {
    TestBed.configureTestingModule({
      imports: [AchievementsComponent],
      providers: [{ provide: GamificationService, useValue: {
        getAllAchievements: () => throwError(() => new Error('Gateway Timeout')),
        getUserAchievements: () => of([])
      } }]
    });
    const fixture = TestBed.createComponent(AchievementsComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.loading).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Başarımlar yüklenemedi');
    expect(fixture.nativeElement.querySelector('button')?.textContent).toContain('Yeniden dene');
  });

  it('stops loading when the achievements request never responds', fakeAsync(() => {
    TestBed.configureTestingModule({
      imports: [AchievementsComponent],
      providers: [{ provide: GamificationService, useValue: {
        getAllAchievements: () => NEVER,
        getUserAchievements: () => of([])
      } }]
    });
    const fixture = TestBed.createComponent(AchievementsComponent);
    spyOn(console, 'error');
    fixture.detectChanges();

    tick(15000);
    fixture.detectChanges();

    expect(fixture.componentInstance.loading).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Başarımlar yüklenemedi');
  }));

  // H19 (audit 30.10.2026): a badge an administrator deactivated stays on the page of the students who earned it, and the
  // headline sums the XP of earned badges under an honest label.
  it('keeps earned badges that were later deactivated and labels the badge XP correctly', () => {
    const active = { id: 'a', name: 'Seri', tier: 'Bronze', category: 'Streak', xpReward: 10, isActive: true };
    const retired = { id: 'r', name: 'Eski Rozet', tier: 'Gold', category: 'Special', xpReward: 50, isActive: false };
    TestBed.configureTestingModule({
      imports: [AchievementsComponent],
      providers: [{ provide: GamificationService, useValue: {
        getAllAchievements: () => of([active]),
        getUserAchievements: () => of([{ id: 'u', userId: 's', achievementId: 'r', achievement: retired, unlockedAt: new Date(), isShowcased: false }])
      } }]
    });
    const fixture = TestBed.createComponent(AchievementsComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.achievements.map(item => item.name)).toEqual(['Seri', 'Eski Rozet']);
    expect(component.unlockedCount).toBe(1);
    expect(component.totalXPEarned).toBe(50);
    expect(fixture.nativeElement.textContent).toContain('Rozet XP');
    expect(fixture.nativeElement.textContent).not.toContain('Toplam XP');
  });
});
