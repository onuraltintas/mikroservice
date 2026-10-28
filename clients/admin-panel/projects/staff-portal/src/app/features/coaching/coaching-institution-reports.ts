import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Injectable, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, firstValueFrom, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CoachingInstitutionSelection } from './coaching-institution-selection';

export interface InstitutionTeacherWorkload {
  teacherUserId: string; name: string; students: number; awaitingGrading: number; overdue: number;
  unreadMessages: number; upcomingSessions: number;
}
export interface InstitutionWorkload {
  institutionId: string; students: number; teachers: number; studentsWithoutTeacher: number; awaitingGrading: number;
  overdue: number; unreadMessages: number; pendingInvitations: number; teacherRows: InstitutionTeacherWorkload[]; generatedAt: string;
}
export interface InstitutionBranchSummary {
  institutionId: string; name: string; isCurrent: boolean; students: number; teachers: number; awaitingGrading: number; overdue: number;
}
export type InstitutionBulkKind = 'student-invitations' | 'teacher-links';
export interface InstitutionBulkRow { line: number; email: string; teacherEmail: string | null; action: string; error: string | null; }
export interface InstitutionBulkPreview { kind: string; fingerprint: string; rows: InstitutionBulkRow[]; toApply: number; hasErrors: boolean; }
export interface InstitutionBulkResult { applied: number; rows: InstitutionBulkRow[]; }
export interface InstitutionBulkFile { csv: string | null; xlsxBase64: string | null; }

/** Roadmap O13: workload, branches and CSV / Excel bulk operations of the selected institution. */
@Injectable({ providedIn: 'root' })
export class CoachingInstitutionReportsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/institution`;

  workload(): Observable<InstitutionWorkload> {
    return this.http.get<{ data: InstitutionWorkload }>(`${this.url}/workload`).pipe(map(r => r.data));
  }
  branches(): Observable<InstitutionBranchSummary[]> {
    return this.http.get<{ data: InstitutionBranchSummary[] }>(`${environment.apiUrl}/coaching/institution-branches`).pipe(map(r => r.data));
  }
  preview(kind: InstitutionBulkKind, file: InstitutionBulkFile): Observable<InstitutionBulkPreview> {
    return this.http.post<{ data: InstitutionBulkPreview }>(`${this.url}/bulk/${kind}/preview`, { csv: file.csv, xlsxBase64: file.xlsxBase64 })
      .pipe(map(r => r.data));
  }
  apply(kind: InstitutionBulkKind, file: InstitutionBulkFile, fingerprint: string): Observable<InstitutionBulkResult> {
    return this.http.post<{ data: InstitutionBulkResult }>(`${this.url}/bulk/${kind}`, { csv: file.csv, xlsxBase64: file.xlsxBase64, fingerprint })
      .pipe(map(r => r.data));
  }
  template(kind: InstitutionBulkKind): Observable<Blob> {
    return this.http.get(`${this.url}/bulk/${kind}/template`, { responseType: 'blob' });
  }
}

const sharedStyles = `
  :host{display:block;margin:1rem 0}
  h3{margin:0 0 .3rem;font-size:1rem}
  p{margin:.25rem 0;font-size:.86rem;line-height:1.5}
  .muted{color:var(--staff-muted,#64748b);font-size:.82rem}
  .err{color:#b91c1c} .ok{color:#047857}
  .table-scroll{overflow-x:auto;margin-top:.6rem}
  table{border-collapse:collapse;width:100%;font-size:.84rem}
  th,td{border-bottom:1px solid var(--staff-border,#e2e8f0);padding:.45rem .5rem;text-align:left;vertical-align:top}
  th{font-weight:600;white-space:nowrap}
  td.num,th.num{text-align:right}
  .warn{color:#b45309;font-weight:600}
  button{min-height:2.2rem;border:1px solid var(--staff-border,#cbd5e1);border-radius:.5rem;background:transparent;color:inherit;padding:.3rem .75rem;font:inherit;font-size:.82rem;font-weight:600;cursor:pointer}
  button.primary{background:var(--staff-accent,#0f766e);border-color:transparent;color:#fff}
  button:disabled{opacity:.55;cursor:not-allowed}
  .row{display:flex;flex-wrap:wrap;gap:.5rem;align-items:center;margin-top:.6rem}
`;

/** Roadmap O13: each branch of an admin who manages several institutions, with a switch to work on another one. */
@Component({
  selector: 'staff-coaching-institution-branches', standalone: true, imports: [CommonModule],
  styles: [sharedStyles + `.cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(14rem,1fr));gap:.6rem;margin-top:.6rem}
    article{border:1px solid var(--staff-border,#e2e8f0);border-radius:.7rem;padding:.7rem}
    article.current{border-color:var(--staff-accent,#0f766e);box-shadow:inset 0 0 0 1px var(--staff-accent,#0f766e)}
    article strong{display:block;margin-bottom:.3rem}`],
  template: `
    @if (branches().length > 1) {
      <section aria-labelledby="branches-title" data-testid="institution-branches">
        <h3 id="branches-title">Şubeleriniz</h3>
        <p class="muted">Birden fazla kurumu yönetiyorsunuz. Her şube ayrı kurumdur; öğrenci, öğretmen ve kontenjan şubeler arasında paylaşılmaz.</p>
        <div class="cards">
          @for (branch of branches(); track branch.institutionId) {
            <article [class.current]="branch.isCurrent" [attr.data-testid]="'branch-' + branch.institutionId">
              <strong>{{ branch.name }}</strong>
              <p>{{ branch.students }} öğrenci · {{ branch.teachers }} öğretmen</p>
              <p [class.warn]="branch.awaitingGrading + branch.overdue > 0">{{ branch.awaitingGrading }} değerlendirme bekliyor · {{ branch.overdue }} gecikmiş ödev</p>
              @if (branch.isCurrent) { <p class="muted">Şu an bu şubedesiniz.</p> }
              @else { <button type="button" class="no-print" [attr.data-testid]="'switch-branch-' + branch.institutionId" (click)="switchTo(branch)">Bu şubeye geç</button> }
            </article>
          }
        </div>
      </section>
    }
    @if (error()) { <p class="err" role="alert">{{ error() }}</p> }
  `
})
export class CoachingInstitutionBranchesComponent implements OnInit {
  private readonly api = inject(CoachingInstitutionReportsApi);
  private readonly selection = inject(CoachingInstitutionSelection);
  @Output() readonly switched = new EventEmitter<string>();
  readonly branches = signal<InstitutionBranchSummary[]>([]);
  readonly error = signal('');

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.branches().subscribe({ next: rows => this.branches.set(rows), error: () => this.error.set('Şube özeti yüklenemedi.') });
  }

  switchTo(branch: InstitutionBranchSummary): void {
    this.selection.choose(branch.institutionId);
    this.switched.emit(branch.institutionId);
  }
}

/** Roadmap O13: every teacher of the institution with their students and the work waiting on them. */
@Component({
  selector: 'staff-coaching-institution-workload', standalone: true, imports: [CommonModule],
  styles: [sharedStyles + `.summary{display:flex;flex-wrap:wrap;gap:.4rem 1.2rem;margin-top:.4rem;font-size:.86rem}`],
  template: `
    <section aria-labelledby="workload-title" data-testid="institution-workload">
      <h3 id="workload-title">Öğretmen iş yükü</h3>
      <p class="muted">Bekleyen işi en çok olan öğretmen üstte. Yaklaşan seanslar önümüzdeki 7 günü kapsar.</p>
      @if (loading()) { <p class="muted" role="status">İş yükü hesaplanıyor…</p> }
      @else if (error()) { <p class="err" role="alert">{{ error() }}</p> }
      @else if (workload(); as w) {
        <div class="summary">
          <span>{{ w.teachers }} öğretmen · {{ w.students }} öğrenci</span>
          <span [class.warn]="w.studentsWithoutTeacher > 0" data-testid="workload-unassigned">{{ w.studentsWithoutTeacher }} öğrencinin öğretmeni yok</span>
          <span>{{ w.awaitingGrading }} değerlendirme bekliyor</span>
          <span [class.warn]="w.overdue > 0">{{ w.overdue }} gecikmiş ödev</span>
          <span>{{ w.pendingInvitations }} bekleyen davet</span>
        </div>
        @if (!w.teacherRows.length) { <p class="muted">Kurumda henüz öğretmen yok.</p> }
        @else {
          <div class="table-scroll">
            <table>
              <thead><tr><th>Öğretmen</th><th class="num">Öğrenci</th><th class="num">Değerlendirme bekleyen</th><th class="num">Gecikmiş ödev</th><th class="num">Okunmamış yazışma</th><th class="num">Yaklaşan seans</th></tr></thead>
              <tbody>
                @for (row of w.teacherRows; track row.teacherUserId) {
                  <tr [attr.data-testid]="'workload-' + row.teacherUserId">
                    <td>{{ row.name }}</td><td class="num">{{ row.students }}</td>
                    <td class="num" [class.warn]="row.awaitingGrading > 0">{{ row.awaitingGrading }}</td>
                    <td class="num" [class.warn]="row.overdue > 0">{{ row.overdue }}</td>
                    <td class="num">{{ row.unreadMessages }}</td><td class="num">{{ row.upcomingSessions }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
        <p class="muted">Hesaplanma: {{ w.generatedAt | date:'d MMM y HH:mm' }}</p>
      }
    </section>
  `
})
export class CoachingInstitutionWorkloadComponent implements OnInit {
  private readonly api = inject(CoachingInstitutionReportsApi);
  readonly workload = signal<InstitutionWorkload | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true); this.error.set('');
    this.api.workload().subscribe({
      next: w => { this.workload.set(w); this.loading.set(false); },
      error: () => { this.error.set('Öğretmen iş yükü yüklenemedi.'); this.loading.set(false); }
    });
  }
}

const ACTION_LABELS: Record<string, string> = {
  Invite: 'Davet gönderilecek', AlreadyMember: 'Zaten kurumda', PendingInvitation: 'Bekleyen daveti var',
  Link: 'Öğretmen atanacak', AlreadyLinked: 'Zaten atanmış', Error: 'Hata'
};

/** Roadmap O13: CSV or Excel upload of student invitations or teacher links, previewed line by line before it is applied. */
@Component({
  selector: 'staff-coaching-institution-bulk-import', standalone: true, imports: [CommonModule, FormsModule],
  styles: [sharedStyles + `:host{border:1px solid var(--staff-border,#e2e8f0);border-radius:.75rem;padding:1rem}
    fieldset{border:0;padding:0;margin:.5rem 0 0;display:flex;flex-wrap:wrap;gap:.4rem 1rem}
    label{display:inline-flex;gap:.35rem;align-items:center;font-size:.86rem}
    tr.error td{color:#b91c1c}`],
  template: `
    <h3>Toplu yükleme</h3>
    <p class="muted">CSV (noktalı virgül veya virgülle ayrılmış) ya da Excel dosyası yükleyin; en çok 300 satır. Önce her satırın ne olacağını görürsünüz, hatasız dosya onayınızla uygulanır.</p>
    <fieldset>
      <legend class="muted">İşlem</legend>
      <label><input type="radio" name="bulkKind" value="student-invitations" [ngModel]="kind()" (ngModelChange)="setKind($event)" data-testid="bulk-kind-invitations">Öğrenci davetleri</label>
      <label><input type="radio" name="bulkKind" value="teacher-links" [ngModel]="kind()" (ngModelChange)="setKind($event)" data-testid="bulk-kind-links">Öğretmen atamaları</label>
    </fieldset>
    <p class="muted">{{ kind() === 'student-invitations'
      ? 'Sütunlar: eposta; ogretmen_eposta (isteğe bağlı). Kurumda olmayan her adrese davet gider.'
      : 'Sütunlar: ogrenci_eposta; ogretmen_eposta. İkisi de kurum üyesi olmalı; mevcut atamalar değişmez.' }}</p>
    <div class="row">
      <label>CSV ya da Excel dosyası <input type="file" accept=".csv,.xlsx,text/csv" data-testid="bulk-file" [disabled]="busy()" (change)="choose($event)"></label>
      <button type="button" data-testid="bulk-template" [disabled]="busy()" (click)="downloadTemplate()">Şablonu indir</button>
    </div>
    @if (error()) { <p class="err" role="alert">{{ error() }}</p> }
    @if (message()) { <p class="ok" role="status">{{ message() }}</p> }
    @if (busy()) { <p class="muted" role="status">İşleniyor…</p> }
    @if (preview(); as p) {
      <p data-testid="bulk-summary">{{ p.rows.length }} satır · {{ p.toApply }} işlem uygulanacak{{ p.hasErrors ? ' · hatalı satırlar var' : '' }}</p>
      @if (p.hasErrors) { <p class="err">Hatalı satırları düzeltip dosyayı yeniden yükleyin. Dosyada hata varken hiçbir satır uygulanmaz.</p> }
      <div class="table-scroll">
        <table>
          <thead><tr><th class="num">Satır</th><th>{{ kind() === 'student-invitations' ? 'E-posta' : 'Öğrenci' }}</th><th>Öğretmen</th><th>Sonuç</th></tr></thead>
          <tbody>
            @for (row of p.rows; track row.line) {
              <tr [class.error]="row.action === 'Error'" [attr.data-testid]="'bulk-row-' + row.line">
                <td class="num">{{ row.line }}</td><td>{{ row.email }}</td><td>{{ row.teacherEmail || '—' }}</td>
                <td>{{ label(row.action) }}{{ row.error ? ': ' + row.error : '' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <div class="row">
        <button type="button" class="primary" data-testid="bulk-apply" [disabled]="busy() || p.hasErrors || !p.toApply" (click)="apply()">
          {{ kind() === 'student-invitations' ? p.toApply + ' davet gönder' : p.toApply + ' atama yap' }}</button>
        <button type="button" [disabled]="busy()" (click)="reset()">Vazgeç</button>
      </div>
    }
  `
})
export class CoachingInstitutionBulkImportComponent {
  private readonly api = inject(CoachingInstitutionReportsApi);
  @Output() readonly applied = new EventEmitter<InstitutionBulkKind>();
  readonly kind = signal<InstitutionBulkKind>('student-invitations');
  readonly preview = signal<InstitutionBulkPreview | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  private file: InstitutionBulkFile | null = null;

  label(action: string): string { return ACTION_LABELS[action] ?? action; }

  setKind(kind: InstitutionBulkKind): void { this.kind.set(kind); this.reset(); }

  reset(): void { this.preview.set(null); this.file = null; this.error.set(''); }

  async choose(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement; const chosen = input.files?.[0]; input.value = '';
    if (!chosen) return;
    this.message.set(''); this.error.set(''); this.preview.set(null);
    if (chosen.size > 2 * 1024 * 1024) { this.error.set('Dosya 2 MB sınırını aşıyor.'); return; }
    this.busy.set(true);
    try {
      this.file = /\.xlsx$/i.test(chosen.name) ? { csv: null, xlsxBase64: await readBase64(chosen) } : { csv: await readText(chosen), xlsxBase64: null };
      this.preview.set(await firstValueFrom(this.api.preview(this.kind(), this.file)));
    } catch (error) {
      this.file = null; this.error.set(problem(error, 'Dosya okunamadı. Biçimi ve sütun adlarını kontrol edin.'));
    } finally { this.busy.set(false); }
  }

  async apply(): Promise<void> {
    const preview = this.preview(); const file = this.file;
    if (!preview || !file || preview.hasErrors || this.busy()) return;
    this.busy.set(true); this.error.set('');
    try {
      const result = await firstValueFrom(this.api.apply(this.kind(), file, preview.fingerprint));
      this.message.set(this.kind() === 'student-invitations' ? `${result.applied} davet gönderildi.` : `${result.applied} öğretmen ataması yapıldı.`);
      this.preview.set(null); this.file = null; this.applied.emit(this.kind());
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        this.error.set('Önizlemeden sonra kurum kayıtları değişti. Güncel önizleme gösteriliyor; kontrol edip yeniden onaylayın.');
        try { this.preview.set(await firstValueFrom(this.api.preview(this.kind(), file))); } catch { this.preview.set(null); }
      } else this.error.set(problem(error, 'Toplu işlem uygulanamadı. Lütfen yeniden deneyin.'));
    } finally { this.busy.set(false); }
  }

  async downloadTemplate(): Promise<void> {
    try {
      const blob = await firstValueFrom(this.api.template(this.kind()));
      const link = document.createElement('a'); const url = URL.createObjectURL(blob);
      link.href = url; link.download = this.kind() === 'student-invitations' ? 'ogrenci-davet-sablonu.csv' : 'ogretmen-atama-sablonu.csv';
      link.click(); setTimeout(() => URL.revokeObjectURL(url), 0);
    } catch { this.error.set('Şablon indirilemedi.'); }
  }
}

function problem(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse && error.status === 400 && typeof error.error?.message === 'string') return error.error.message;
  return fallback;
}

function readBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? '').replace(/^data:[^,]*,/, '')); reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

function readText(file: File): Promise<string> {
  if (typeof file.text === 'function') return file.text();
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? '')); reader.onerror = () => reject(reader.error);
    reader.readAsText(file);
  });
}
