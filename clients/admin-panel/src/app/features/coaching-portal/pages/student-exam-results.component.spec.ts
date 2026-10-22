import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CoachingStudentExamsService } from '../../../core/services/coaching-student-exams.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentExamResultsComponent } from './student-exam-results.component';

describe('StudentExamResultsComponent', () => {
  async function setup() {
    const service = { profiles: vi.fn(() => of([])), list: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 })),
      create: vi.fn(() => of({ id: 'exam', version: 1 })), replace: vi.fn(), delete: vi.fn(), get: vi.fn() };
    const topics = { searchTopics: vi.fn(() => of({ items: [{ id: 'topic', lessonId: 'lesson', name: 'Fractions', lessonName: 'Math' }], totalCount: 1, pageNumber: 1, pageSize: 20 })) };
    TestBed.configureTestingModule({ imports: [StudentExamResultsComponent], providers: [
      { provide: CoachingStudentExamsService, useValue: service }, { provide: CoachingStudyPlanningService, useValue: topics }] });
    const fixture = TestBed.createComponent(StudentExamResultsComponent); fixture.detectChanges(); await fixture.whenStable();
    return { component: fixture.componentInstance, fixture, service };
  }
  it('requires explicit score and confirmation, uses topic catalog IDs, and prevents duplicate rows', async () => {
    const { component, service } = await setup();
    component.title = 'My mock'; component.examDate = '2026-10-01'; component.save();
    expect(service.create).not.toHaveBeenCalled();
    component.score = 80; component.searchTopics(); component.addTopic('unknown'); component.addTopic('topic'); component.addTopic('topic');
    expect(component.lessons.length).toBe(1);
    Object.assign(component.lessons[0], { questionCount: 10, correct: 8, wrong: 1, empty: 1 });
    component.confirmed = true; component.save();
    expect(service.create).toHaveBeenCalledWith(expect.objectContaining({ correctAnswers: 8, lessons: [expect.objectContaining({ lessonId: 'lesson', topicId: 'topic' })] }));
    expect(await component.page().totalCount).toBe(1);
  });
  it('does not present a failed result load as an empty account', async () => {
    const { component, fixture, service } = await setup();
    service.list.mockReturnValueOnce(throwError(() => ({ status: 500 })));
    component.loadList(); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Henüz kendi girdiğin bir sonuç yok.');
  });
  it('rejects inconsistent counts and impossible dates and invalidates confirmation on edits', async () => {
    const { component, service } = await setup();
    component.title = 'Mock'; component.score = 80; component.examDate = '2026-02-30'; component.confirmed = true; component.save();
    expect(service.create).not.toHaveBeenCalled();
    component.examDate = '2026-10-01'; component.searchTopics(); component.addTopic('topic');
    Object.assign(component.lessons[0], { questionCount: 10, correct: 9, wrong: 1, empty: 1 });
    component.confirmed = true; component.save(); expect(service.create).not.toHaveBeenCalled();
    component.changed(); expect(component.confirmed).toBe(false);
  });
  it('allows lesson totals without mixing them with topic rows from the same lesson', async () => {
    const { component, fixture } = await setup();
    component.searchTopics(); fixture.detectChanges(); await fixture.whenStable();
    const button = [...fixture.nativeElement.querySelectorAll('button')].find((x: any) => x.textContent.includes('Ders toplamını ekle')) as HTMLButtonElement | undefined;
    expect(button).toBeDefined();
    button!.click(); fixture.detectChanges(); await fixture.whenStable();
    expect(component.lessons).toEqual([expect.objectContaining({ lessonId: 'lesson', topicId: null })]);
    component.addTopic('topic'); expect(component.lessons.length).toBe(1);
  });
  it('locks pending writes and keeps the draft after conflict without allowing a blind retry', async () => {
    const { component, service } = await setup();
    const pending = new Subject<any>(); service.create.mockReturnValueOnce(pending);
    component.title = 'Mock'; component.score = 80; component.examDate = '2026-10-01'; component.confirmed = true;
    component.save(); component.save(); expect(service.create).toHaveBeenCalledTimes(1);
    expect(await component.canLeavePage()).toBe(false);
    pending.error({ status: 409 });
    expect(component.title).toBe('Mock'); component.confirmed = true; component.save();
    expect(service.create).toHaveBeenCalledTimes(1);
  });
  it('saves server-profile totals and reloads the saved scope without guessing a score', async () => {
    const { component, fixture, service } = await setup();
    component.profiles.set([{ code: 'TYT', name: 'TYT', wrongAnswerDivisor: 4, sections: [{ code: 'MAT', name: 'Matematik', questionCount: 40 }] }]);
    const practice = { examCode: 'TYT', sections: [{ sectionCode: 'MAT', correct: 30, wrong: 4, empty: 6 }] };
    const saved = { id: 'saved', version: 1, title: 'TYT denemem', examType: 'YKS', examDate: '2026-10-01', score: 300, maxScore: 500,
      correctAnswers: 30, wrongAnswers: 4, emptyAnswers: 6, lessons: [], practiceExam: practice, source: 'StudentReported' };
    service.create.mockReturnValueOnce(of(saved)); service.get.mockReturnValueOnce(of(saved));
    await component.practiceChanged(practice); component.title = saved.title; component.examDate = saved.examDate;
    component.score = 300; component.maxScore = 500; component.confirmed = true; component.save();
    expect(service.create).toHaveBeenCalledWith(expect.objectContaining({ practiceExam: practice, examType: 'YKS', correctAnswers: 30, wrongAnswers: 4, emptyAnswers: 6, score: 300 }));
    await component.open('saved'); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); await fixture.whenStable();
    expect(component.practiceExam).toEqual(practice);
    expect(fixture.nativeElement.querySelector('app-practice-exam-editor select').disabled).toBe(true);
  });
  it('blocks invalid practice totals and resets the scope for a new result', async () => {
    const { component, service } = await setup();
    component.practiceExam = { examCode: 'TYT', sections: [] }; component.confirmed = true; component.save();
    expect(service.create).not.toHaveBeenCalled(); expect(component.error()).toContain('doğru/yanlış/boş');
    component.dirty = false; await component.newExam(); expect(component.practiceExam).toBeNull();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });

beforeEach(()=>TestBed.overrideProvider(CoachingLearningSetupService,{useValue:{get:()=>of({context:null})}}));
