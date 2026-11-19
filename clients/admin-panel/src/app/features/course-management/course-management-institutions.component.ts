import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { CourseManagementSupportChangesComponent } from './course-management-support-changes.component';
import {
  CourseManagementAdminWorkspaceDetail,
  CourseManagementAdminWorkspaceRow,
  CourseManagementInstitutionsService,
  SupportSection
} from './course-management-admin.service';

interface SupportTable { title: string; columns: string[]; rows: string[][]; }

const SECTION_LABELS: Record<SupportSection, string> = {
  Students: 'Öğrenciler', Schedule: 'Takvim ve yoklama', Finance: 'Tahsilat', Settings: 'Ayarlar'
};
const COLUMN_LABELS: Record<string, string> = {
  displayName: 'Ad', email: 'E-posta', phoneNumber: 'Telefon', isActive: 'Etkin', emailsMuted: 'E-posta kapalı',
  guardians: 'Veliler', groups: 'Gruplar', courseName: 'Kurs', startsAt: 'Başlangıç', endsAt: 'Bitiş', status: 'Durum',
  attendance: 'Yoklama', student: 'Öğrenci', label: 'Taksit', remaining: 'Kalan', dueOn: 'Son gün', amount: 'Tutar',
  receivedAt: 'Tarih', method: 'Yöntem', isReversal: 'Ters kayıt', kind: 'Bildirim', recipientCount: 'Alıcı',
  skipReason: 'Gönderilmeme nedeni', processedAt: 'Zaman', enabled: 'Açık', audience: 'Kime'
};

/**
 * Kurumlar (19.11.2026): every institution with counts, and support's view and corrections of its records. No owner
 * approval is needed (user decision, 19.11.2026); the second factor follows the administrators' MFA policy, and every
 * look and change shows in the institution's support record.
 */
@Component({
  selector: 'app-course-management-institutions',
  standalone: true,
  imports: [FormsModule, CourseManagementSupportChangesComponent],
  template: `
    <section class="panel" aria-labelledby="institutions-title">
      <div class="heading">
        <div>
          <h2 id="institutions-title">Kurumlar</h2>
          <p>Ad, sayılar ve son etkinlik. Öğrenci, yoklama ve tahsilat kayıtları yalnız kurumun onayladığı destek erişimiyle açılır.</p>
        </div>
        <form class="search" role="search" (ngSubmit)="load(0)">
          <label for="institution-search">Kurum ara</label>
          <input id="institution-search" name="query" [(ngModel)]="query" maxlength="80">
          <button type="submit" [disabled]="loading()">Ara</button>
        </form>
      </div>
      @if (error()) {
        <p class="alert" role="alert">Kurumlar yüklenemedi. <button type="button" (click)="load()">Yeniden dene</button></p>
      }
      @if (rows().length) {
        <div class="scroll">
          <table>
            <thead><tr><th scope="col">Kurum</th><th scope="col">Tür</th><th scope="col">Öğretmen</th><th scope="col">Etkin öğrenci</th>
              <th scope="col">Kayıt</th><th scope="col">Son etkinlik</th><th scope="col"><span class="sr-only">İşlem</span></th></tr></thead>
            <tbody>
              @for (row of rows(); track row.id) {
                <tr [class.selected]="selected()?.id === row.id">
                  <th scope="row">{{ row.name }}@if (row.isReadOnly) { <span class="tag">Salt okunur</span> }</th>
                  <td>{{ typeLabel(row.type) }}</td>
                  <td class="num">{{ row.teacherCount }}</td>
                  <td class="num">{{ row.activeStudentCount }}</td>
                  <td>{{ date(row.createdAt) }}</td>
                  <td>{{ row.lastActivityAt ? dateTime(row.lastActivityAt) : 'Yok' }}</td>
                  <td><button type="button" (click)="open(row.id)">Ayrıntı</button></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <nav class="pager" aria-label="Kurum sayfaları">
          <span class="num">{{ skip() + 1 }}–{{ skip() + rows().length }} / {{ total() }}</span>
          <button type="button" (click)="load(skip() - pageSize)" [disabled]="loading() || skip() === 0">Önceki</button>
          <button type="button" (click)="load(skip() + pageSize)" [disabled]="loading() || skip() + pageSize >= total()">Sonraki</button>
        </nav>
      } @else if (!loading() && !error()) {
        <p>{{ query.trim() ? 'Aramaya uyan kurum yok.' : 'Henüz kurum yok.' }}</p>
      }

      @if (selected(); as detail) {
        <section class="detail" aria-labelledby="institution-detail-title">
          <h3 id="institution-detail-title">{{ detail.name }}</h3>
          <dl class="facts">
            <div><dt>Tür</dt><dd>{{ typeLabel(detail.type) }}</dd></div>
            <div><dt>Öğretmen</dt><dd class="num">{{ detail.teacherCount }}</dd></div>
            <div><dt>Etkin öğrenci</dt><dd class="num">{{ detail.activeStudentCount }}</dd></div>
            <div><dt>Kurs</dt><dd class="num">{{ detail.courseCount }}</dd></div>
            <div><dt>Son 30 günde ders</dt><dd class="num">{{ detail.lessonsLast30Days }}</dd></div>
            <div><dt>E-postalar</dt><dd>{{ detail.email.switchedOn ? 'Açık' : 'Kapalı' }}</dd></div>
            <div><dt>Son 30 günde e-posta</dt><dd class="num">{{ detail.email.sentLast30Days }} gönderildi, {{ detail.email.skippedLast30Days }} gönderilmedi</dd></div>
            <div><dt>E-postalardan çıkan adres</dt><dd class="num">{{ detail.email.optedOutAddresses }}</dd></div>
            <div><dt>Son 30 günde teslim edilemeyen</dt><dd class="num">{{ detail.email.undeliveredLast30Days }}</dd></div>
          </dl>

          <h4>Kurumun kayıtları</h4>
          <p class="note">Destek için kurumun kayıtlarını açabilir ve küçük düzeltmeler yapabilirsiniz. Her bakış ve her değişiklik kurumun ekranında "Destek kaydı" olarak görünür. İki adımlı doğrulama, Sistem ayarlarındaki "maxkampüs (Kurs Yönetimi)" MFA politikasına göre istenir.</p>
          <div class="actions">
            @for (section of sections; track section) {
              <button type="button" (click)="view(detail.id, section)" [disabled]="sectionLoading()" [attr.aria-pressed]="shownSection() === section">{{ label(section) }}</button>
            }
          </div>
          @if (sectionError()) { <p class="alert" role="alert">{{ sectionError() }}</p> }
          @if (shownSection(); as section) {
            @if (section !== 'Finance') {
              <app-course-management-support-changes [workspaceId]="detail.id" [section]="section" [data]="shownData()"
                (changed)="view(detail.id, section)" />
            } @else { <p class="banner">Tahsilat kayıtlarını yalnız kurum değiştirebilir.</p> }
          }
          @for (table of tables(); track table.title) {
            <div class="scroll">
              <table>
                <caption>{{ table.title }}</caption>
                <thead><tr>@for (column of table.columns; track column) { <th scope="col">{{ column }}</th> }</tr></thead>
                <tbody>@for (row of table.rows; track $index) { <tr>@for (cell of row; track $index) { <td>{{ cell }}</td> }</tr> }</tbody>
              </table>
            </div>
          }
          @if (detail.supportLog.length) {
            <details>
              <summary>Bu kurumdaki son destek işlemleri</summary>
              <ul>
                @for (entry of detail.supportLog; track $index) {
                  <li>{{ dateTime(entry.occurredAt) }}: {{ entry.what }}@if (entry.isChange && entry.after) { ({{ entry.before ? entry.before + ' → ' : '' }}{{ entry.after }}) }</li>
                }
              </ul>
            </details>
          }
        </section>
      }
    </section>
  `,
  styles: `
    :host { display: block; margin-top: 30px; }
    .panel { border: 1px solid var(--ds-line, #E1E4E9); border-radius: 10px; background: var(--ds-surface, #fff); padding: 20px; color: var(--ds-text, #1C2430); }
    .heading { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 16px; align-items: flex-end; }
    h2 { margin: 0; font-size: 18px; } h3 { margin: 0 0 12px; font-size: 18px; } h4 { margin: 20px 0 10px; font-size: 16px; }
    .heading p { margin: 6px 0 0; color: var(--ds-muted, #555F6D); max-width: 60ch; }
    .search { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    .search label { font-size: 14px; color: var(--ds-muted, #555F6D); }
    input, textarea { border: 1px solid var(--ds-field, #7A8492); border-radius: 8px; padding: 10px; font: inherit; color: inherit; background: var(--ds-surface, #fff); min-height: 44px; box-sizing: border-box; }
    textarea { width: 100%; }
    button { min-height: 44px; border: 1px solid var(--ds-field, #7A8492); border-radius: 8px; background: var(--ds-surface, #fff); color: inherit; padding: 0 14px; font: inherit; cursor: pointer; }
    button[type=submit], .actions button:not(.quiet) { background: var(--ds-primary, #1C2430); color: var(--ds-on-primary, #fff); border-color: var(--ds-primary, #1C2430); }
    button:disabled { opacity: .6; cursor: not-allowed; }
    button:focus-visible, input:focus-visible, textarea:focus-visible, summary:focus-visible { outline: 3px solid var(--ds-focus, #1C2430); outline-offset: 2px; }
    .scroll { overflow-x: auto; margin-top: 14px; }
    table { width: 100%; border-collapse: collapse; min-width: 720px; }
    caption { text-align: left; font-weight: 600; padding: 8px 0; }
    th, td { padding: 10px; border-bottom: 1px solid var(--ds-line, #E1E4E9); text-align: left; vertical-align: top; }
    thead th { color: var(--ds-muted, #555F6D); font-size: 14px; font-weight: 500; }
    tbody th { font-weight: 600; }
    tr.selected { background: var(--ds-soft, #ECEEF2); }
    .num { font-variant-numeric: tabular-nums; }
    .tag { margin-left: 8px; padding: 2px 8px; border-radius: 999px; background: var(--ds-warning-soft, #FDF0D5); color: var(--ds-warning, #8A5300); font-size: 13px; font-weight: 600; }
    .pager { display: flex; justify-content: flex-end; align-items: center; gap: 10px; margin-top: 12px; color: var(--ds-muted, #555F6D); }
    .detail { margin-top: 24px; border-top: 1px solid var(--ds-line, #E1E4E9); padding-top: 20px; }
    .facts { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; margin: 0; }
    .facts div { display: flex; flex-direction: column; gap: 2px; } dt { font-size: 14px; color: var(--ds-muted, #555F6D); } dd { margin: 0; font-weight: 600; }
    .note { margin: 0 0 12px; color: var(--ds-muted, #555F6D); max-width: 75ch; }
    .banner { margin: 0 0 12px; padding: 12px 14px; border-radius: 8px; background: var(--ds-warning-soft, #FDF0D5); color: var(--ds-text, #1C2430); line-height: 1.5; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .alert { color: var(--ds-danger, #B42318); }
    details { margin-top: 16px; } summary { cursor: pointer; min-height: 44px; display: flex; align-items: center; }
    .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
  `
})
export class CourseManagementInstitutionsComponent implements OnInit {
  private readonly service = inject(CourseManagementInstitutionsService);
  readonly pageSize = 25;
  readonly rows = signal<CourseManagementAdminWorkspaceRow[]>([]);
  readonly total = signal(0);
  readonly skip = signal(0);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly selected = signal<CourseManagementAdminWorkspaceDetail | null>(null);
  readonly tables = signal<SupportTable[]>([]);
  readonly sectionLoading = signal(false);
  readonly sectionError = signal('');
  readonly sections: SupportSection[] = ['Students', 'Schedule', 'Finance', 'Settings'];
  readonly shownSection = signal<SupportSection | null>(null);
  readonly shownData = signal<unknown>(null);
  query = '';

  ngOnInit(): void { this.load(0); }

  load(skip = this.skip()): void {
    this.loading.set(true);
    this.error.set(false);
    this.service.list(this.query, Math.max(0, skip), this.pageSize).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: page => { this.rows.set(page.items); this.total.set(page.total); this.skip.set(Math.max(0, skip)); },
      error: () => this.error.set(true)
    });
  }

  open(id: string): void {
    this.tables.set([]);
    this.shownSection.set(null);
    this.sectionError.set('');
    this.service.detail(id).subscribe({ next: detail => this.selected.set(detail), error: () => this.error.set(true) });
  }

  view(id: string, section: SupportSection): void {
    this.sectionLoading.set(true);
    this.sectionError.set('');
    this.service.section(id, section).pipe(finalize(() => this.sectionLoading.set(false))).subscribe({
      next: data => { this.tables.set(toTables(SECTION_LABELS[section], data)); this.shownSection.set(section); this.shownData.set(data); },
      error: response => {
        this.tables.set([]); this.shownSection.set(null);
        this.sectionError.set(response?.status === 403
          ? 'Bu bölüm iki adımlı doğrulama istiyor. Oturumu iki adımlı doğrulamayla yeniden açın.'
          : 'Bu bölüm açılamadı. Sayfayı yenileyip yeniden deneyin.');
      }
    });
  }

  label(section: SupportSection): string { return SECTION_LABELS[section]; }
  typeLabel(type: string): string { return type === 'Institution' ? 'Kurum' : 'Bağımsız öğretmen'; }
  date(value: string): string { return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium' }).format(new Date(value)); }
  dateTime(value: string): string {
    return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
  }

}

/** Turns a support view (a list, or an object of lists) into simple tables for reading. */
export function toTables(title: string, data: unknown): SupportTable[] {
  const asTable = (name: string, items: unknown[]): SupportTable | null => {
    const objects = items.filter((item): item is Record<string, unknown> => !!item && typeof item === 'object');
    if (!objects.length) return { title: name, columns: ['Kayıt'], rows: [['Kayıt yok']] };
    const keys = Object.keys(objects[0]).filter(key => !/id$/i.test(key));
    return { title: name, columns: keys.map(key => COLUMN_LABELS[key] ?? key), rows: objects.map(item => keys.map(key => cell(item[key]))) };
  };
  if (Array.isArray(data)) return [asTable(title, data)!];
  if (data && typeof data === 'object') {
    const record = data as Record<string, unknown>;
    const tables = Object.entries(record).filter(([, value]) => Array.isArray(value))
      .map(([key, value]) => asTable(`${title}: ${COLUMN_LABELS[key] ?? key}`, value as unknown[])!);
    const facts = Object.entries(record).filter(([, value]) => !Array.isArray(value));
    if (facts.length) tables.unshift({ title, columns: ['Alan', 'Değer'], rows: facts.map(([key, value]) => [COLUMN_LABELS[key] ?? key, cell(value)]) });
    return tables;
  }
  return [];
}

function cell(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'boolean') return value ? 'Evet' : 'Hayır';
  if (Array.isArray(value)) return value.map(item => item && typeof item === 'object'
    ? Object.entries(item).filter(([key, part]) => part !== null && !/id$/i.test(key) && typeof part !== 'boolean').map(([, part]) => String(part)).join(', ')
    : String(item)).join('; ');
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) {
    return new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
  }
  return String(value);
}
