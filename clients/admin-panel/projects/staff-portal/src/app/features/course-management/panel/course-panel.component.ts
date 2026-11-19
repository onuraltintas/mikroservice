import { Component, DestroyRef, Input, OnInit, computed, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StaffAuthService } from '../../../auth/staff-auth.service';
import { CourseManagementService, CourseWorkspace, PendingCourseInvitation } from '../course-management.service';
import { CoursePanelState, PanelSection, SECTIONS, problemText } from './panel-state';
import { OverviewSectionComponent } from './sections/overview.component';
import { StudentsSectionComponent } from './sections/students.component';
import { CoursesSectionComponent } from './sections/courses.component';
import { CalendarSectionComponent } from './sections/calendar.component';
import { AttendanceSectionComponent } from './sections/attendance.component';
import { FinanceSectionComponent } from './sections/finance.component';
import { TeachersSectionComponent } from './sections/teachers.component';
import { ReportsSectionComponent } from './sections/reports.component';
import { SettingsSectionComponent } from './sections/settings.component';

/**
 * Kurs Yönetimi institution panel (19.11.2026, design approved 17.42): one panel, nine sections in a side menu.
 * The owner sees everything; a teacher sees their own lessons, students, attendance and homework. The section is kept
 * in the address (#takvim) so a refresh or the back button keeps the place.
 */
@Component({
  selector: 'staff-course-panel',
  standalone: true,
  imports: [FormsModule, OverviewSectionComponent, StudentsSectionComponent, CoursesSectionComponent, CalendarSectionComponent,
    AttendanceSectionComponent, FinanceSectionComponent, TeachersSectionComponent, ReportsSectionComponent, SettingsSectionComponent],
  providers: [CoursePanelState],
  styleUrl: './panel.scss',
  styles: `
    .shell { display: flex; flex-wrap: wrap; min-height: 100%; }
    nav.side { flex: 1 1 240px; max-width: 260px; min-width: 220px; box-sizing: border-box; padding: 20px 14px; background: var(--ds-nav); display: flex; flex-direction: column; gap: 4px; }
    .brand { display: flex; align-items: center; gap: 10px; padding: 4px 8px 16px; }
    .brand strong { display: block; font-size: 16px; } .brand span { font-size: 13px; color: var(--ds-muted); }
    nav.side a { display: flex; align-items: center; min-height: 44px; padding: 0 14px 0 17px; border-radius: 8px; color: var(--ds-text); text-decoration: none; opacity: .85; }
    nav.side a[aria-current='page'] { background: var(--ds-surface); box-shadow: 0 1px 2px rgba(28,36,48,.08); font-weight: 600; opacity: 1; border-left: 3px solid var(--ds-accent); padding-left: 14px; }
    nav.side select { min-height: 40px; margin: 0 8px 12px; border: 1px solid var(--ds-field); border-radius: 8px; background: var(--ds-surface); color: var(--ds-text); font: inherit; padding: 0 8px; }
    main { flex: 999 1 560px; min-width: 0; box-sizing: border-box; padding: 28px 36px 40px; max-width: 1240px; }
    .setup { max-width: 560px; margin: 40px auto; padding: 24px; display: flex; flex-direction: column; gap: 16px; }
    @media (max-width: 720px) { nav.side { max-width: none; flex-basis: 100%; flex-direction: row; overflow-x: auto; padding: 8px; }
      .brand, nav.side select { display: none; } nav.side a { white-space: nowrap; } main { padding: 20px 16px 32px; } }
  `,
  template: `
    @if (loading()) {
      <p class="empty" role="status">Kurs Yönetimi açılıyor…</p>
    } @else if (state.workspace(); as workspace) {
      <div class="shell">
        <nav class="side" aria-label="Kurs Yönetimi">
          <div class="brand">
            <svg width="32" height="32" viewBox="0 0 64 64" aria-hidden="true"><rect width="64" height="64" rx="15" fill="#3A2E14"></rect><rect x="27" y="27" width="10" height="20" rx="5" fill="#FFFFFF"></rect><path d="M24 20c3-6 9-9 14-8" fill="none" stroke="#F2B33D" stroke-width="2.5" stroke-linecap="round" opacity="0.6"></path><circle cx="40" cy="14" r="6.5" fill="#F2B33D"></circle><rect x="18" y="50" width="7" height="4" rx="2" fill="#F2B33D"></rect><rect x="28.5" y="50" width="7" height="4" rx="2" fill="#F2B33D"></rect><rect x="39" y="50" width="7" height="4" rx="2" fill="#F2B33D" opacity="0.45"></rect></svg>
            <div><strong>Kurs Yönetimi</strong><span>{{ workspace.name }}</span></div>
          </div>
          @if (workspaces().length > 1) {
            <label class="sr-only" for="course-workspace">Çalışma alanı</label>
            <select id="course-workspace" [ngModel]="workspace.id" (ngModelChange)="choose($event)">
              @for (item of workspaces(); track item.id) { <option [value]="item.id">{{ item.name }}</option> }
            </select>
          }
          @for (item of visibleSections(); track item.id) {
            <a [href]="'#' + item.slug" [attr.aria-current]="section() === item.id ? 'page' : null" (click)="go(item.id, $event)">{{ item.label }}</a>
          }
        </nav>
        <main>
          @if (supportPending()) {
            <p class="banner" role="status">eduivme destek ekibi kurum kayıtlarınıza bakmak için izin istedi.
              <a href="#ayarlar" (click)="go('settings', $event)">İsteği görüntüleyin</a></p>
          }
          @if (state.readOnly()) {
            <p class="banner" role="status">Kurum salt okunur: kayıtları görebilir ve dışa aktarabilirsiniz, değişiklik yapamazsınız.</p>
          }
          @switch (section()) {
            @case ('overview') { <course-overview-section (navigate)="go($event)" (support)="supportPending.set($event)" /> }
            @case ('students') { <course-students-section /> }
            @case ('courses') { <course-courses-section /> }
            @case ('calendar') { <course-calendar-section (navigate)="go($event)" /> }
            @case ('attendance') { <course-attendance-section /> }
            @case ('finance') { <course-finance-section /> }
            @case ('teachers') { <course-teachers-section /> }
            @case ('reports') { <course-reports-section /> }
            @case ('settings') { <course-settings-section /> }
          }
        </main>
      </div>
    } @else {
      <section class="card setup" aria-labelledby="course-setup-title">
        <h1 id="course-setup-title">{{ role === 'institution' ? 'Kurumunuzu açın' : 'Kurs Yönetimi' }}</h1>
        @if (pending().length) {
          <h2>Bekleyen davetleriniz</h2>
          @for (invitation of pending(); track invitation.id) {
            <div class="actions"><span>{{ invitation.workspaceName }}, öğretmen olarak</span>
              <button type="button" class="btn btn-small" (click)="accept(invitation)" [disabled]="busy()">Daveti kabul et</button></div>
          }
        }
        <p class="muted">{{ role === 'institution' ? 'Kurumunuzun adını yazın; öğrenci, ders ve tahsilat kayıtlarınız burada tutulur.' : 'Bir kurum sizi öğretmen olarak davet ettiğinde burada görünür. Kendi öğrencileriniz için özel ders alanı da açabilirsiniz.' }}</p>
        <label class="field">Adınız<input [(ngModel)]="displayName" maxlength="200" autocomplete="name"></label>
        <label class="field">{{ role === 'institution' ? 'Kurumun adı' : 'Özel ders alanının adı' }}<input [(ngModel)]="workspaceName" maxlength="200"></label>
        @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }
        <div><button type="button" class="btn" (click)="create()" [disabled]="busy() || !displayName.trim() || !workspaceName.trim()">
          {{ role === 'institution' ? 'Kurumu aç' : 'Özel ders alanını aç' }}</button></div>
      </section>
    }
  `
})
export class CoursePanelComponent implements OnInit {
  @Input({ required: true }) role!: 'teacher' | 'institution';
  readonly state = inject(CoursePanelState);
  private readonly service = inject(CourseManagementService);
  private readonly auth = inject(StaffAuthService);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly workspaces = signal<CourseWorkspace[]>([]);
  readonly pending = signal<PendingCourseInvitation[]>([]);
  readonly section = signal<PanelSection>('overview');
  readonly supportPending = signal(false);
  readonly visibleSections = computed(() => SECTIONS.filter(item => this.state.isOwner() || !item.ownerOnly));
  displayName = '';
  workspaceName = '';
  private myUserId = '';

  async ngOnInit(): Promise<void> {
    this.readHash();
    const onHash = () => this.readHash();
    this.document.defaultView?.addEventListener('hashchange', onHash);
    this.destroyRef.onDestroy(() => this.document.defaultView?.removeEventListener('hashchange', onHash));
    try {
      try {
        const profile = await this.service.getProfile();
        this.displayName = profile.displayName;
        this.myUserId = profile.userId;
      } catch { /* no profile yet: asked on the setup card */ }
      await this.loadWorkspaces();
    } catch (error) {
      this.error.set(problemText(error, 'Kurs Yönetimi açılamadı. Sayfayı yenileyin.'));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadWorkspaces(preferred?: string): Promise<void> {
    const [workspaces, pending] = await Promise.all([this.service.getWorkspaces(), this.service.getPendingInvitations().catch(() => [])]);
    // The institution address shows the workspaces the user owns; the teacher address the ones they teach in.
    const usable = workspaces.filter(item => item.role !== 'Student'
      && (this.role === 'institution' ? item.role === 'Owner' : true));
    this.workspaces.set(usable);
    this.pending.set(pending.filter(item => item.role === 'Teacher'));
    const stored = this.storage()?.getItem('eduivme_course_workspace');
    const pick = usable.find(item => item.id === preferred) ?? usable.find(item => item.id === stored) ?? usable[0];
    if (pick) await this.open(pick);
  }

  private async open(workspace: CourseWorkspace): Promise<void> {
    this.storage()?.setItem('eduivme_course_workspace', workspace.id);
    await this.state.open(workspace, this.myUserId);
    if (!this.visibleSections().some(item => item.id === this.section())) this.section.set('overview');
  }

  async choose(id: string): Promise<void> {
    const workspace = this.workspaces().find(item => item.id === id);
    if (workspace) await this.open(workspace);
  }

  go(section: PanelSection, event?: Event): void {
    event?.preventDefault();
    this.section.set(section);
    const slug = SECTIONS.find(item => item.id === section)?.slug ?? '';
    const view = this.document.defaultView;
    if (view && view.location.hash.split('?')[0] !== `#${slug}`) view.history.pushState(null, '', `#${slug}`);
    view?.scrollTo?.({ top: 0 });
  }

  private readHash(): void {
    const slug = (this.document.defaultView?.location.hash ?? '').replace(/^#/, '').split('?')[0];
    const match = SECTIONS.find(item => item.slug === slug);
    if (match) this.section.set(match.id);
  }

  async create(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const profile = await this.service.saveProfile(this.displayName.trim());
      this.myUserId = profile.userId;
      const created = await this.service.createWorkspace(crypto.randomUUID(), this.workspaceName.trim(), this.role === 'institution' ? 1 : 2);
      await this.loadWorkspaces(created.id);
    } catch (error) {
      this.error.set(problemText(error, 'Çalışma alanı açılamadı. Hesabınızın bu rol için yetkisi olduğunu kontrol edin.'));
    } finally {
      this.busy.set(false);
    }
  }

  async accept(invitation: PendingCourseInvitation): Promise<void> {
    if (!this.displayName.trim()) { this.error.set('Daveti kabul etmeden önce adınızı yazın.'); return; }
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.joinCourseManagement('Teacher');
      await this.service.accept(invitation.id, this.displayName.trim());
      if (!this.myUserId) this.myUserId = (await this.service.getProfile()).userId;
      await this.loadWorkspaces();
    } catch (error) {
      this.error.set(problemText(error, 'Davet kabul edilemedi. Yeniden deneyin.'));
    } finally {
      this.busy.set(false);
    }
  }

  private storage(): Storage | null {
    try { return this.document.defaultView?.localStorage ?? null; } catch { return null; }
  }
}

