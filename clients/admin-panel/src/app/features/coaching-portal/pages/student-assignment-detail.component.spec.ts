import { ToasterService } from '../../../core/services/toaster.service';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { AssignmentDetail, CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { StudentAssignmentDetailComponent } from './student-assignment-detail.component';
import { CoachingPortalViewService } from '../coaching-portal-view.service';

describe('StudentAssignmentDetailComponent navigation', () => {
  const profile = signal<UserProfile | null>(null);

  beforeEach(async () => {
    profile.set(null);
    await TestBed.configureTestingModule({
      imports: [StudentAssignmentDetailComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: {} },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: Router, useValue: { navigate: vi.fn() } }
      ]
    }).compileComponents();
  });

  it('returns the student assignment list for a student', () => {
    profile.set(user('Student'));
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;

    expect(component.backRoute()).toBe('/coaching-portal/assignments');
  });

  it('labels submission states in Turkish', () => {
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;
    expect(component.assignmentStatusLabel('Assigned')).toBe('Yapılacak');
    expect(component.assignmentStatusLabel('Submitted')).toBe('Teslim edildi');
    expect(component.assignmentStatusLabel('Graded')).toBe('Değerlendirildi');
  });

  it('shows book instructions for mixed assignments as well as book-only assignments', () => {
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;
    const mixed = { source: 'Mixed' } as AssignmentDetail;

    expect(component.hasBookReference(mixed)).toBe(true);
    expect(component.hasBookReference({ source: 'Digital' } as AssignmentDetail)).toBe(false);
  });

  it('offers a teacher no grading here; grading lives in the staff portal (Roadmap D3)', () => {
    profile.set({ ...user('InstitutionAdmin'), roles: ['InstitutionAdmin', 'Teacher'] });
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance as unknown as Record<string, unknown>;
    expect(component['isStudent'] instanceof Function && (component['isStudent'] as () => boolean)()).toBe(false);
    expect(component['grade']).toBeUndefined();
    expect(component['isTeacher']).toBeUndefined();
  });

  it('lets a dual-role user submit their own assignment in student view', () => {
    profile.set({ ...user('Teacher'), roles: ['Teacher', 'Student'] });
    const view = TestBed.inject(CoachingPortalViewService);
    view.select('Student');
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;
    expect(component.isStudent()).toBe(true);
    expect(component.backRoute()).toBe('/coaching-portal/assignments');
  });
});

describe('StudentAssignmentDetailComponent attachments', () => {
  it('lets the student upload a Markdown document with an empty browser MIME type', async () => {
    const profile = signal<UserProfile | null>(user('Student'));
    const service = {
      calculateSha256: vi.fn(async () => 'hash'),
      createAttachment: vi.fn(() => of({ attachmentId: 'attachment-1' })),
      uploadAttachment: vi.fn(() => of({ status: 'Clean' })),
      getAssignment: vi.fn(() => of({ id: 'assignment-1', status: 'Active', assignedStudents: [] }))
    };
    TestBed.configureTestingModule({
      imports: [StudentAssignmentDetailComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'assignment-1' } } } }
      ]
    });
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;
    component.assignment.set({ id: 'assignment-1', status: 'Active', assignedStudents: [{ studentId: 'user-1', status: 'Assigned' }] } as AssignmentDetail);
    const file = new File(['# My work'], 'work.md', { type: '' });
    const input = { files: [file], value: 'selected' } as unknown as HTMLInputElement;

    await component.onFileSelected({ target: input } as unknown as Event);

    expect(service.createAttachment).toHaveBeenCalledWith('assignment-1', 'user-1', expect.objectContaining({ type: 'text/markdown' }), 'hash');
    expect(service.uploadAttachment).toHaveBeenCalledWith('assignment-1', 'user-1', 'attachment-1', expect.objectContaining({ type: 'text/markdown' }), 'hash');
  });
});

describe('StudentAssignmentDetailComponent cancelled assignment', () => {
  it('does not allow a student to submit or upload work for a cancelled assignment', () => {
    const profile = signal<UserProfile | null>(user('Student'));
    const service = {
      submitAssignment: vi.fn(),
      createAttachment: vi.fn()
    };
    TestBed.configureTestingModule({
      imports: [StudentAssignmentDetailComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'assignment-1' } } } }
      ]
    });
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;
    component.assignment.set({
      id: 'assignment-1',
      status: 'Cancelled',
      assignedStudents: [{ studentId: 'user-1', status: 'Assigned' }]
    } as AssignmentDetail);

    component.submit();

    expect(component.canSubmit()).toBe(false);
    expect(service.submitAssignment).not.toHaveBeenCalled();
    expect(service.createAttachment).not.toHaveBeenCalled();
  });
});

// K4 (31.10.2026): a file still uploading or being scanned stops the hand-in; the student sees the server's reason.
describe('StudentAssignmentDetailComponent blocked submission', () => {
  it('shows the server message when a file is not ready yet', () => {
    const profile = signal<UserProfile | null>(user('Student'));
    const message = 'Bir dosya henüz yükleniyor ya da güvenlik taramasından geçiyor. Biraz bekleyip yeniden dene ya da o dosyayı kaldır.';
    const service = {
      submitAssignment: vi.fn(() => throwError(() => ({ status: 400, error: { error: message, code: 'Attachment.NotClean' } })))
    };
    TestBed.configureTestingModule({
      imports: [StudentAssignmentDetailComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'assignment-1' } } } }
      ]
    });
    const component = TestBed.createComponent(StudentAssignmentDetailComponent).componentInstance;
    component.assignment.set({ id: 'assignment-1', status: 'Active', assignedStudents: [{ studentId: 'user-1', status: 'Assigned' }] } as AssignmentDetail);

    component.submit();

    expect(component.errorMessage()).toBe(message);
  });
});

describe('StudentAssignmentDetailComponent grading', () => {
  it('shows a saved score and teacher feedback for the student', async () => {
    const profile = signal<UserProfile | null>(user('Student'));
    const service = {
      getAssignment: vi.fn(() => of({
        id: 'assignment-1', title: 'deneme', subject: 'Matematik', source: 'Digital',
        status: 'Active', dueDate: '2030-01-02T10:00:00Z', maxScore: 100,
        assignedStudents: [{studentId: 'user-1', status: 'Graded', score: 95,
          teacherFeedback: 'Çözümün düzenli ve anlaşılır; bu çalışma biçimini sürdür.'}]
      } as AssignmentDetail))
    };
    TestBed.configureTestingModule({
      imports: [StudentAssignmentDetailComponent],
      providers: [
        {provide: AuthService, useValue: {userProfile: profile}},
        {provide: CoachingPortalService, useValue: service},
        {provide: ActivatedRoute, useValue: {snapshot: {paramMap: {get: () => 'assignment-1'}}}}
      ]
    });
    const fixture = TestBed.createComponent(StudentAssignmentDetailComponent);
    fixture.detectChanges(); await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('95');
    expect(fixture.nativeElement.textContent).toContain('/ 100');
    expect(fixture.nativeElement.textContent).toContain('Çözümün düzenli ve anlaşılır; bu çalışma biçimini sürdür.');
  });
});

// T5 (31.10.2026): status first, then the score and teacher's note, then the brief, then the hand-in.
describe('StudentAssignmentDetailComponent T5 layout', () => {
  const base = {
    id: 'assignment-1', title: 'Üslü sayılar', subject: 'Matematik', source: 'Digital', description: 'Testi çöz.',
    status: 'Active', dueDate: '2030-01-02T10:00:00Z', maxScore: 100,
    instructionAttachments: [{ id: 'i1', originalFileName: 'test.pdf', status: 'Clean', sizeBytes: 1024 }]
  };
  async function render(record: Record<string, unknown>, overrides: Record<string, unknown> = {}) {
    const profile = signal<UserProfile | null>(user('Student'));
    const service = {
      getAssignment: vi.fn(() => of({ ...base, ...overrides, assignedStudents: [{ studentId: 'user-1', ...record }] } as AssignmentDetail)),
      submitAssignment: vi.fn(() => of({})),
      calculateSha256: vi.fn(async () => 'hash'),
      createAttachment: vi.fn(() => throwError(() => ({ status: 400, error: { code: 'Attachment.Locked' } })))
    };
    TestBed.configureTestingModule({
      imports: [StudentAssignmentDetailComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: profile } },
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'assignment-1' } } } }
      ]
    });
    const fixture = TestBed.createComponent(StudentAssignmentDetailComponent);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    return { fixture, component: fixture.componentInstance, el, service, sections: () => [...el.querySelectorAll('[data-section]')].map(x => x.getAttribute('data-section')) };
  }

  it('puts the score and feedback above the brief for graded work, with one status label', async () => {
    const { el, sections } = await render({ status: 'Graded', score: 85, teacherFeedback: 'Düzenli çalışmışsın.', submittedAt: '2029-12-30T10:00:00Z' });
    expect(sections()).toEqual(['status', 'result', 'brief', 'submission']);
    expect(el.querySelector('[data-section=status]')?.textContent).toContain('Puan: 85 / 100');
    expect(el.querySelectorAll('cs-status-chip').length).toBe(1);
    expect(el.querySelector('cs-status-chip')?.textContent?.trim()).toBe('Değerlendirildi');
    expect(el.querySelector('[data-section=result]')?.textContent).toContain('Düzenli çalışmışsın.');
    expect(el.querySelector('[data-section=brief]')?.textContent).toContain('test.pdf');
    expect(el.querySelector('textarea')).toBeNull();
    expect(el.textContent).not.toContain('yeniden teslim');
  });

  it('shows the maximum score without a result section before grading', async () => {
    const { el, sections } = await render({ status: 'Assigned' });
    expect(sections()).toEqual(['status', 'brief', 'submission']);
    expect(el.querySelector('[data-section=status]')?.textContent).toContain('100 puan üzerinden');
    expect(el.querySelector('cs-status-chip')?.textContent?.trim()).toBe('Yapılacak');
    expect(el.querySelector('textarea')).not.toBeNull();
  });

  it('hides the form once submitted and offers to edit while the teacher has not graded', async () => {
    const { fixture, el, component, service } = await render({ status: 'Submitted', submittedAt: '2029-12-30T10:00:00Z' });
    const submission = el.querySelector('[data-section=submission]') as HTMLElement;
    expect(el.querySelector('textarea')).toBeNull();
    expect(submission.textContent).toContain('Teslim edildi');
    expect(submission.textContent).toContain('30.12.2029');
    const edit = [...submission.querySelectorAll('button')].find(b => b.textContent?.includes('Düzenle ve yeniden teslim et')) as HTMLButtonElement;
    expect(edit).toBeTruthy();
    edit.click(); fixture.detectChanges();
    expect(el.querySelector('textarea')).not.toBeNull();
    expect(component.canSubmit()).toBe(true);
    component.submit();
    expect(service.submitAssignment).toHaveBeenCalledWith('assignment-1', 'user-1', '');
    expect(component.editing()).toBe(false);
  });

  it('does not offer a new hand-in after the assignment is closed', async () => {
    const { el } = await render({ status: 'Submitted', submittedAt: '2029-12-30T10:00:00Z' }, { status: 'Completed' });
    expect(el.textContent).not.toContain('Düzenle ve yeniden teslim et');
  });

  it('explains a locked upload in plain words', async () => {
    const { component } = await render({ status: 'Assigned' });
    const input = { files: [new File(['x'], 'cevap.pdf', { type: 'application/pdf' })], value: 'x' } as unknown as HTMLInputElement;
    await component.onFileSelected({ target: input } as unknown as Event);
    expect(component.errorMessage()).toBe('Ödevin değerlendirildi; artık dosya ekleyip kaldıramazsın.');
  });
});

function user(role: string): UserProfile {
  return {
    id: 'user-1',
    email: 'user@example.test',
    firstName: 'Test',
    lastName: 'User',
    username: 'user@example.test',
    roles: [role],
    product: 'coaching',
    role,
    permissions: []
  };
}

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
