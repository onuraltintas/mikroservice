import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Output, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { StaffAuthService } from '../../../../auth/staff-auth.service';
import {
  CourseManagementService, LegacyInstitutionImportPreview, LegacyInstitutionImportRequest, LegacyStudentImportField,
  LegacyStudentImportPreview
} from '../../course-management.service';
import {
  CourseManagementLegacyImportService, LegacyInstitutionCategory, LegacyInstitutionSourceInfo, LegacyStudentSource,
  LegacyStudentSourceRow, LegacyTeacherSourceRow
} from '../../course-management-legacy-import.service';
import { CoursePanelState, problemText } from '../panel-state';

type Tab = 'students' | 'teachers' | 'institution';
const SOURCE_LABEL: Record<LegacyStudentSource, string> = { coaching: 'maxkoç', 'speed-reading': 'Master Hızlı Okuma' };
const CATEGORY_LABEL: Record<LegacyInstitutionCategory, string> = {
  School: 'Okul', PrivateCourse: 'Kurs / dershane', StudyCenter: 'Etüt merkezi', OnlinePlatform: 'Çevrim içi'
};

/** The other products where this person manages an institution: only those can be moved from. */
export function legacySources(products: { product: string; roles: string[] }[]): LegacyStudentSource[] {
  return products.filter(access => access.product === 'coaching' || access.product === 'speed-reading')
    .filter(access => access.roles.some(role => role === 'InstitutionAdmin' || role === 'InstitutionOwner'))
    .map(access => access.product as LegacyStudentSource);
}

/**
 * Başka üründen aktarma (moved from the earlier single-page screen, 21.11.2026): students, teacher invitations and the
 * institution's name or kind from maxkoç or Master Hızlı Okuma. Only what the owner ticks moves; a preview comes first
 * and nothing is imported while a row is wrong. Shown only to an owner who manages an institution in that product.
 */
@Component({
  selector: 'course-legacy-import',
  standalone: true,
  imports: [FormsModule],
  styleUrl: '../panel.scss',
  template: `
    <section class="card" aria-labelledby="legacy-title">
      <div class="card-head"><h2 id="legacy-title">Başka üründen aktar</h2>
        <button type="button" class="btn-quiet" (click)="closed.emit()">Kapat</button></div>
      <div class="card-pad stack" style="gap: 14px; padding-top: 0">
        <div class="chips" role="group" aria-label="Kaynak ürün">
          @for (item of sources(); track item) {
            <button type="button" [attr.aria-pressed]="source() === item" (click)="chooseSource(item)">{{ sourceLabel(item) }}</button>
          }
        </div>
        <div class="tabs" role="tablist" aria-label="Ne aktarılsın">
          <button type="button" role="tab" [attr.aria-selected]="tab() === 'students'" (click)="tab.set('students')">Öğrenciler</button>
          <button type="button" role="tab" [attr.aria-selected]="tab() === 'teachers'" (click)="tab.set('teachers')">Öğretmenler</button>
          @if (state.workspace()?.type === 'Institution') {
            <button type="button" role="tab" [attr.aria-selected]="tab() === 'institution'" (click)="tab.set('institution')">Kurum bilgisi</button>
          }
        </div>
        @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }
        @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }

        @switch (tab()) {
          @case ('students') {
            <div class="actions"><button type="button" class="btn-secondary" [disabled]="busy()" (click)="loadStudents(page())">{{ studentRows().length ? 'Listeyi yenile' : 'Öğrencileri getir' }}</button>
              @if (pages() > 1) {
                <button type="button" class="btn-quiet" [disabled]="busy() || page() <= 1" (click)="loadStudents(page() - 1)">Önceki sayfa</button>
                <span class="muted num">{{ page() }} / {{ pages() }}</span>
                <button type="button" class="btn-quiet" [disabled]="busy() || page() >= pages()" (click)="loadStudents(page() + 1)">Sonraki sayfa</button>
              }</div>
            @if (studentRows().length) {
              <ul class="pick">
                @for (row of studentRows(); track row.sourceRecordId) {
                  <li><label class="check"><input type="checkbox" [checked]="chosen().has(row.sourceRecordId)" (change)="toggleStudent(row)">
                    <span>{{ row.displayName }}@if (!row.isActive) { <span class="muted"> (kaynakta etkin değil)</span> }</span></label></li>
                }
              </ul>
              <label class="check"><input type="checkbox" [(ngModel)]="withEmail" (ngModelChange)="preview.set(null)"><span>E-posta adreslerini de aktar</span></label>
              <div class="actions">
                <button type="button" class="btn-secondary" [disabled]="busy() || !chosen().size" (click)="previewStudents()">{{ chosen().size }} öğrenciyi önizle</button>
                @if (preview(); as result) {
                  <button type="button" class="btn" [disabled]="busy() || !canCommit(result)" (click)="commitStudents()">{{ result.readyCount }} öğrenciyi aktar</button>
                }
              </div>
              @if (preview(); as result) {
                <p class="muted" style="margin: 0">{{ result.readyCount }} aktarılacak, {{ result.importedCount }} zaten aktarılmış{{ result.invalidCount + result.conflictCount + result.duplicateCount
                  ? ', ' + (result.invalidCount + result.conflictCount + result.duplicateCount) + ' satır sorunlu; sorunlu satırların seçimini kaldırın' : '' }}.</p>
                @for (row of problemRows(result); track row.rowNumber) { <p class="muted" style="margin: 0; font-size: 14px">{{ row.displayName }}: {{ row.error || 'kontrol edin' }}</p> }
              }
            }
          }
          @case ('teachers') {
            <div class="actions"><button type="button" class="btn-secondary" [disabled]="busy()" (click)="loadInstitutions()">Kurumları getir</button></div>
            @if (institutions().length) {
              <label class="field">Hangi kurumun öğretmenleri?<select [ngModel]="teacherInstitution()" (ngModelChange)="chooseTeacherInstitution($event)">
                <option value="">Kurum seçin</option>
                @for (item of institutions(); track item.sourceInstitutionId) { <option [value]="item.sourceInstitutionId">{{ item.name }}</option> }</select></label>
            }
            @if (teacherRows().length) {
              <ul class="pick">
                @for (row of teacherRows(); track row.sourceUserId) {
                  <li><label class="check"><input type="checkbox" [disabled]="!!teacherStatus(row)" [checked]="chosenTeachers().has(row.sourceUserId)" (change)="toggleTeacher(row)">
                    <span>{{ row.displayName }}@if (teacherStatus(row); as why) { <span class="muted"> ({{ why }})</span> }</span></label></li>
                }
              </ul>
              <div><button type="button" class="btn" [disabled]="busy() || !chosenTeachers().size" (click)="inviteTeachers()">{{ chosenTeachers().size }} öğretmeni davet et</button></div>
              <p class="muted" style="margin: 0; font-size: 14px">Davet, öğretmenin {{ sourceLabel(source()) }} hesabına gider; kabul edince bu kurumda öğretmen olur.</p>
            }
          }
          @case ('institution') {
            <div class="actions"><button type="button" class="btn-secondary" [disabled]="busy()" (click)="loadInstitutions()">Kurumları getir</button></div>
            @if (institutions().length) {
              <label class="field">Hangi kurum?<select [ngModel]="institution()?.sourceInstitutionId ?? ''" (ngModelChange)="chooseInstitution($event)">
                <option value="">Kurum seçin</option>
                @for (item of institutions(); track item.sourceInstitutionId) { <option [value]="item.sourceInstitutionId">{{ item.name }}</option> }</select></label>
            }
            @if (institution(); as source) {
              <label class="check"><input type="checkbox" [(ngModel)]="withName" (ngModelChange)="institutionPreview.set(null)"><span>Kurumun adı: {{ source.name }}</span></label>
              <label class="check"><input type="checkbox" [(ngModel)]="withType" (ngModelChange)="institutionPreview.set(null)"><span>Kurumun türü: {{ categoryLabel(source.institutionCategory) }}</span></label>
              <div class="actions">
                <button type="button" class="btn-secondary" [disabled]="busy() || (!withName && !withType)" (click)="previewInstitution()">Önizle</button>
                @if (institutionPreview(); as result) {
                  <button type="button" class="btn" [disabled]="busy()" (click)="commitInstitution(result)">Aktar</button>
                  <span class="muted">{{ result.workspaceName }} → {{ result.targetName }}</span>
                }
              </div>
            }
          }
        }
      </div>
    </section>
  `,
  styles: `
    .pick { list-style: none; margin: 0; padding: 0; max-height: 320px; overflow: auto; border: 1px solid var(--ds-line); border-radius: 8px; }
    .pick li { padding: 0 12px; border-top: 1px solid var(--ds-line); } .pick li:first-child { border-top: 0; }
    .pick .check { min-height: 44px; align-items: center; }
  `
})
export class LegacyImportComponent {
  @Output() readonly closed = new EventEmitter<void>();
  readonly state = inject(CoursePanelState);
  private readonly auth = inject(StaffAuthService);
  private readonly service = inject(CourseManagementService);
  private readonly sourceService = inject(CourseManagementLegacyImportService);

  readonly sources = computed(() => legacySources(this.auth.products()));
  readonly source = signal<LegacyStudentSource>('coaching');
  readonly tab = signal<Tab>('students');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly studentRows = signal<LegacyStudentSourceRow[]>([]);
  readonly page = signal(1);
  readonly pages = signal(1);
  readonly chosen = signal(new Map<string, LegacyStudentSourceRow>());
  readonly preview = signal<LegacyStudentImportPreview | null>(null);
  readonly institutions = signal<LegacyInstitutionSourceInfo[]>([]);
  readonly teacherInstitution = signal('');
  readonly teacherRows = signal<LegacyTeacherSourceRow[]>([]);
  readonly chosenTeachers = signal(new Map<string, LegacyTeacherSourceRow>());
  readonly institution = signal<LegacyInstitutionSourceInfo | null>(null);
  readonly institutionPreview = signal<LegacyInstitutionImportPreview | null>(null);
  withEmail = false; withName = false; withType = false;

  constructor() { const first = this.sources()[0]; if (first) this.source.set(first); }

  sourceLabel(source: LegacyStudentSource): string { return SOURCE_LABEL[source]; }
  categoryLabel(category: LegacyInstitutionCategory | null): string { return category ? CATEGORY_LABEL[category] : 'Belirtilmemiş'; }

  chooseSource(source: LegacyStudentSource): void {
    this.source.set(source);
    this.studentRows.set([]); this.chosen.set(new Map()); this.preview.set(null);
    this.institutions.set([]); this.teacherInstitution.set(''); this.teacherRows.set([]); this.chosenTeachers.set(new Map());
    this.institution.set(null); this.institutionPreview.set(null);
  }

  // ---- students ----
  loadStudents(page: number): Promise<void> {
    return this.act(async () => {
      const result = await this.sourceService.getStudentPage(this.source(), Math.max(1, page));
      this.studentRows.set(result.items);
      this.page.set(result.pageNumber);
      this.pages.set(Math.max(1, Math.ceil(result.totalCount / result.pageSize)));
      this.preview.set(null);
    });
  }
  toggleStudent(row: LegacyStudentSourceRow): void {
    const next = new Map(this.chosen());
    if (next.has(row.sourceRecordId)) next.delete(row.sourceRecordId);
    else if (next.size >= 500) { this.error.set('Bir aktarımda en çok 500 öğrenci seçebilirsiniz.'); return; }
    else next.set(row.sourceRecordId, row);
    this.chosen.set(next); this.preview.set(null); this.error.set('');
  }
  private fields(): LegacyStudentImportField[] { return ['displayName', ...(this.withEmail ? ['email' as const] : [])]; }
  private rows(): LegacyStudentSourceRow[] {
    return [...this.chosen().values()].map(row => ({ sourceRecordId: row.sourceRecordId, displayName: row.displayName,
      email: this.withEmail ? row.email : null, isActive: true }));
  }
  canCommit(result: LegacyStudentImportPreview): boolean {
    return result.readyCount > 0 && !result.invalidCount && !result.conflictCount && !result.duplicateCount && !this.state.readOnly();
  }
  problemRows(result: LegacyStudentImportPreview) { return result.rows.filter(row => row.status !== 'Ready' && row.status !== 'Imported').slice(0, 10); }
  previewStudents(): Promise<void> {
    return this.act(async () => this.preview.set(await this.service.previewLegacyStudents(this.state.id(), this.source(), this.fields(), this.rows())));
  }
  commitStudents(): Promise<void> {
    const preview = this.preview();
    if (!preview) return Promise.resolve();
    return this.act(async () => {
      const result = await this.service.commitLegacyStudents(this.state.id(), preview.previewId, this.source(), this.fields(), this.rows());
      this.chosen.set(new Map()); this.preview.set(null);
      this.state.changed();
      this.notice.set(`${result.createdCount} öğrenci aktarıldı${result.alreadyImportedCount ? `; ${result.alreadyImportedCount} öğrenci zaten aktarılmıştı` : ''}.`);
    });
  }

  // ---- teachers and the institution ----
  loadInstitutions(): Promise<void> {
    return this.act(async () => this.institutions.set((await this.sourceService.getInstitutionPage(this.source(), 1)).items));
  }
  chooseTeacherInstitution(id: string): Promise<void> {
    this.teacherInstitution.set(id); this.teacherRows.set([]); this.chosenTeachers.set(new Map());
    if (!id) return Promise.resolve();
    return this.act(async () => this.teacherRows.set((await this.sourceService.getTeacherPage(this.source(), id, 1)).items));
  }
  teacherStatus(row: LegacyTeacherSourceRow): string | null {
    if (!row.isActive) return 'kaynakta etkin değil';
    if (row.sourceUserId === this.state.myUserId()) return 'kendi hesabınız';
    if (this.state.members().some(member => member.userId === row.sourceUserId && member.isActive)) return 'zaten bu kurumda';
    return null;
  }
  toggleTeacher(row: LegacyTeacherSourceRow): void {
    const next = new Map(this.chosenTeachers());
    if (next.has(row.sourceUserId)) next.delete(row.sourceUserId); else next.set(row.sourceUserId, row);
    this.chosenTeachers.set(next);
  }
  inviteTeachers(): Promise<void> {
    const teachers = [...this.chosenTeachers().values()].filter(row => !this.teacherStatus(row));
    return this.act(async () => {
      let sent = 0, skipped = 0;
      for (const teacher of teachers) {
        try { await this.service.invite(this.state.id(), crypto.randomUUID(), teacher.sourceUserId, 2, null); sent++; }
        catch (error) { if (error instanceof HttpErrorResponse && error.status === 409) skipped++; else throw error; }
      }
      this.chosenTeachers.set(new Map());
      await this.state.refreshLists();
      this.notice.set(`${sent} öğretmene davet gönderildi${skipped ? `; ${skipped} öğretmenin daveti zaten vardı` : ''}.`);
    });
  }
  chooseInstitution(id: string): void {
    this.institution.set(this.institutions().find(item => item.sourceInstitutionId === id) ?? null);
    this.withName = false; this.withType = false; this.institutionPreview.set(null);
  }
  private institutionRequest(): LegacyInstitutionImportRequest | null {
    const source = this.institution();
    if (!source || (!this.withName && !this.withType)) return null;
    return { sourceSystem: this.source(), sourceInstitutionId: source.sourceInstitutionId,
      selectedFields: [...(this.withName ? ['name' as const] : []), ...(this.withType ? ['type' as const] : [])],
      name: this.withName ? source.name : null, institutionCategory: this.withType ? source.institutionCategory : null };
  }
  previewInstitution(): Promise<void> {
    const request = this.institutionRequest();
    if (!request) return Promise.resolve();
    return this.act(async () => this.institutionPreview.set(await this.service.previewLegacyInstitution(this.state.id(), request)));
  }
  commitInstitution(preview: LegacyInstitutionImportPreview): Promise<void> {
    const request = this.institutionRequest();
    if (!request) return Promise.resolve();
    return this.act(async () => {
      const result = await this.service.commitLegacyInstitution(this.state.id(), request, preview);
      this.institutionPreview.set(null);
      await this.state.refreshWorkspaceName(result.name);
      await this.state.loadSetup();
      this.notice.set(result.alreadyApplied ? 'Bu bilgiler zaten kayıtlı.' : 'Kurum bilgileri aktarıldı.');
    });
  }

  private async act(work: () => Promise<unknown>): Promise<void> {
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try { await work(); }
    catch (error) { this.error.set(problemText(error, `${this.sourceLabel(this.source())} bilgilerine ulaşılamadı. Oturumunuzu kontrol edin.`)); }
    finally { this.busy.set(false); }
  }
}
