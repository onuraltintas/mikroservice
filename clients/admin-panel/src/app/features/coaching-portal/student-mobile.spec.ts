import { CoachingInstitutionExamRanksService } from '../../core/services/coaching-institution-exam-ranks.service';
import { Component, signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../core/auth/auth.service';
import { CoachingPortalService } from '../../core/services/coaching-portal.service';
import { CoachingStudentNavigationService } from '../../core/services/coaching-student-navigation.service';
import { CoachingStudyPlanningService, StudyPlan } from '../../core/services/coaching-study-planning.service';
import { CoachingLearningSetupService } from '../../core/services/coaching-learning-setup.service';
import { CoachingTimeZoneService } from '../../core/services/coaching-time-zone.service';
import { NotificationService } from '../../core/services/notification.service';
import { ToasterService } from '../../core/services/toaster.service';
import { PracticeExamProfile } from '../../core/models/practice-exam';
import { CoachingParentService } from './parent/coaching-parent.service';
import { CoachingStudentTeachersService } from '../../core/services/coaching-student-teachers.service';
import { CoachingDataExportService } from '../../core/services/coaching-data-export.service';
import { coachingFeedback } from './coaching-feedback';
import { CoachingPortalLayoutComponent } from './layout/coaching-portal-layout.component';
import { CoachingWhatsNewService } from './layout/coaching-whats-new.service';
import { CoachingMessagesUnreadService } from '../../core/services/coaching-messages-unread.service';
import { StudentAssignmentsComponent } from './pages/student-assignments.component';
import { StudentSettingsComponent } from './pages/student-settings.component';
import { PracticeExamEditorComponent } from './pages/practice-exam-editor.component';
import { StudentStudyPlansComponent } from './pages/student-study-plans.component';
import { StudentExamResultsComponent } from './pages/student-exam-results.component';
import { CoachingStudentExamsService } from '../../core/services/coaching-student-exams.service';

// Roadmap O1: the student's screens on the phone.
const toaster = { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() };
beforeEach(() => { vi.clearAllMocks(); TestBed.overrideProvider(ToasterService, { useValue: toaster }); });

const student: UserProfile = { id: 'student-1', email: 's@example.test', firstName: 'Ada', lastName: 'Öğrenci',
  username: 's@example.test', roles: ['Student'], role: 'Student', permissions: [] };
const istanbulToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());

// T2 (31.10.2026): the phone bar is Bugün · Planım · Ödevler · Gelişimim · Mesajlar; notifications live behind the bell.
describe('O1 bottom navigation', () => {
  async function layout(showAssignments = true, unread = 3, messages = 2) {
    TestBed.configureTestingModule({ imports: [CoachingPortalLayoutComponent], providers: [provideRouter([]),
      { provide: AuthService, useValue: { userProfile: signal<any>({ roles: ['Student'], product: 'coaching', firstName: 'Ada' }) } },
      { provide: NotificationService, useValue: { unreadCount: signal(unread) } },
      { provide: CoachingMessagesUnreadService, useValue: { count: signal(messages), refresh: () => {} } },
      { provide: CoachingWhatsNewService, useValue: { visible: () => false, dismiss: () => {} } },
      { provide: CoachingStudentNavigationService, useValue: { showAssignments: () => showAssignments, showSessions: () => true, showFees: () => true, refresh: () => {} } }] });
    const fixture = TestBed.createComponent(CoachingPortalLayoutComponent); fixture.detectChanges(); await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('puts today, the plan, assignments, growth and messages under the thumb with the unread message count', async () => {
    const page = await layout();
    const links = [...page.querySelectorAll('.coaching-mobile-nav a')] as HTMLAnchorElement[];
    expect(links.map(a => a.getAttribute('href'))).toEqual(['/coaching-portal', '/coaching-portal/plan', '/coaching-portal/assignments', '/coaching-portal/growth', '/coaching-portal/messages']);
    expect(links.map(a => a.textContent?.replace(/\d+/g, '').trim())).toEqual(['Bugün', 'Planım', 'Ödevler', 'Gelişimim', 'Mesajlar']);
    expect(page.querySelector('.coaching-mobile-nav .coaching-badge')?.textContent?.trim()).toBe('2');
    expect(links[4].getAttribute('aria-label')).toBe('Mesajlar (2 okunmamış)');
    expect(page.querySelector('.coaching-bell')?.getAttribute('aria-label')).toBe('Bildirimler (3 okunmamış)');
    expect(page.querySelector('.coaching-mobile-nav a[href="/coaching-portal/notifications"]')).toBeNull();
  });

  it('drops assignments from the bar when the student has none and hides an empty badge', async () => {
    const page = await layout(false, 0, 0);
    const hrefs = [...page.querySelectorAll('.coaching-mobile-nav a')].map(a => a.getAttribute('href'));
    expect(hrefs).toEqual(['/coaching-portal', '/coaching-portal/plan', '/coaching-portal/growth', '/coaching-portal/messages']);
    expect(page.querySelector('.coaching-badge')).toBeNull();
  });

  it('keeps the plan wizard out of the menu; it is a tab under Planım', async () => {
    const page = await layout();
    const labels = [...page.querySelectorAll('.coaching-navigation a')].map(a => a.textContent?.replace(/\d+/g, '').trim());
    expect(labels).toEqual(['Bugün', 'Planım', 'Ödevlerim', 'Gelişimim', 'Mesajlar', 'Seanslar']);
    expect(labels).not.toContain('Otomatik plan');
  });
});

describe('O1 assignment list', () => {
  function setup() {
    const service = { getStudentAssignments: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 })) };
    TestBed.configureTestingModule({ imports: [StudentAssignmentsComponent], providers: [provideRouter([]),
      { provide: AuthService, useValue: { userProfile: signal(student) } }, { provide: CoachingPortalService, useValue: service }] });
    const fixture = TestBed.createComponent(StudentAssignmentsComponent); fixture.detectChanges();
    return { component: fixture.componentInstance, service };
  }

  it('opens with waiting and late work and filters on the server', () => {
    const { component, service } = setup();
    expect(component.filter()).toBe('open');
    expect(service.getStudentAssignments).toHaveBeenCalledWith('student-1', 1, 25, 'open');
    component.pageNumber.set(3); component.setFilter('submitted');
    expect(service.getStudentAssignments).toHaveBeenLastCalledWith('student-1', 1, 25, 'submitted');
    expect(component.filters.map(x => x.value)).toEqual(['open', 'overdue', 'pending', 'submitted', 'cancelled', 'all']);
  });

  it('sends the filter to the API and leaves it out for the full list', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CoachingPortalService); const http = TestBed.inject(HttpTestingController);
    service.getStudentAssignments('11111111-1111-1111-1111-111111111111', 2, 25, 'overdue').subscribe();
    expect(http.expectOne(r => r.url.endsWith('/assignments/student/11111111-1111-1111-1111-111111111111')).request.params.get('status')).toBe('overdue');
    service.getStudentAssignments('11111111-1111-1111-1111-111111111111', 1, 25, 'all').subscribe();
    expect(http.expectOne(r => r.url.endsWith('/assignments/student/11111111-1111-1111-1111-111111111111')).request.params.has('status')).toBe(false);
    http.verify();
  });
});

describe('O1 one message channel', () => {
  @Component({ standalone: true, template: `@if (error()) { <p role="alert">{{ error() }}</p> }` })
  class Host { readonly error = signal<string | null>(null); private readonly feedback = coachingFeedback(undefined, this.error); }

  it('shows a message once beside the form and brings it into view instead of a second pop-up', async () => {
    const scroll = vi.fn(); Element.prototype.scrollIntoView = scroll;
    TestBed.configureTestingModule({ imports: [Host] });
    const fixture = TestBed.createComponent(Host); fixture.detectChanges();
    fixture.componentInstance.error.set('Bağlantını kontrol et.'); fixture.detectChanges(); await fixture.whenStable();
    await vi.waitFor(() => expect(scroll).toHaveBeenCalledTimes(1));
    expect(scroll.mock.contexts[0]).toBe(fixture.nativeElement.querySelector('[role=alert]'));
    expect(toaster.error).not.toHaveBeenCalled(); expect(toaster.success).not.toHaveBeenCalled();
  });
});

describe('O1 time zone', () => {
  it('chooses the time zone from a list with the saved zone and Istanbul suggested first', async () => {
    const api = { get: vi.fn(() => of({ timeZoneId: 'Europe/Berlin', availabilityVersion: 1, draftVersion: 1 })), save: vi.fn() };
    TestBed.configureTestingModule({ imports: [StudentSettingsComponent], providers: [
      { provide: CoachingTimeZoneService, useValue: api }, { provide: CoachingParentService, useValue: { getLinks: () => of([]) } }, { provide: CoachingStudentTeachersService, useValue: { list: () => of([]) } }, { provide: CoachingDataExportService, useValue: {} }] });
    const fixture = TestBed.createComponent(StudentSettingsComponent); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('input[name=timeZone]')).toBeNull();
    const select = page.querySelector('select[name=timeZone]') as HTMLSelectElement;
    const suggested = [...select.querySelectorAll('optgroup[label="Önerilen"] option')] as HTMLOptionElement[];
    expect(suggested[0].value).toBe('Europe/Istanbul');
    expect(suggested[0].textContent).toContain('İstanbul');
    expect(suggested[0].textContent).toContain('GMT+3');
    expect(suggested.map(x => x.value)).toContain('Europe/Berlin');
    expect(select.value).toBe('Europe/Berlin');
  });
});

describe('O1 practice exam form', () => {
  const profiles: PracticeExamProfile[] = [{ code: 'TYT', name: 'TYT', wrongAnswerDivisor: 4, sections: [{ code: 'MAT', name: 'Matematik', questionCount: 40 }] }];

  it('asks only for correct and wrong answers and shows the net right away', async () => {
    TestBed.configureTestingModule({ imports: [PracticeExamEditorComponent] });
    const fixture = TestBed.createComponent(PracticeExamEditorComponent); const editor = fixture.componentInstance;
    editor.profiles = profiles; editor.valueChange.subscribe(value => fixture.componentRef.setInput('value', value));
    fixture.detectChanges(); editor.choose('TYT'); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelectorAll('tbody input').length).toBe(2);
    editor.edit('MAT', 'correct', 30); fixture.detectChanges(); editor.edit('MAT', 'wrong', 8); fixture.detectChanges(); await fixture.whenStable();
    expect(editor.value?.sections[0]).toEqual({ sectionCode: 'MAT', correct: 30, wrong: 8, empty: 2 });
    expect(fixture.nativeElement.querySelector('tbody').textContent).toContain('28');
    expect(fixture.nativeElement.querySelector('.summary').textContent).toMatch(/28[.,]00 net/);
    editor.edit('MAT', 'wrong', 11); fixture.detectChanges(); await fixture.whenStable();
    expect(editor.totals()).toBeNull();
    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain('Matematik');
  });

  it('names and dates a new practice exam result for the student', async () => {
    const service = { profiles: vi.fn(() => of(profiles)), list: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 })), create: vi.fn(), replace: vi.fn(), delete: vi.fn(), get: vi.fn() };
    TestBed.configureTestingModule({ imports: [StudentExamResultsComponent], providers: [{ provide: CoachingInstitutionExamRanksService, useValue: { mine: () => of([]) } },
      { provide: CoachingStudentExamsService, useValue: service }, { provide: CoachingStudyPlanningService, useValue: { searchTopics: vi.fn() } },
      { provide: CoachingLearningSetupService, useValue: { get: () => of({ context: null }) } }] });
    const fixture = TestBed.createComponent(StudentExamResultsComponent); fixture.detectChanges(); await fixture.whenStable();
    const component = fixture.componentInstance;
    await component.practiceChanged({ examCode: 'TYT', sections: [{ sectionCode: 'MAT', correct: 0, wrong: 0, empty: 40 }] });
    expect(component.title).toBe('TYT denemesi');
    expect(component.examDate).toBe(istanbulToday());
    component.title = 'Kendi adım'; await component.practiceChanged({ examCode: 'TYT', sections: [{ sectionCode: 'MAT', correct: 1, wrong: 0, empty: 39 }] });
    expect(component.title).toBe('Kendi adım');
  });
});

describe('O1 plan opens on today', () => {
  const plan = (dates: string[]): StudyPlan => ({ id: 'plan', version: 1, title: 'Planım', status: 'Active', tasks: dates.map((plannedDate, i) => ({
    id: `task-${i}`, plannedDate, title: `Çalışma ${i}`, plannedMinutes: 30, topicId: null, isPinned: false, isCompleted: false, actualMinutes: null, completedAt: null })) });
  async function open(value: StudyPlan, staffStudentId: string | null = null) {
    const service = { list: vi.fn(() => of({ items: [{ ...value, tasks: undefined }], totalCount: 1, pageNumber: 1, pageSize: 20 })), get: vi.fn(() => of(value)) };
    TestBed.configureTestingModule({ imports: [StudentStudyPlansComponent], providers: [
      { provide: Router, useValue: { navigate: vi.fn() } }, { provide: CoachingStudyPlanningService, useValue: service },
      { provide: CoachingLearningSetupService, useValue: { catalog: () => of({ lessons: [] }), catalogForStudent: () => of({ lessons: [] }) } },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }] });
    const fixture = TestBed.createComponent(StudentStudyPlansComponent);
    if (staffStudentId) { fixture.componentRef.setInput('staffStudentId', staffStudentId); fixture.componentRef.setInput('autoOpenActive', true); }
    fixture.detectChanges(); await fixture.whenStable();
    return fixture.componentInstance;
  }

  it('shows the student today\'s work first', async () => {
    const component = await open(plan(['2020-01-01', istanbulToday(), '2099-01-01']));
    expect(component.taskFilter).toBe('today');
    expect(component.planView()).toBe('list');
    expect(component.visibleTasks().map(x => x.id)).toEqual(['task-1']);
  });

  it('falls back to the whole plan when nothing is planned today and for teachers', async () => {
    expect((await open(plan(['2099-01-01']))).taskFilter).toBe('all');
    TestBed.resetTestingModule(); TestBed.overrideProvider(ToasterService, { useValue: toaster });
    expect((await open(plan([istanbulToday()]), 'student-1')).taskFilter).toBe('all');
  });
});
