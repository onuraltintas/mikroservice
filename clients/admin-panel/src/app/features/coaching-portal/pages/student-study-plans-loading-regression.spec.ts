import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of, Subject } from 'rxjs';
import { CoachingStudyPlanningService, StudyPlan, StudyPlanPage } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyPlansComponent } from './student-study-plans.component';

describe('Study plan loading regression', () => {
  for (const staffStudentId of [null, 'assigned-student']) {
    it(`preserves the new manual draft when the active-plan lookup finishes late (${staffStudentId ? 'teacher' : 'student'})`, () => {
      const firstPage = new Subject<StudyPlanPage>();
      const activePage = new Subject<StudyPlanPage>();
      const active: StudyPlan = { id: 'old-active', title: 'Existing plan', status: 'Active', version: 1, tasks: [] };
      const service = {
        list: vi.fn((_page: number, status?: string) => status === 'Active' ? activePage : firstPage),
        get: vi.fn(() => of(active))
      };
      TestBed.configureTestingModule({ imports: [StudentStudyPlansComponent], providers: [
        { provide: CoachingStudyPlanningService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
        { provide: Router, useValue: { navigate: vi.fn(() => Promise.resolve(true)) } }
      ] });
      const fixture = TestBed.createComponent(StudentStudyPlansComponent);
      const component = fixture.componentInstance;
      component.staffStudentId = staffStudentId;
      component.autoOpenActive = true;
      fixture.detectChanges();
      firstPage.next({ items: [], totalCount: 20, pageNumber: 1, pageSize: 20 });
      firstPage.complete();
      component.newDraft();
      component.editTitle = 'Unsaved new plan';
      component.editTasks[0].title = 'Unsaved work';
      activePage.next({ items: [active], totalCount: 1, pageNumber: 1, pageSize: 20 });
      activePage.complete();
      expect(component.editing).toBe(true);
      expect(component.editTitle).toBe('Unsaved new plan');
      expect(component.editTasks[0].title).toBe('Unsaved work');
      expect(component.selected()).toBeNull();
      expect(service.get).not.toHaveBeenCalled();
      fixture.destroy();
    });
  }
});
