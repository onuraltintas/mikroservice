import { Location } from '@angular/common';
import { provideLocationMocks, SpyLocation } from '@angular/common/testing';
import { ChangeDetectorRef, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ToasterService } from '../../../../../src/app/core/services/toaster.service';
import { StaffAuthService } from '../auth/staff-auth.service';
import { StaffProductAccess } from '../auth/staff-auth.models';
import { AppComponent } from '../app.component';
import { CoachingTeacherWorkspaceComponent } from '../features/coaching/coaching-teacher-workspace.component';
import { CoachingInstitutionWorkspaceComponent } from '../features/coaching/coaching-institution-workspace.component';
import { CoachingInstitutionService } from '../features/coaching/coaching-institution.service';
import { formatStaffPath, labelTableCells, parseStaffPath, StaffLocationService } from './staff-location.service';
import { StaffSectionNavComponent } from './staff-section-nav.component';

// Roadmap O2: the staff portal has addresses, groups its sections and works on the phone.
beforeEach(() => TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }));
const assignmentId = '3f2b8a52-6c1e-4f0e-9d7a-0b6c2a1e5d44';
const studentId = '6a1f9c3e-2b7d-4e8a-9c0f-1d2e3f4a5b6c';
const decisionId = '9b8c7d6e-5f4a-4b3c-8d2e-1f0a9b8c7d6e';

describe('O2 staff addresses', () => {
  it('reads and writes product, workspace, section and the item being looked at', () => {
    expect(parseStaffPath(`/coaching/teacher/assignments/${assignmentId}`)).toEqual({ product: 'coaching', role: 'teacher', section: 'assignments', id: assignmentId, week: null });
    expect(parseStaffPath(`/coaching/teacher/tracking/${studentId}/2026-10-26/${decisionId}?x=1`)).toEqual({ product: 'coaching', role: 'teacher', section: 'tracking', id: null,
      week: { studentId, weekStart: '2026-10-26', decisionId } });
    expect(parseStaffPath('/coaching/institution/teachers')).toEqual({ product: 'coaching', role: 'institution', section: 'teachers', id: null, week: null });
    expect(parseStaffPath('/speed-reading')).toEqual({ product: 'speed-reading', role: null, section: null, id: null, week: null });
    expect(parseStaffPath('/coaching/teacher/unknown')?.section).toBeNull();
    expect(parseStaffPath('/coaching/teacher/assignments/not-an-id')?.id).toBeNull();
    expect(parseStaffPath('/coaching/teacher/tracking/' + studentId + '/2026-10-27')?.week).toBeNull();
    expect(parseStaffPath('/')).toBeNull(); expect(parseStaffPath('/elsewhere/teacher')).toBeNull();
    expect(formatStaffPath({ product: 'coaching', role: 'teacher', section: 'assignments', id: assignmentId, week: null })).toBe(`/coaching/teacher/assignments/${assignmentId}`);
    expect(formatStaffPath({ product: 'coaching', role: 'teacher', section: 'tracking', id: null, week: { studentId, weekStart: '2026-10-26', decisionId: null } }))
      .toBe(`/coaching/teacher/tracking/${studentId}/2026-10-26`);
    expect(formatStaffPath({ product: 'coaching', role: null, section: 'goals', id: null, week: null })).toBe('/coaching');
  });
});

describe('O2 teacher workspace', () => {
  function workspace(path = '/') {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideLocationMocks(), StaffLocationService] });
    const location = TestBed.inject(Location) as SpyLocation; location.replaceState(path);
    const component = TestBed.runInInjectionContext(() => new CoachingTeacherWorkspaceComponent());
    return { component, location };
  }

  it('opens on the tracking centre', () => {
    expect(workspace().component.activeSection()).toBe('tracking');
  });

  it('opens the section and assignment in the address after a refresh', () => {
    const { component } = workspace(`/coaching/teacher/assignments/${assignmentId}`);
    expect(component.activeSection()).toBe('assignments');
    expect(component.notificationAssignmentId()).toBe(assignmentId);
    const weekly = workspace(`/coaching/teacher/tracking/${studentId}/2026-10-26`).component;
    expect(weekly.notificationWeeklyTarget()).toEqual({ studentId, weekStart: '2026-10-26', decisionId: null });
  });

  it('writes each section to the address and follows the back button', async () => {
    const { component, location } = workspace();
    await component.selectSection('goals');
    expect(location.path()).toBe('/coaching/teacher/goals');
    await component.selectSection('exams');
    location.simulateUrlPop('/coaching/teacher/goals'); await vi.waitFor(() => expect(component.activeSection()).toBe('goals'));
    expect(await component.openAssignmentFromNotification(assignmentId)).toBe(true);
    expect(location.path()).toBe(`/coaching/teacher/assignments/${assignmentId}`);
  });

  it('stays put and restores the address when leaving would lose unsaved work', async () => {
    const { component, location } = workspace('/coaching/teacher/finance');
    component.financePanel = { canLeavePage: async () => false };
    location.simulateUrlPop('/coaching/teacher/students');
    await vi.waitFor(() => expect(location.path()).toBe('/coaching/teacher/finance'));
    expect(component.activeSection()).toBe('finance');
  });
});

describe('O2 institution workspace', () => {
  it('keeps its section in the address', async () => {
    TestBed.configureTestingModule({ providers: [provideLocationMocks(), StaffLocationService, { provide: CoachingInstitutionService, useValue: {} }] });
    const location = TestBed.inject(Location) as SpyLocation; location.replaceState('/coaching/institution/analytics');
    const component = TestBed.runInInjectionContext(() => new CoachingInstitutionWorkspaceComponent());
    expect(component.activeSection()).toBe('analytics');
    await component.selectSection('subscription');
    expect(location.path()).toBe('/coaching/institution/subscription');
  });
});

describe('O2 grouped section menu', () => {
  @Component({ standalone: true, imports: [StaffSectionNavComponent], template: `<staff-section-nav label="Bölüm" [groups]="groups" [active]="active()" (select)="picked.push($event)" />` })
  class Host {
    readonly active = signal('b'); readonly picked: string[] = [];
    readonly groups = [{ label: 'Günlük takip', items: [{ value: 'a', label: 'A', testId: 'a-tab' }, { value: 'b', label: 'B' }] }, { label: 'Hesabım', items: [{ value: 'c', label: 'C' }] }];
  }

  it('shows labelled groups as buttons and the same choices as a menu for the phone', () => {
    const fixture = TestBed.createComponent(Host); fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    expect([...page.querySelectorAll('[role=group]')].map(x => x.getAttribute('aria-label'))).toEqual(['Günlük takip', 'Hesabım']);
    expect(page.querySelector('[data-testid=a-tab]')).not.toBeNull();
    expect(page.querySelector('button[aria-current=page]')?.textContent?.trim()).toBe('B');
    const select = page.querySelector('select') as HTMLSelectElement;
    expect([...select.querySelectorAll('optgroup')].map(x => x.label)).toEqual(['Günlük takip', 'Hesabım']);
    expect(select.value).toBe('b');
    (page.querySelector('[data-testid=a-tab]') as HTMLButtonElement).click();
    select.value = 'c'; select.dispatchEvent(new Event('change'));
    expect(fixture.componentInstance.picked).toEqual(['a', 'c']);
  });
});

describe('O2 portal shell', () => {
  function app(path: string, roles: string[] = ['Teacher', 'InstitutionAdmin'], products: StaffProductAccess['product'][] = ['coaching']) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideLocationMocks(), StaffLocationService, { provide: StaffAuthService, useValue: {
      products: signal<StaffProductAccess[]>(products.map(product => ({ product, roles }))), activeProduct: signal(products[0]), isAuthenticated: () => true } }, { provide: ChangeDetectorRef, useValue: { markForCheck: vi.fn() } }] });
    const location = TestBed.inject(Location) as SpyLocation; location.replaceState(path);
    return { component: TestBed.runInInjectionContext(() => new AppComponent()), location };
  }

  it('starts on the product and workspace in the address', () => {
    expect(app('/speed-reading/teacher', ['Teacher'], ['speed-reading']).component.selectedProduct).toBe('speed-reading');
    expect(app('/coaching/teacher/tracking').component.activeWorkspaceRole).toBe('teacher');
    expect(app('/coaching/institution').component.activeWorkspaceRole).toBe('institution');
  });

  it('writes the workspace to the address and gives the institution workspace the phone menu too', async () => {
    const { component, location } = app('/');
    await component.selectWorkspaceRole('teacher');
    expect(location.path()).toBe('/coaching/teacher');
    await component.selectWorkspaceRole('institution');
    expect(location.path()).toBe('/coaching/institution');
    expect(component.hasPhoneMenu).toBe(true);
  });
});

describe('O2 tables on the phone', () => {
  it('labels each cell with its column so rows can be shown as cards', () => {
    const root = document.createElement('div');
    root.innerHTML = '<table><thead><tr><th>Öğrenci</th><th>Durum</th></tr></thead><tbody><tr><td>Ada</td><td data-label="Özel">Teslim</td></tr></tbody></table><table><tbody><tr><td>Başlıksız</td></tr></tbody></table>';
    labelTableCells(root);
    const cells = [...root.querySelectorAll('td')];
    expect(cells.map(cell => cell.getAttribute('data-label'))).toEqual(['Öğrenci', 'Özel', null]);
  });
});
