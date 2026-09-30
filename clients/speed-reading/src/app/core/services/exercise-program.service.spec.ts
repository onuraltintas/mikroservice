import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ExerciseProgramService } from './exercise-program.service';

describe('assigned program summary', () => {
  it('preserves completion after the student reloads the dashboard', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(ExerciseProgramService);
    const http = TestBed.inject(HttpTestingController);
    service.getMyProgress().subscribe(progress => {
      expect(progress?.completedDate).toBe('2026-09-30T12:00:00Z');
      expect(progress?.templateName).toBe('My program');
      expect(progress?.totalDays).toBe(28);
    });
    http.expectOne(request => request.url.endsWith('/progress/programs')).flush([
      { templateName: 'My program', totalDays: 28, totalWeeks: 4,
        completedDate: '2026-09-30T12:00:00Z', isActive: false }
    ]);
    http.verify();
  });
});
