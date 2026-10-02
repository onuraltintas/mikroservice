import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CoachingStudentExamsService } from '../../../core/services/coaching-student-exams.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentExamResultsComponent } from './student-exam-results.component';

describe('StudentExamResultsComponent', () => {
  function setup() {
    const service = { list: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 })),
      create: vi.fn(() => of({ id: 'exam', version: 1 })), replace: vi.fn(), delete: vi.fn(), get: vi.fn() };
    const topics = { searchTopics: vi.fn(() => of({ items: [{ id: 'topic', lessonId: 'lesson', name: 'Fractions', lessonName: 'Math' }], totalCount: 1, pageNumber: 1, pageSize: 20 })) };
    TestBed.configureTestingModule({ imports: [StudentExamResultsComponent], providers: [
      { provide: CoachingStudentExamsService, useValue: service }, { provide: CoachingStudyPlanningService, useValue: topics }] });
    const fixture = TestBed.createComponent(StudentExamResultsComponent); fixture.detectChanges();
    return { component: fixture.componentInstance, fixture, service };
  }
  it('requires explicit score and confirmation, uses topic catalog IDs, and prevents duplicate rows', () => {
    const { component, service } = setup();
    component.title = 'My mock'; component.examDate = '2026-10-01'; component.save();
    expect(service.create).not.toHaveBeenCalled();
    component.score = 80; component.searchTopics(); component.addTopic('unknown'); component.addTopic('topic'); component.addTopic('topic');
    expect(component.lessons.length).toBe(1);
    Object.assign(component.lessons[0], { questionCount: 10, correct: 8, wrong: 1, empty: 1 });
    component.confirmed = true; component.save();
    expect(service.create).toHaveBeenCalledWith(expect.objectContaining({ correctAnswers: 8, lessons: [expect.objectContaining({ lessonId: 'lesson', topicId: 'topic' })] }));
    expect(component.page().totalCount).toBe(1);
  });
  it('does not present a failed result load as an empty account', () => {
    const { component, fixture, service } = setup();
    service.list.mockReturnValueOnce(throwError(() => ({ status: 500 })));
    component.loadList(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Henüz kendi girdiğin bir sonuç yok.');
  });
  it('rejects inconsistent counts and impossible dates and invalidates confirmation on edits', () => {
    const { component, service } = setup();
    component.title = 'Mock'; component.score = 80; component.examDate = '2026-02-30'; component.confirmed = true; component.save();
    expect(service.create).not.toHaveBeenCalled();
    component.examDate = '2026-10-01'; component.searchTopics(); component.addTopic('topic');
    Object.assign(component.lessons[0], { questionCount: 10, correct: 9, wrong: 1, empty: 1 });
    component.confirmed = true; component.save(); expect(service.create).not.toHaveBeenCalled();
    component.changed(); expect(component.confirmed).toBe(false);
  });
  it('allows lesson totals without mixing them with topic rows from the same lesson', () => {
    const { component, fixture } = setup();
    component.searchTopics(); fixture.detectChanges();
    const button = [...fixture.nativeElement.querySelectorAll('button')].find((x: any) => x.textContent.includes('Ders toplamını ekle')) as HTMLButtonElement | undefined;
    expect(button).toBeDefined();
    button!.click(); fixture.detectChanges();
    expect(component.lessons).toEqual([expect.objectContaining({ lessonId: 'lesson', topicId: null })]);
    component.addTopic('topic'); expect(component.lessons.length).toBe(1);
  });
  it('locks pending writes and keeps the draft after conflict without allowing a blind retry', () => {
    const { component, service } = setup();
    const pending = new Subject<any>(); service.create.mockReturnValueOnce(pending);
    component.title = 'Mock'; component.score = 80; component.examDate = '2026-10-01'; component.confirmed = true;
    component.save(); component.save(); expect(service.create).toHaveBeenCalledTimes(1);
    expect(component.canLeavePage()).toBe(false);
    pending.error({ status: 409 });
    expect(component.title).toBe('Mock'); component.confirmed = true; component.save();
    expect(service.create).toHaveBeenCalledTimes(1);
  });
});
