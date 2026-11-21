import { TestBed } from '@angular/core/testing';
import { CourseManagementService } from '../course-management.service';
import { CoursePanelApi, PanelSetup } from './panel-api.service';
import { CoursePanelState, SECTIONS, SECTION_GROUPS } from './panel-state';
import { SetupWizardComponent } from './sections/setup-wizard.component';

// Setup guide (20.11.2026, design approved by the user).
const setup = (open: string[] = []): PanelSetup => ({
  name: 'TEST Kurs', institutionCategory: null, closed: false,
  steps: ['profile', 'teachers', 'rooms', 'courses', 'students', 'fees', 'schedule', 'emails']
    .map(id => ({ id: id as PanelSetup['steps'][number]['id'], status: open.includes(id) ? 'open' as const : 'done' as const })),
  counts: { teachers: 0, invited: 1, rooms: 0, courses: 0, groups: 0, students: 0, unplaced: 0, withoutFee: 0, futureLessons: 0 }
});

function configure(api: Partial<Record<keyof CoursePanelApi, unknown>>) {
  const service = { getCourses: async () => [], getGroups: async () => [], getRooms: async () => [], getBranches: async () => [] };
  TestBed.configureTestingModule({
    providers: [CoursePanelState, { provide: CoursePanelApi, useValue: api }, { provide: CourseManagementService, useValue: service }]
  });
  const state = TestBed.inject(CoursePanelState);
  state.workspace.set({ id: 'w1', name: 'TEST Kurs', type: 'Institution', role: 'Owner', isReadOnly: false } as never);
  return state;
}

describe('setup guide', () => {
  it('groups the side menu by daily use, with the guide on top', () => {
    expect(SECTION_GROUPS.map(group => group.label)).toEqual(['', 'Günlük', 'Kurum', 'Diğer']);
    const of = (group: string) => SECTIONS.filter(item => item.group === group).map(item => item.label);
    expect(of('setup')).toEqual(['Kurulum']);
    expect(of('daily')).toEqual(['Genel bakış', 'Takvim', 'Yoklama ve ödev', 'Tahsilat']);
    expect(of('institution')).toEqual(['Öğrenciler', 'Kurslar ve gruplar', 'Öğretmenler']);
    expect(SECTIONS.find(item => item.id === 'setup')?.ownerOnly).toBe(true);
  });

  it('lets the owner give work to a teacher who has not accepted the invitation yet', () => {
    const state = configure({});
    const later = new Date(Date.now() + 86_400_000).toISOString();
    state.members.set([{ userId: 'owner', role: 'Owner', isActive: true, displayName: 'TEST Sahip' }]);
    state.invitations.set([
      { id: 'inv1', email: 'a@example.com', displayName: 'TEST Ayşe', status: 'Pending', createdAt: later, expiresAt: later, acceptedAt: null },
      { id: 'inv2', email: 'b@example.com', displayName: null, status: 'Cancelled', createdAt: later, expiresAt: later, acceptedAt: null }
    ]);
    expect(state.teachers().map(item => item.userId)).toEqual(['owner', 'inv1']);
    expect(state.teacherName('inv1')).toBe('TEST Ayşe (davet bekliyor)');
    expect(state.teacherName('inv2')).toBe('b@example.com (davet geçersiz)');
  });

  it('counts done and skipped steps, and continues at the first open one', async () => {
    const saved: unknown[] = [];
    const api = {
      setup: async () => setup(['students', 'fees']), searchStudents: async () => ({ items: [], total: 0 }), feeAgreements: async () => [],
      lessons: async () => [], emailSettings: async () => null, members: async () => [], teacherInvitations: async () => [],
      saveProfile: async (...args: unknown[]) => { saved.push(args); return { name: 'TEST Kurs', institutionCategory: 'StudyCenter' }; },
      saveSetup: async () => undefined
    };
    const state = configure(api);
    const fixture = TestBed.createComponent(SetupWizardComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();
    expect(state.setupProgress()).toEqual({ done: 6, total: 8 });
    expect(fixture.componentInstance.current()).toBe('students');
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('h2')?.textContent).toContain('Öğrencilerinizi ekleyip yerleştirin');
    expect(element.querySelector('[aria-current="step"]')?.textContent).toContain('Öğrenciler');

    fixture.componentInstance.goTo('profile');
    fixture.detectChanges();
    const cards = Array.from(element.querySelectorAll<HTMLButtonElement>('.choice'));
    expect(cards.map(card => card.querySelector('strong')?.textContent)).toEqual(['Kurs / dershane', 'Etüt merkezi', 'Okul', 'Çevrim içi']);
    cards[1].click();
    fixture.detectChanges();
    expect(cards[1].getAttribute('aria-pressed')).toBe('true');
    await fixture.componentInstance.next();
    expect(saved).toEqual([['w1', 'TEST Kurs', 3]]);
    expect(fixture.componentInstance.current()).toBe('teachers');
  });

  it('lists the first payment days in date order (21.11.2026 live test)', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-21T08:00:00Z'));
    try {
      configure({});
      const wizard = TestBed.createComponent(SetupWizardComponent).componentInstance;
      expect(wizard.dueChoices().map(item => item.value)).toEqual(['2026-11-21', '2026-12-01', '2026-12-15']);
    } finally { vi.useRealTimers(); }
  });
});
