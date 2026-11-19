import { Injectable, computed, inject, signal } from '@angular/core';
import {
  CourseBranch, CourseGroup, CourseManagementService, CourseRoom, CourseSummary, CourseWorkspace
} from '../course-management.service';
import { CoursePanelApi, PanelMember } from './panel-api.service';

export type PanelSection = 'overview' | 'students' | 'courses' | 'calendar' | 'attendance' | 'finance' | 'teachers' | 'reports' | 'settings' | 'previous';

export const SECTIONS: { id: PanelSection; label: string; slug: string; ownerOnly: boolean }[] = [
  { id: 'overview', label: 'Genel bakış', slug: 'genel-bakis', ownerOnly: false },
  { id: 'students', label: 'Öğrenciler', slug: 'ogrenciler', ownerOnly: false },
  { id: 'courses', label: 'Kurslar ve gruplar', slug: 'kurslar', ownerOnly: true },
  { id: 'calendar', label: 'Takvim', slug: 'takvim', ownerOnly: false },
  { id: 'attendance', label: 'Yoklama ve ödev', slug: 'yoklama', ownerOnly: false },
  { id: 'finance', label: 'Tahsilat', slug: 'tahsilat', ownerOnly: true },
  { id: 'teachers', label: 'Öğretmenler', slug: 'ogretmenler', ownerOnly: true },
  { id: 'reports', label: 'Raporlar', slug: 'raporlar', ownerOnly: false },
  { id: 'settings', label: 'Ayarlar', slug: 'ayarlar', ownerOnly: true },
  // Transition: the earlier single-page screen keeps the tools the new sections do not have yet
  // (moving records from other products, makeup lessons, homework files, the change record).
  { id: 'previous', label: 'Önceki ekran', slug: 'onceki-ekran', ownerOnly: false }
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
  readonly groups = signal<CourseGroup[]>([]);
  readonly rooms = signal<CourseRoom[]>([]);
  readonly branches = signal<CourseBranch[]>([]);
  readonly members = signal<PanelMember[]>([]);
  readonly myUserId = signal<string>('');
  /** Bumped after a change that other sections should see (a new lesson, a payment). */
  readonly version = signal(0);

  readonly isOwner = computed(() => this.workspace()?.role === 'Owner');
  readonly readOnly = computed(() => this.workspace()?.isReadOnly ?? false);
  readonly id = computed(() => this.workspace()?.id ?? '');
  readonly teachers = computed(() => {
    const workspace = this.workspace();
    const list = this.members().filter(member => member.isActive && (member.role === 'Teacher' || member.role === 'Owner'));
    if (workspace && !this.isOwner()) return list.filter(member => member.userId === this.myUserId());
    return list;
  });

  teacherName(userId: string | null | undefined): string {
    if (!userId) return '';
    const member = this.members().find(item => item.userId === userId);
    if (member?.displayName) return member.role === 'Owner' ? `${member.displayName} (kurum sahibi)` : member.displayName;
    return member?.role === 'Owner' ? 'Kurum sahibi' : 'Öğretmen';
  }

  async open(workspace: CourseWorkspace, myUserId: string): Promise<void> {
    this.workspace.set(workspace);
    this.myUserId.set(myUserId);
    await this.refreshLists();
  }

  async refreshLists(): Promise<void> {
    const id = this.id();
    if (!id) return;
    const owner = this.isOwner();
    const [courses, groups, rooms, branches, members] = await Promise.all([
      this.service.getCourses(id), this.service.getGroups(id), this.service.getRooms(id),
      owner ? this.service.getBranches(id) : Promise.resolve([]),
      owner ? this.api.members(id) : Promise.resolve<PanelMember[]>([])
    ]);
    this.courses.set(courses);
    this.groups.set(groups);
    this.rooms.set(rooms);
    this.branches.set(branches);
    this.members.set(members);
  }

  changed(): void { this.version.update(value => value + 1); }
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
export const formatMoney = (value: number) =>
  `${new Intl.NumberFormat(tr, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value)} TL`;
/** Plain-language messages for the codes the API answers with. */
export function problemText(error: unknown, fallback = 'İşlem tamamlanamadı. Bilgileri kontrol edip yeniden deneyin.'): string {
  const code = (error as { error?: { code?: string } })?.error?.code ?? '';
  const status = (error as { status?: number })?.status;
  const known: Record<string, string> = {
    'Lesson.TeacherConflict': 'Öğretmenin bu saatte başka bir dersi var.',
    'Lesson.StudentConflict': 'Öğrencilerden birinin bu saatte başka bir dersi var.',
    'Room.Conflict': 'Derslik bu saatte dolu.',
    'Room.CapacityExceeded': 'Derslik bu grup için küçük.',
    'Availability.TimeOffConflict': 'Öğretmen bu saatte izinli.',
    'Availability.OutsideWorkingHours': 'Ders öğretmenin çalışma saatlerinin dışında.',
    'Lesson.GroupHasNoActiveStudents': 'Grupta etkin öğrenci yok; önce öğrenci ekleyin.',
    'Lesson.TeacherNotInWorkspace': 'Seçilen öğretmen bu kurumda değil.',
    'LessonSeries.NoFutureOccurrences': 'Seçilen aralıkta ileri tarihli ders çıkmıyor.',
    'LessonSeries.AllOccurrencesConflict': 'Seçilen derslerin hepsi çakışıyor.',
    'Invitation.AccountNotFound': 'Bu e-postayla bir eduivme hesabı yok. Öğretmen önce hesap açmalı.',
    'Membership.AlreadyActive': 'Bu kişi zaten kurumda.',
    'Invitation.AlreadyPending': 'Bu kişiye gönderilmiş bir davet zaten bekliyor.',
    'EmailSettings.ConsentRequired': 'E-postaları açmadan önce bilgilendirme onayını işaretleyin.',
    'Workspace.ErasureReadOnly': 'Kurum salt okunur; değişiklik yapılamaz.',
    'Subscription.ReadOnly': 'Aboneliğiniz etkin değil; kayıtlar yalnız okunabilir.',
    'Attendance.LessonNotStarted': 'Ders henüz başlamadı; yoklama ders başlayınca alınır.',
    'Attendance.LessonClosed': 'Ders tamamlanmış ya da iptal edilmiş; yoklama değiştirilemez.',
    'Lesson.AttendanceStarted': 'Yoklaması alınmış ya da başlamış ders iptal edilemez.',
    'Assignment.NoActiveRecipients': 'Grupta etkin öğrenci yok.',
    'Student.ActiveLimitReached': 'Planınızın öğrenci sınırına ulaştınız.'
  };
  if (known[code]) return known[code];
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
export function writeHashQuery(slug: string, values: Record<string, string | number | boolean | null | undefined>): void {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value === null || value === undefined || value === '' || value === false) continue;
    params.set(key, String(value));
  }
  const query = params.toString();
  const next = `#${slug}${query ? `?${query}` : ''}`;
  if (globalThis.location && globalThis.location.hash !== next) globalThis.history?.replaceState(null, '', next);
}
