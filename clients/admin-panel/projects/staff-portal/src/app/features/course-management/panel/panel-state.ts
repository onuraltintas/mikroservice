import { Injectable, computed, inject, signal } from '@angular/core';
import {
  CourseBranch, CourseGroup, CourseManagementService, CourseRoom, CourseSummary, CourseWorkspace
} from '../course-management.service';
import { CoursePanelApi, PanelFeatures, PanelMember, PanelSetup, PanelTeacherInvitation } from './panel-api.service';
import { PROBLEM_TEXTS } from './problem-codes';

/** Where the panel runs: the maxkampüs site or the eduivme staff portal (kurum.eduivme.com). */
export type PanelSite = 'maxkampus' | 'eduivme';

export type PanelSection = 'setup' | 'overview' | 'students' | 'courses' | 'calendar' | 'attendance' | 'finance' | 'teachers' | 'reports' | 'settings';

/** Side menu groups (20.11.2026 decision: by daily use, not by setup order). */
export type SectionGroup = 'setup' | 'daily' | 'institution' | 'other';
export const SECTION_GROUPS: { id: SectionGroup; label: string }[] = [
  { id: 'setup', label: '' }, { id: 'daily', label: 'Günlük' }, { id: 'institution', label: 'Kurum' }, { id: 'other', label: 'Diğer' }
];

export const SECTIONS: { id: PanelSection; label: string; slug: string; ownerOnly: boolean; group: SectionGroup }[] = [
  // Setup guide: shown while the setup is not finished or closed (see CoursePanelComponent.visibleSections).
  { id: 'setup', label: 'Kurulum', slug: 'kurulum', ownerOnly: true, group: 'setup' },
  { id: 'overview', label: 'Genel bakış', slug: 'genel-bakis', ownerOnly: false, group: 'daily' },
  { id: 'calendar', label: 'Takvim', slug: 'takvim', ownerOnly: false, group: 'daily' },
  { id: 'attendance', label: 'Yoklama ve ödev', slug: 'yoklama', ownerOnly: false, group: 'daily' },
  { id: 'finance', label: 'Tahsilat', slug: 'tahsilat', ownerOnly: true, group: 'daily' },
  { id: 'students', label: 'Öğrenciler', slug: 'ogrenciler', ownerOnly: false, group: 'institution' },
  { id: 'courses', label: 'Kurslar ve gruplar', slug: 'kurslar', ownerOnly: true, group: 'institution' },
  { id: 'teachers', label: 'Öğretmenler', slug: 'ogretmenler', ownerOnly: true, group: 'institution' },
  { id: 'reports', label: 'Raporlar', slug: 'raporlar', ownerOnly: false, group: 'other' },
  { id: 'settings', label: 'Ayarlar', slug: 'ayarlar', ownerOnly: true, group: 'other' }
  // The earlier single-page screen ("Önceki ekran") was removed on 21.11.2026; its tools live in these sections.
];

/**
 * What every section of the institution panel shares: the chosen workspace, the caller's role there and the small
 * lists (courses, groups, rooms, teachers) that forms pick from. Sections load their own pages of data.
 */
@Injectable()
export class CoursePanelState {
  private readonly service = inject(CourseManagementService);
  private readonly api = inject(CoursePanelApi);

  readonly workspace = signal<CourseWorkspace | null>(null);
  readonly courses = signal<CourseSummary[]>([]);
  /** Every group, archived ones too (names in reports, the archive list on Kurslar ve gruplar). */
  readonly allGroups = signal<CourseGroup[]>([]);
  /** The groups forms pick from: archived groups take no students, lessons or homework (23.11.2026). */
  readonly groups = computed(() => this.allGroups().filter(group => !group.archivedAt));
  readonly rooms = signal<CourseRoom[]>([]);
  readonly branches = signal<CourseBranch[]>([]);
  readonly members = signal<PanelMember[]>([]);
  /** Teachers invited by e-mail who have not accepted yet: work can already be given to them (owner only). */
  readonly invitations = signal<PanelTeacherInvitation[]>([]);
  /** The setup guide's state (owner only); null until loaded. */
  readonly setup = signal<PanelSetup | null>(null);
  readonly myUserId = signal<string>('');
  /** Switched features; both off until the server says otherwise, so a closed feature never shows a dead button. */
  readonly features = signal<PanelFeatures>({ studentPortal: false, assignmentAttachments: false });
  readonly site = signal<PanelSite>('eduivme');
  /** Product name for texts shown inside the panel. */
  readonly brand = computed(() => this.site() === 'maxkampus' ? 'maxkampüs' : 'eduivme');
  /** Bumped after a change that other sections should see (a new lesson, a payment). */
  readonly version = signal(0);

  readonly isOwner = computed(() => this.workspace()?.role === 'Owner');
  readonly readOnly = computed(() => this.workspace()?.isReadOnly ?? false);
  readonly id = computed(() => this.workspace()?.id ?? '');
  /** The setup guide and teacher invitations are for an institution's owner, not for a teacher's own private-lesson space. */
  readonly isInstitutionOwner = computed(() => this.isOwner() && this.workspace()?.type === 'Institution');
  /** Who can be given groups and lessons: the owner, active teachers and (for the owner) teachers invited but not yet in. */
  readonly teachers = computed<PanelMember[]>(() => {
    const workspace = this.workspace();
    const list = this.members().filter(member => member.isActive && (member.role === 'Teacher' || member.role === 'Owner'));
    if (workspace && !this.isOwner()) return list.filter(member => member.userId === this.myUserId());
    const now = Date.now();
    const invited = this.invitations().filter(item => item.status === 'Pending' && new Date(item.expiresAt).getTime() > now)
      .map(item => ({ userId: item.id, role: 'Teacher' as const, isActive: true, displayName: item.displayName, invited: true, email: item.email }));
    return [...list, ...invited];
  });

  /** Rooms that can be planned into: archived ones only show in Ayarlar and on past lessons (21.11.2026). */
  readonly activeRooms = computed(() => this.rooms().filter(room => !room.archivedAt));

  /** Setup steps done or skipped, of eight. */
  readonly setupProgress = computed(() => {
    const steps = this.setup()?.steps ?? [];
    return { done: steps.filter(step => step.status !== 'open').length, total: steps.length };
  });

  async loadSetup(): Promise<void> {
    if (!this.isInstitutionOwner() || !this.id()) { this.setup.set(null); return; }
    try {
      const setup = await this.api.setup(this.id());
      this.setup.set(setup && Array.isArray(setup.steps) ? setup : null);
    } catch { this.setup.set(null); }
  }

  teacherName(userId: string | null | undefined): string {
    if (!userId) return '';
    const invitation = this.invitations().find(item => item.id === userId);
    if (invitation) return `${invitation.displayName || invitation.email} (${invitation.status === 'Pending' ? 'davet bekliyor' : 'davet geçersiz'})`;
    const member = this.members().find(item => item.userId === userId);
    if (member?.displayName) return member.role === 'Owner' ? `${member.displayName} (kurum sahibi)` : member.displayName;
    return member?.role === 'Owner' ? 'Kurum sahibi' : 'Öğretmen';
  }

  async open(workspace: CourseWorkspace, myUserId: string): Promise<void> {
    this.workspace.set(workspace);
    this.myUserId.set(myUserId);
    await Promise.all([this.refreshLists(), this.loadFeatures()]);
  }

  async refreshLists(): Promise<void> {
    const id = this.id();
    if (!id) return;
    const owner = this.isOwner();
    const [courses, groups, rooms, branches, members, invitations] = await Promise.all([
      this.service.getCourses(id), this.service.getGroups(id), this.service.getRooms(id),
      owner ? this.service.getBranches(id) : Promise.resolve([]),
      owner ? this.api.members(id) : Promise.resolve<PanelMember[]>([]),
      this.isInstitutionOwner() ? this.api.teacherInvitations(id).catch(() => [] as PanelTeacherInvitation[]) : Promise.resolve<PanelTeacherInvitation[]>([])
    ]);
    this.invitations.set(invitations);
    this.courses.set(courses);
    this.allGroups.set(groups);
    this.rooms.set(rooms);
    this.branches.set(branches);
    this.members.set(members);
  }

  private async loadFeatures(): Promise<void> {
    try {
      const features = await this.api.features();
      this.features.set({ studentPortal: features?.studentPortal === true, assignmentAttachments: features?.assignmentAttachments === true });
    } catch { /* Older server: keep both off. */ }
  }

  changed(): void { this.version.update(value => value + 1); }

  /** After the owner renames the institution: the header and e-mails use the new name at once. */
  async refreshWorkspaceName(name: string): Promise<void> {
    const workspace = this.workspace();
    if (workspace) this.workspace.set({ ...workspace, name });
  }
}

const ZONE = 'Europe/Istanbul';
const tr = 'tr-TR';
export const formatDate = (value: string | Date) =>
  new Intl.DateTimeFormat(tr, { timeZone: ZONE, day: 'numeric', month: 'long', year: 'numeric' }).format(new Date(value));
export const formatDay = (value: string | Date) =>
  new Intl.DateTimeFormat(tr, { timeZone: ZONE, day: 'numeric', month: 'long', weekday: 'long' }).format(new Date(value));
export const formatTime = (value: string | Date) =>
  new Intl.DateTimeFormat(tr, { timeZone: ZONE, hour: '2-digit', minute: '2-digit' }).format(new Date(value));
export const formatDateTime = (value: string | Date) =>
  new Intl.DateTimeFormat(tr, { timeZone: ZONE, day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }).format(new Date(value));
/** Money in Turkish style; a negative amount (a reversal) gets a real minus sign, not a hyphen. */
export const formatMoney = (value: number) =>
  `${new Intl.NumberFormat(tr, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value).replace(/^-/, '\u2212')} TL`;
/** Plain-language message for an API error: the code's own text (problem-codes.ts), then the HTTP status. */
export function problemText(error: unknown, fallback = 'İşlem tamamlanamadı. Bilgileri kontrol edip yeniden deneyin.'): string {
  const code = (error as { error?: { code?: string } })?.error?.code ?? '';
  const status = (error as { status?: number })?.status;
  if (PROBLEM_TEXTS[code]) return PROBLEM_TEXTS[code];
  if (status === 403 || status === 404) return 'Bu işlem için yetkiniz yok ya da kayıt bulunamadı.';
  if (status === 0) return 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edip yeniden deneyin.';
  return fallback;
}

/** Filters live after the section in the address (#ogrenciler?q=ali&take=25) so a refresh keeps them. */
export function readHashQuery(): URLSearchParams {
  const hash = globalThis.location?.hash ?? '';
  const index = hash.indexOf('?');
  return new URLSearchParams(index >= 0 ? hash.slice(index + 1) : '');
}
/**
 * The address for a fragment on the current page. The apps set <base href="/">, so a bare "#x" would resolve
 * to the site root ("/#x") and a reload would open the home page instead of the panel.
 */
export function hashUrl(fragment: string): string {
  const where = globalThis.location;
  return where ? `${where.pathname}${where.search}${fragment}` : fragment;
}

export function writeHashQuery(slug: string, values: Record<string, string | number | boolean | null | undefined>): void {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value === null || value === undefined || value === '' || value === false) continue;
    params.set(key, String(value));
  }
  const query = params.toString();
  const next = `#${slug}${query ? `?${query}` : ''}`;
  if (globalThis.location && globalThis.location.hash !== next) globalThis.history?.replaceState(null, '', hashUrl(next));
}

/** The student whose name is the typed text, ignoring case and surrounding spaces. */
export function sameStudentName<T extends { displayName: string }>(options: readonly T[], text: string): T | undefined {
  const wanted = text.trim().toLocaleLowerCase('tr');
  return wanted ? options.find(student => student.displayName.trim().toLocaleLowerCase('tr') === wanted) : undefined;
}

/** After a student joins a group (21.11.2026): the group's planned lessons whose room is now too small; the join is not refused. */
export function fullRoomNotice(lessons: number | undefined): string {
  return lessons ? ` Bu grubun ${lessons} planlı dersinin dersliği artık grup için küçük; Takvim'den başka bir derslik seçebilirsiniz.` : '';
}
