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
});
