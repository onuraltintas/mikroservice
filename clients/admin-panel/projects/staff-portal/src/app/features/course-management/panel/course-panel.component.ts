import { Component, DestroyRef, Input, OnInit, computed, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StaffAuthService } from '../../../auth/staff-auth.service';
import { CourseManagementService, CourseWorkspace, PendingCourseInvitation } from '../course-management.service';
import { CoursePanelState, PanelSection, PanelSite, SECTIONS, SECTION_GROUPS, hashUrl, problemText } from './panel-state';
import { SetupWizardComponent } from './sections/setup-wizard.component';
import { PanelConfirmDialogComponent } from './panel-confirm';
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
    AttendanceSectionComponent, FinanceSectionComponent, TeachersSectionComponent, ReportsSectionComponent, SettingsSectionComponent, PanelConfirmDialogComponent, SetupWizardComponent],
  providers: [CoursePanelState],
  styleUrl: './panel.scss',
  styles: `
    .shell { display: flex; flex-wrap: wrap; min-height: 100%; }
    nav.side { flex: 1 1 240px; max-width: 260px; min-width: 220px; box-sizing: border-box; padding: 20px 14px; background: var(--ds-nav); display: flex; flex-direction: column; gap: 4px; }
    .brand { display: flex; align-items: center; gap: 10px; padding: 4px 8px 16px; }
    .brand strong { display: block; font-size: 16px; } .brand span { font-size: 13px; color: var(--ds-muted); }
    nav.side a { display: flex; align-items: center; min-height: 44px; padding: 0 14px 0 17px; border-radius: 8px; color: var(--ds-text); text-decoration: none; opacity: .85; }
    nav.side a[aria-current='page'] { background: var(--ds-surface); box-shadow: 0 1px 2px rgba(28,36,48,.08); font-weight: 600; opacity: 1; border-left: 3px solid var(--ds-accent); padding-left: 14px; }
    .menu-group { margin: 14px 8px 4px; font-size: 13px; font-weight: 600; color: var(--ds-muted); }
    nav.side a.setup-link { font-weight: 600; opacity: 1; background: var(--ds-surface); border: 1px dashed var(--ds-accent); margin-bottom: 4px; }
    nav.side select { min-height: 40px; margin: 0 8px 12px; border: 1px solid var(--ds-field); border-radius: 8px; background: var(--ds-surface); color: var(--ds-text); font: inherit; padding: 0 8px; }
    main { flex: 999 1 560px; min-width: 0; box-sizing: border-box; padding: 28px 36px 40px; max-width: 1240px; }
    .setup { max-width: 560px; margin: 40px auto; padding: 24px; display: flex; flex-direction: column; gap: 16px; }
    @media (max-width: 720px) { nav.side { max-width: none; flex-basis: 100%; flex-direction: row; overflow-x: auto; padding: 8px; }
      .brand, nav.side select, .menu-group { display: none; } nav.side a { white-space: nowrap; } main { padding: 20px 16px 32px; } }
  `,
  template: `
    @if (loading()) {
      <p class="empty" role="status">maxkampüs açılıyor…</p>
    } @else if (state.workspace(); as workspace) {
      <div class="shell">
        <nav class="side" aria-label="maxkampüs">
          <div class="brand">
            <svg width="32" height="32" viewBox="0 0 64 64" aria-hidden="true"><rect width="64" height="64" rx="15" fill="#3A2E14"></rect><rect x="27" y="27" width="10" height="20" rx="5" fill="#FFFFFF"></rect><path d="M24 20c3-6 9-9 14-8" fill="none" stroke="#F2B33D" stroke-width="2.5" stroke-linecap="round" opacity="0.6"></path><circle cx="40" cy="14" r="6.5" fill="#F2B33D"></circle><rect x="18" y="50" width="7" height="4" rx="2" fill="#F2B33D"></rect><rect x="28.5" y="50" width="7" height="4" rx="2" fill="#F2B33D"></rect><rect x="39" y="50" width="7" height="4" rx="2" fill="#F2B33D" opacity="0.45"></rect></svg>
            <div><strong>maxkampüs</strong><span>{{ workspace.name }}</span></div>
          </div>
          @if (workspaces().length > 1) {
            <label class="sr-only" for="course-workspace">Çalışma alanı</label>
            <select id="course-workspace" [ngModel]="workspace.id" (ngModelChange)="choose($event)">
              @for (item of workspaces(); track item.id) { <option [value]="item.id">{{ item.name }}</option> }
            </select>
          }
          @for (group of menu(); track group.id) {
            @if (group.label) { <span class="menu-group">{{ group.label }}</span> }
            @for (item of group.items; track item.id) {
              <a [href]="link(item.slug)" [attr.aria-current]="section() === item.id ? 'page' : null" [class.setup-link]="item.id === 'setup'"
                (click)="go(item.id, $event)">{{ item.id === 'setup' ? 'Kurulum (' + state.setupProgress().done + '/' + state.setupProgress().total + ')' : item.label }}</a>
            }
          }
        </nav>
        <main>
          @if (state.readOnly()) {
            <p class="banner" role="status">Kurum salt okunur: kayıtları görebilir ve dışa aktarabilirsiniz, değişiklik yapamazsınız.</p>
          }
          @switch (section()) {
            @case ('setup') { <course-setup-wizard (navigate)="leaveSetup($event)" /> }
            @case ('overview') { <course-overview-section (navigate)="go($event)" /> }
            @case ('students') { <course-students-section /> }
            @case ('courses') { <course-courses-section /> }
            @case ('calendar') { <course-calendar-section (navigate)="go($event)" /> }
            @case ('attendance') { <course-attendance-section /> }
            @case ('finance') { <course-finance-section /> }
            @case ('teachers') { <course-teachers-section /> }
            @case ('reports') { <course-reports-section /> }
            @case ('settings') { <course-settings-section (navigate)="go($event)" /> }
          }
        </main>
      </div>
    } @else {
      <section class="card setup" aria-labelledby="course-setup-title">
        <h1 id="course-setup-title">{{ role === 'institution' ? 'Kurumunuzu açın' : 'maxkampüs' }}</h1>
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
    <staff-panel-confirm-dialog />
  `
})
export class CoursePanelComponent implements OnInit {
  @Input({ required: true }) role!: 'teacher' | 'institution';
  /** maxkampus.com: plan and support texts name maxkampüs and link to its /plan page. */
  @Input() set site(value: PanelSite) { this.state.site.set(value); }
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
  /** Sections this user may open (a teacher: no owner-only ones). The setup guide stays reachable after it is closed. */
  readonly visibleSections = computed(() => SECTIONS.filter(item => (this.state.isOwner() || !item.ownerOnly)
    && (item.id !== 'setup' || this.state.isInstitutionOwner())));
  /** The side menu, grouped by daily use; "Kurulum (x/8)" sits on top until the owner finishes or closes the guide. */
  readonly menu = computed(() => {
    const setup = this.state.setup();
    const showSetup = !!setup && !setup.closed;
    return SECTION_GROUPS.map(group => ({ ...group,
      items: this.visibleSections().filter(item => item.group === group.id && (item.id !== 'setup' || showSetup)) }))
      .filter(group => group.items.length);
  });
  displayName = '';
  workspaceName = '';
  private myUserId = '';

  async ngOnInit(): Promise<void> {
    this.readHash();
    const onHash = () => this.readHash();
    this.document.defaultView?.addEventListener('hashchange', onHash);
    this.destroyRef.onDestroy(() => this.document.defaultView?.removeEventListener('hashchange', onHash));
    // Live test 21.11.2026: a change made elsewhere (a teacher accepting an invitation, another tab) showed only after a
    // reload. Coming back to the tab reloads the lists, at most once a minute.
    const onVisible = () => { if (this.document.visibilityState === 'visible') void this.refreshIfStale(); };
    this.document.addEventListener('visibilitychange', onVisible);
    this.destroyRef.onDestroy(() => this.document.removeEventListener('visibilitychange', onVisible));
    try {
      try {
        const profile = await this.service.getProfile();
        this.displayName = profile.displayName;
        this.myUserId = profile.userId;
      } catch { /* no profile yet: asked on the setup card */ }
      await this.loadWorkspaces();
    } catch (error) {
      this.error.set(problemText(error, 'maxkampüs açılamadı. Sayfayı yenileyin.'));
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
    await this.state.loadSetup();
    if (!this.visibleSections().some(item => item.id === this.section())) this.toOverview();
    // The guide opens by itself on entry while steps are open, unless the owner chose "Daha sonra devam et" in this
    // browser session or came in on a section's own address.
    const setup = this.state.setup();
    const hash = (this.document.defaultView?.location.hash ?? '').replace(/^#/, '').split('?')[0];
    const later = this.session()?.getItem(`eduivme_setup_later_${workspace.id}`) === '1';
    if (setup && !setup.closed && setup.steps.some(step => step.status === 'open') && !later && (!hash || hash === 'genel-bakis')) {
      this.section.set('setup');
      const view = this.document.defaultView;
      view?.history.replaceState(view.history.state, '', hashUrl('#kurulum'));
    }
  }

  private refreshedAt = Date.now();
  private async refreshIfStale(): Promise<void> {
    if (!this.state.id() || Date.now() - this.refreshedAt < 60_000) return;
    this.refreshedAt = Date.now();
    try {
      await this.state.refreshLists();
      await this.state.loadSetup();
      this.state.changed();
    } catch { /* the next visit tries again */ }
  }

  /** Leaving the guide: "Daha sonra devam et" keeps it closed until the next visit; finishing removes it from the menu. */
  leaveSetup(section: PanelSection): void {
    const id = this.state.id();
    if (id && !this.state.setup()?.closed) this.session()?.setItem(`eduivme_setup_later_${id}`, '1');
    this.go(section);
  }

  private session(): Storage | null {
    try { return this.document.defaultView?.sessionStorage ?? null; } catch { return null; }
  }

  /** A teacher who types an owner-only address (#tahsilat, #ayarlar…) lands on Genel bakış (20.11.2026 live test). */
  private toOverview(): void {
    this.section.set('overview');
    const view = this.document.defaultView;
    if (!view || !view.location.hash) return;
    view.history.replaceState(view.history.state, '', hashUrl('#genel-bakis'));
    // Live test 21.11.2026: the host app's router finishes its own navigation afterwards and wrote the old address back.
    // Replacing once more after it settles leaves #genel-bakis in the address bar (hashchange → readHash → overview).
    setTimeout(() => {
      const slug = view.location.hash.replace(/^#/, '').split('?')[0];
      if (slug && slug !== 'genel-bakis' && !this.visibleSections().some(item => item.slug === slug)) view.location.replace(hashUrl('#genel-bakis'));
    }, 300);
  }

  async choose(id: string): Promise<void> {
    const workspace = this.workspaces().find(item => item.id === id);
    if (workspace) await this.open(workspace);
  }

  go(section: PanelSection, event?: Event, query?: string): void {
    event?.preventDefault();
    const slug = SECTIONS.find(item => item.id === section)?.slug ?? '';
    const view = this.document.defaultView;
    if (view && query) view.history.pushState(null, '', hashUrl(`#${slug}?${query}`));
    else if (view && view.location.hash.split('?')[0] !== `#${slug}`) view.history.pushState(null, '', hashUrl(`#${slug}`));
    // Re-create the section when it is chosen again, so it reads its filters from the address.
    if (this.section() === section) { this.section.set('overview'); queueMicrotask(() => this.section.set(section)); }
    else this.section.set(section);
    view?.scrollTo?.({ top: 0 });
  }

  link(slug: string): string { return hashUrl(`#${slug}`); }

  private readHash(): void {
    const slug = (this.document.defaultView?.location.hash ?? '').replace(/^#/, '').split('?')[0];
    const match = SECTIONS.find(item => item.slug === slug);
    // The earlier screen was removed on 21.11.2026: a saved #onceki-ekran address opens Genel bakış.
    if (!match) { if (slug === 'onceki-ekran') this.toOverview(); return; }
    // Before the workspace is open the role is unknown; open() checks the section again.
    if (this.state.workspace() && !this.visibleSections().some(item => item.id === match.id)) this.toOverview();
    else this.section.set(match.id);
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

