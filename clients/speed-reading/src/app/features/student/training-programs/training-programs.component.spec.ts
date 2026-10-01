import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { TrainingProgramsComponent } from './training-programs.component';

describe('Staff training programs', () => {
  it('lists training programs and enrolls explicitly before opening daily training', () => {
    const router = { navigate: jasmine.createSpy('navigate') };
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: Router, useValue: router }] });
    const fixture = TestBed.createComponent(TrainingProgramsComponent);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(request => request.url.endsWith('/staff-training/programs')).flush([
      { id: 'template', name: 'Program', description: 'Açıklama', totalDays: 14, programType: 0 }]);
    http.expectOne(request => request.url.endsWith('/student-program/my-programs')).flush([]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Program');
    fixture.componentInstance.start('template');
    const enrollment = http.expectOne(request => request.url.endsWith('/staff-training/start'));
    expect(enrollment.request.body).toEqual({ templateId: 'template' });
    enrollment.flush({ success: true });
    expect(router.navigate).toHaveBeenCalledWith(['/student/daily-exercises']);
    http.verify();
  });
});
