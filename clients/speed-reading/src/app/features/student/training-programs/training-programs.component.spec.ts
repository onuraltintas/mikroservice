import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { TrainingProgramsComponent } from './training-programs.component';

describe('Staff training programs', () => {
  it('stacks the title and description despite shared header flex styles', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(TrainingProgramsComponent);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(request => request.url.endsWith('/staff-training/programs')).flush([]);
    http.expectOne(request => request.url.endsWith('/student-program/my-programs')).flush([]);
    const sharedStyle = document.createElement('style');
    sharedStyle.textContent = '.page-header { display:flex; align-items:center; justify-content:space-between; }';
    document.head.appendChild(sharedStyle);
    document.body.appendChild(fixture.nativeElement);
    try {
      const header = fixture.nativeElement.querySelector('header') as HTMLElement;
      expect(header.classList.contains('page-header')).toBeFalse();
      expect(getComputedStyle(header).display).toBe('flex');
      expect(getComputedStyle(header).flexDirection).toBe('column');
      expect(getComputedStyle(header).alignItems).toBe('flex-start');
    } finally {
      sharedStyle.remove();
      fixture.nativeElement.remove();
    }
    http.verify();
  });
  it('explains locked enrollment and clearly distinguishes the active program', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(TrainingProgramsComponent);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(request => request.url.endsWith('/staff-training/programs')).flush([
      { id: 'template', name: 'Program', description: 'Açıklama', totalDays: 14, programType: 0 }]);
    http.expectOne(request => request.url.endsWith('/student-program/my-programs')).flush([
      { progressId: 'progress', templateName: 'Aktif eğitim', isActive: true,
        currentWeek: 1, currentDay: 2, totalDaysCompleted: 1 }]);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('Önce aktif programınızı tamamlayın');
    expect(element.querySelector('.active-program h2')?.textContent).toContain('Aktif eğitim');
    expect(element.querySelector<HTMLButtonElement>('.program-card button')?.disabled).toBeTrue();
    expect(element.querySelector('.active-program a')?.getAttribute('href')).toBe('/student/daily-exercises');
    http.verify();
  });
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
