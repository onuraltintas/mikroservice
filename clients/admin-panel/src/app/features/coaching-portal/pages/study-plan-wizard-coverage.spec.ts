import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingWeeklyDraftService } from '../../../core/services/coaching-weekly-draft.service';
import { StudentStudyPlanAssistantComponent } from './student-study-plan-assistant.component';
import { createStudyPlanProposal } from './study-plan-wizard';
import { Weekday } from './weekly-calendar';

describe('Wizard lesson coverage regression', () => {
  const days: Weekday[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  const lessons = Array.from({ length: 16 }, (_, i) => ({ id: `lesson-${i + 1}`, name: `Ders ${i + 1}`, examCode: 'AYT' }));

  it('includes every selected lesson when a second weekly recommendation adds enough slots', () => {
    const first = createStudyPlanProposal({ days, startMinute: 480, minutesPerDay: 120 }, lessons, []);
    const before = JSON.stringify(first);
    const second = createStudyPlanProposal({ days, startMinute: 600, minutesPerDay: 120 }, lessons, first);
    expect(new Set([...first, ...second].map(slot => slot.lessonId)).size).toBe(16);
    expect(second.slice(0, 2).map(slot => slot.lessonId)).toEqual(['lesson-15', 'lesson-16']);
    expect(JSON.stringify(first)).toBe(before);
  });

  it('prioritizes a lesson with no existing weekly time without changing the lesson list', () => {
    const selected = lessons.slice(0, 2);
    const before = JSON.stringify(selected);
    const existing = createStudyPlanProposal({ days: ['Monday'], startMinute: 480, minutesPerDay: 60 }, selected, []);
    const next = createStudyPlanProposal({ days: ['Tuesday'], startMinute: 480, minutesPerDay: 60 }, selected, existing);
    expect(next[0].lessonId).toBe('lesson-2');
    expect(JSON.stringify(selected)).toBe(before);
  });

  for (const teacher of [false, true]) {
    it(`names selected lessons that still have no slot (${teacher ? 'teacher' : 'student'})`, () => {
      TestBed.configureTestingModule({ imports: [StudentStudyPlanAssistantComponent], providers: [
        provideRouter([]),
        { provide: CoachingWeeklyDraftService, useValue: {} },
        { provide: CoachingLearningSetupService, useValue: {} }
      ] });
      const fixture = TestBed.createComponent(StudentStudyPlanAssistantComponent);
      const component = fixture.componentInstance;
      component.staffStudentId = teacher ? 'assigned-student' : null;
      component.busy.set(false);
      component.lessons.set(lessons);
      component.learningContext.set({ id: 'context', version: 1, academicYearStart: 2026, purpose: 'Yks', targetExamYear: 2027,
        primaryScoreType: 'SOZ', foreignLanguage: null, curriculumVersionId: null, topicStates: [], startingStatusReviewed: true,
        coursePreferences: lessons.map(lesson => ({ lessonId: lesson.id, priority: 3, selfReportedDifficulty: null })) });
      component.selectedDays.set(days);
      component.startTime.set('08:00');
      component.minutesPerDay.set(120);
      component.createProposal();
      const message = component.messages().at(-1)!.content;
      expect(message).toContain('Ders 15');
      expect(message).toContain('Ders 16');
      expect(message).toContain('henüz ders saati yok');
      component.slots.set(component.proposal());
      component.startTime.set('10:00');
      component.createProposal();
      expect(component.messages().at(-1)!.content).not.toContain('henüz ders saati yok');
      expect(new Set([...component.slots(), ...component.proposal()].map(slot => slot.lessonId)).size).toBe(16);
      fixture.destroy();
    });
  }
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
