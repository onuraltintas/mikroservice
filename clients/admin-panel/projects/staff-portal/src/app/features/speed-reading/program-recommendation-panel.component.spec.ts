import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ProgramRecommendationPanelComponent } from './program-recommendation-panel.component';

describe('staff next program approval', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [ProgramRecommendationPanelComponent],
    providers: [provideHttpClient(), provideHttpClientTesting()] }));

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('loads on request and approves with the displayed cycle references and institution scope', () => {
    const fixture = TestBed.createComponent(ProgramRecommendationPanelComponent);
    fixture.componentRef.setInput('studentId', 'student');
    fixture.componentRef.setInput('institutionId', 'institution');
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectNone(request => request.url.includes('program-management'));
    fixture.componentInstance.load();
    const get = http.expectOne(request => request.url.endsWith('/program-management/students/student/next-recommendation'));
    expect(get.request.params.get('institutionId')).toBe('institution');
    get.flush({ templateId: 'next', sourceProgressId: 'old', assessmentAttemptId: 'post',
      templateName: 'Next program', totalDays: 28, requiresStaffApproval: true });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Next program');
    fixture.componentInstance.approve();
    const post = http.expectOne(request => request.url.endsWith('/program-management/students/student/approve-next'));
    expect(post.request.body).toEqual({ templateId: 'next', sourceProgressId: 'old', assessmentAttemptId: 'post' });
    expect(post.request.params.get('institutionId')).toBe('institution');
    post.flush({ success: true });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Program onaylandı');
  });

  it('shows and approves the students requested higher program with explicit acknowledgement', () => {
    const fixture = TestBed.createComponent(ProgramRecommendationPanelComponent);
    fixture.componentRef.setInput('studentId', 'student');
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    fixture.componentInstance.load();
    http.expectOne(request => request.url.endsWith('/next-recommendation')).flush({ templateId: 'normal', sourceProgressId: 'old',
      assessmentAttemptId: 'post', templateName: 'Normal program', totalDays: 28, requiresStaffApproval: true,
      requestedTemplateId: 'upper', requestedAt: '2026-10-12T12:00:00Z',
      higherOption: { templateId: 'upper', templateName: 'Upper program', level: 4, totalDays: 28 } });
    fixture.detectChanges();
    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(element => element.textContent?.includes('Öğrencinin seçimini onayla'));
    expect(button).toBeDefined();
    button?.click();
    const post = http.expectOne(request => request.url.endsWith('/approve-next'));
    expect(post.request.body).toEqual({ templateId: 'upper', sourceProgressId: 'old', assessmentAttemptId: 'post', overrideAcknowledged: true });
    post.flush({ success: true });
  });
});
