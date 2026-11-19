import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MeasuredProgressService, toMeasuredProgress } from './measured-progress.service';
import { GamificationService } from './gamification.service';

// Live test 19.11.2026 (A3): one measured source for the home page and reports; widgets share one request.
describe('MeasuredProgressService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('treats missing or zero values as not measured', () => {
    const empty = toMeasuredProgress({ currentReadingSpeed: 0, averageSuccessRate: 0, completedExercises: 3 });
    expect(empty.readingSpeed).toBeNull();
    expect(empty.comprehension).toBeNull();
    expect(empty.completedExercises).toBe(3);

    const measured = toMeasuredProgress({ currentReadingSpeed: 311.6, initialReadingSpeed: 250.2, averageSuccessRate: 87.5 });
    expect(measured.readingSpeed).toBe(312);
    expect(measured.initialReadingSpeed).toBe(250);
    expect(measured.comprehension).toBe(87.5);
    expect(toMeasuredProgress(null).readingSpeed).toBeNull();
  });

  it('shares one request between widgets that load together', () => {
    const service = TestBed.inject(MeasuredProgressService);
    const values: (number | null)[] = [];
    service.load(false, 1_000).subscribe(value => values.push(value.readingSpeed));
    service.load(false, 2_000).subscribe(value => values.push(value.readingSpeed));
    http.expectOne(request => request.url.endsWith('/daily-progress/my-progress')).flush({ currentReadingSpeed: 312 });
    expect(values).toEqual([312, 312]);

    service.load(false, 30_000).subscribe();
    http.expectOne(request => request.url.endsWith('/daily-progress/my-progress')).flush({ currentReadingSpeed: 320 });
  });

  it('shows "not measured" instead of failing when the request fails', () => {
    let speed: number | null | undefined;
    TestBed.inject(MeasuredProgressService).load(false, 1_000).subscribe(value => speed = value.readingSpeed);
    http.expectOne(request => request.url.endsWith('/daily-progress/my-progress')).flush('x', { status: 500, statusText: 'Server Error' });
    expect(speed).toBeNull();
  });

  it('shares gamification reads made together and asks again after a write', () => {
    const service = TestBed.inject(GamificationService);
    service.getUserAchievements().subscribe();
    service.getUserAchievements().subscribe();
    http.expectOne(request => request.url.endsWith('/gamification/achievements/user')).flush([]);

    service.useStreakFreeze().subscribe();
    http.expectOne(request => request.url.endsWith('/gamification/streak/use-freeze')).flush({ success: true });
    service.getUserAchievements().subscribe();
    http.expectOne(request => request.url.endsWith('/gamification/achievements/user')).flush([]);
  });
});
