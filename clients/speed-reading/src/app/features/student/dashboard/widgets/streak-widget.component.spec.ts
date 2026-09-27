import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { GamificationService } from '../../../../core/services/gamification.service';
import { StreakWidgetComponent } from './streak-widget.component';

describe('StreakWidgetComponent', () => {
  it('shows the current week from Monday and does not mark future days active', () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 8, 24, 12)); // Thursday
    try {
      TestBed.configureTestingModule({
        imports: [StreakWidgetComponent],
        providers: [{ provide: GamificationService, useValue: { getUserGameification: () => of(null) } }]
      });
      const component = TestBed.createComponent(StreakWidgetComponent).componentInstance;
      component.currentStreak.set(4);

      expect(component.getLast7Days().map(day => day.label)).toEqual(['P', 'S', 'Ç', 'P', 'C', 'C', 'P']);
      expect(component.getLast7Days().map(day => day.isToday)).toEqual([false, false, false, true, false, false, false]);
      expect(component.getLast7Days().slice(4).every(day => !day.active)).toBeTrue();
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
