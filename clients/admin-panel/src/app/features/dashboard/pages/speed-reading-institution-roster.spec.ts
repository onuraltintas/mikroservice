import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { InstitutionService } from '../../../core/services/institution.service';
import { SpeedReadingInstitutionRosterService } from '../../../core/services/speed-reading-institution-roster.service';
import { SpeedReadingInstitutionRosterComponent } from './speed-reading-institution-roster';

describe('SpeedReadingInstitutionRosterComponent', () => {
  function create(roster: object) {
    TestBed.configureTestingModule({ providers: [
      { provide: PLATFORM_ID, useValue: 'server' },
      { provide: ActivatedRoute, useValue: {} },
      { provide: InstitutionService, useValue: {} },
      { provide: SpeedReadingInstitutionRosterService, useValue: roster }
    ] });
    const component = TestBed.runInInjectionContext(() => new SpeedReadingInstitutionRosterComponent());
    component.institutionId = 'institution-1';
    return component;
  }

  it('ignores stale search results', () => {
    const oldResult = new Subject<any>();
    const newResult = new Subject<any>();
    const getMembers = vi.fn().mockReturnValueOnce(oldResult).mockReturnValueOnce(newResult);
    const component = create({ getMembers });
    component.load();
    component.search = 'Ada';
    component.applyFilters();
    newResult.next({ items: [{ userId: 'ada', teacherUserId: null }], totalCount: 1 });
    oldResult.next({ items: [{ userId: 'old', teacherUserId: null }], totalCount: 99 });
    expect(component.members().map(member => member.userId)).toEqual(['ada']);
    expect(component.totalCount()).toBe(1);
  });

  it('assigns the selected teacher in the current institution and reloads the roster', () => {
    const assignStudent = vi.fn(() => of(null));
    const getMembers = vi.fn(() => of({ items: [], totalCount: 0 }));
    const component = create({ assignStudent, getMembers });
    component.teacherDraft['student-1'] = 'teacher-1';
    component.assign({ userId: 'student-1' } as any);
    expect(assignStudent).toHaveBeenCalledWith('institution-1', 'teacher-1', 'student-1');
    expect(getMembers).toHaveBeenCalled();
    expect(component.saving()).toBe(false);
    expect(component.notice()).toBe('Öğretmen bağlantısı güncellendi.');
  });

  it('keeps the roster when a mutation fails and displays an error', () => {
    const component = create({ removeStudent: () => throwError(() => new Error('denied')) });
    const member = { userId: 'student-1', teacherUserId: 'teacher-1' } as any;
    component.members.set([member]);
    component.remove(member);
    expect(component.members()).toEqual([member]);
    expect(component.saving()).toBe(false);
    expect(component.error()).toContain('İşlem tamamlanamadı');
    expect(component.notice()).toBeNull();
  });
});
