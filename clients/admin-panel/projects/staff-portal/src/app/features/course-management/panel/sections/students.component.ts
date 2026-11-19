import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CourseManagementService, CourseStudentAccount, StudentCsvImportPreview, StudentGuardianLink } from '../../course-management.service';
import { CoursePanelApi, PanelSentEmail, PanelStudentFilter, PanelStudentPage, PanelStudentRow } from '../panel-api.service';
import { CoursePanelState, formatDate, formatDateTime, formatMoney, problemText, readHashQuery, writeHashQuery } from '../panel-state';

const RELATIONSHIPS = [{ value: 2, label: 'Annesi' }, { value: 3, label: 'Babası' }, { value: 4, label: 'Yasal vasisi' }, { value: 1, label: 'Velisi' }, { value: 5, label: 'Diğer' }];

/**
 * Öğrenciler: a searchable, filterable, sortable list in pages, and the student card (contact, guardians, groups,
 * e-mail switch, balance). Filters stay in the address. A teacher sees their own groups' students without contacts.
 */
@Component({
  selector: 'course-students-section',
  standalone: true,
  imports: [FormsModule],
  styleUrl: '../panel.scss',
  template: `
    <header class="page-head">
      <div><h1>Öğrenciler</h1><p>{{ page()?.total ?? 0 }} öğrenci</p></div>
      @if (state.isOwner() && !state.readOnly()) {
        <div class="actions">
          <button type="button" class="btn-secondary" (click)="csvName.set(''); csvPreview.set(null); cardError.set(''); csvOpen.set(true)">Toplu ekle (CSV)</button>
          <button type="button" class="btn" (click)="openNew()">Öğrenci ekle</button>
        </div>
      }
    </header>
    @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }
    @if (error()) { <p class="alert" role="alert">{{ error() }}</p> }

    <section class="card">
      <form class="filters" role="search" (ngSubmit)="search()">
        <label class="field">Ara<input name="q" [(ngModel)]="filter.q" placeholder="Ad, e-posta, telefon ya da veli adı"></label>
        <label class="field">Kurs<select name="course" [(ngModel)]="filter.courseId" (ngModelChange)="search()">
          <option value="">Tümü</option>@for (course of state.courses(); track course.id) { <option [value]="course.id">{{ course.name }}</option> }</select></label>
        <label class="field">Grup<select name="group" [(ngModel)]="filter.groupId" (ngModelChange)="search()">
          <option value="">Tümü</option>@for (group of groupsForCourse(); track group.id) { <option [value]="group.id">{{ group.name }}</option> }</select></label>
        @if (state.isOwner()) {
          <label class="field">Öğretmen<select name="teacher" [(ngModel)]="filter.teacherId" (ngModelChange)="search()">
            <option value="">Tümü</option>@for (teacher of state.teachers(); track teacher.userId) { <option [value]="teacher.userId">{{ state.teacherName(teacher.userId) }}</option> }</select></label>
          <label class="field">Borç<select name="overdue" [(ngModel)]="overdueChoice" (ngModelChange)="search()">
            <option value="">Tümü</option><option value="yes">Gecikmiş borcu olan</option><option value="no">Gecikmiş borcu olmayan</option></select></label>
          <label class="field">Durum<select name="status" [(ngModel)]="filter.status" (ngModelChange)="search()">
            <option value="active">Etkin</option><option value="archived">Arşivde</option><option value="all">Tümü</option></select></label>
        }
        <button type="submit" class="btn-secondary">Ara</button>
        @if (hasFilters()) { <button type="button" class="btn-quiet" (click)="clear()">Filtreleri temizle</button> }
      </form>
      @if (page(); as result) {
        @if (result.items.length) {
          <div class="table-wrap">
            <table>
              <thead><tr>
                <th scope="col"><button type="button" class="sort" (click)="toggleSort('name')" [attr.aria-label]="'Ada göre sırala'">Öğrenci {{ sortMark('name') }}</button></th>
                <th scope="col">Gruplar</th>
                @if (state.isOwner()) { <th scope="col">Veli</th><th scope="col" class="right">Gecikmiş borç</th> }
                <th scope="col"><button type="button" class="sort" (click)="toggleSort('created')">Kayıt {{ sortMark('created') }}</button></th>
              </tr></thead>
              <tbody>
                @for (student of result.items; track student.id) {
                  <tr class="clickable" (click)="openCard(student)" (keydown.enter)="openCard(student)" tabindex="0">
                    <th scope="row">{{ student.displayName }}
                      @if (!student.isActive) { <span class="badge">Arşivde</span> }
                      @if (student.emailsMuted) { <span class="badge">E-posta kapalı</span> }</th>
                    <td>{{ groupNames(student) }}</td>
                    @if (state.isOwner()) {
                      <td>@if (student.guardianCount === 0) { <span class="badge warn">Veli yok</span> } @else if (student.guardianWithoutEmail) { <span class="badge warn">E-postası eksik</span> } @else { {{ student.guardianCount }} }</td>
                      <td class="right num">@if (student.overdueAmount) { <span class="badge bad">{{ money(student.overdueAmount) }}</span> } @else { – }</td>
                    }
                    <td class="num">{{ date(student.createdAt) }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <nav class="pager" aria-label="Öğrenci sayfaları">
            <span class="num">{{ result.skip + 1 }}–{{ result.skip + result.items.length }} / {{ result.total }}</span>
            <div class="actions">
              <label class="sr-only" for="page-size">Sayfa başına</label>
              <select id="page-size" class="btn-secondary btn-small" [(ngModel)]="filter.take" (ngModelChange)="search()">
                <option [ngValue]="25">25 / sayfa</option><option [ngValue]="50">50 / sayfa</option><option [ngValue]="100">100 / sayfa</option></select>
              <button type="button" class="btn-secondary btn-small" (click)="move(-1)" [disabled]="result.skip === 0">Önceki</button>
              <button type="button" class="btn-secondary btn-small" (click)="move(1)" [disabled]="result.skip + result.take >= result.total">Sonraki</button>
            </div>
          </nav>
        } @else {
          <p class="empty">{{ hasFilters() ? 'Bu filtrelere uyan öğrenci yok.' : state.isOwner() ? 'Henüz öğrenci yok. Öğrenci ekleyin ya da CSV ile toplu yükleyin.' : 'Size atanmış öğrenci yok. Kurum sizi bir gruba öğretmen yaptığında öğrencileri burada görünür.' }}</p>
        }
      } @else if (!error()) { <p class="empty" role="status">Yükleniyor…</p> }
    </section>

    @if (editing(); as form) {
      <div class="dialog-backdrop" (click)="closeCard()" (keydown.escape)="closeCard()">
        <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="student-card-title" (click)="$event.stopPropagation()">
          <h2 id="student-card-title">{{ form.id ? form.displayName || 'Öğrenci' : 'Yeni öğrenci' }}</h2>
          @if (cardError()) { <p class="alert" role="alert">{{ cardError() }}</p> }
          @if (state.isOwner()) {
            <div class="form-grid">
              <label class="field">Ad soyad<input [(ngModel)]="form.displayName" maxlength="200" [disabled]="state.readOnly()"></label>
              <label class="field">E-posta<input type="email" [(ngModel)]="form.email" maxlength="320" [disabled]="state.readOnly()"></label>
              <label class="field">Telefon<input type="tel" [(ngModel)]="form.phoneNumber" maxlength="40" [disabled]="state.readOnly()"></label>
            </div>
          }
          @if (form.id) {
            <section class="stack" style="gap: 10px">
              <h3>Gruplar</h3>
              <p class="muted" style="margin: 0">{{ form.groups || 'Henüz bir gruba kayıtlı değil.' }}</p>
              @if (state.isOwner() && !state.readOnly()) {
                <div class="actions"><label class="field" style="flex: 1">Gruba ekle<select [(ngModel)]="enrollGroupId">
                  <option value="">Grup seçin</option>@for (group of state.groups(); track group.id) { <option [value]="group.id">{{ group.courseName }}, {{ group.name }}</option> }</select></label>
                  <button type="button" class="btn-secondary" style="align-self: flex-end" (click)="enroll()" [disabled]="!enrollGroupId || busy()">Ekle</button></div>
              }
            </section>
            @if (state.isOwner()) {
              <section class="stack" style="gap: 10px">
                <h3>Veliler</h3>
                @for (guardian of guardians(); track guardian.id) {
                  <div class="actions" style="justify-content: space-between">
                    <div><strong>{{ guardian.displayName }}</strong>, {{ relationLabel(guardian.relationship) }}<br>
                      <span [class.muted]="guardian.email" [style.color]="guardian.email ? null : 'var(--ds-danger)'">{{ guardian.email ?? 'E-posta adresi yok; e-posta gönderilemez.' }}</span></div>
                    @if (!state.readOnly()) { <button type="button" class="btn-quiet" (click)="unlink(guardian)">Kaldır</button> }
                  </div>
                } @empty { <p class="muted" style="margin: 0">Veli eklenmemiş.</p> }
                @if (!state.readOnly()) {
                  <div class="form-grid">
                    <label class="field">Veli adı<input [(ngModel)]="guardian.displayName" maxlength="200"></label>
                    <label class="field">Yakınlığı<select [(ngModel)]="guardian.relationship">@for (item of relationships; track item.value) { <option [ngValue]="item.value">{{ item.label }}</option> }</select></label>
                    <label class="field">E-posta<input type="email" [(ngModel)]="guardian.email" maxlength="320"></label>
                    <label class="field">Telefon<input type="tel" [(ngModel)]="guardian.phoneNumber" maxlength="40"></label>
                  </div>
                  <div><button type="button" class="btn-secondary" (click)="addGuardian()" [disabled]="!guardian.displayName.trim() || busy()">Veli ekle</button></div>
                }
              </section>
              <section class="stack" style="gap: 6px">
                <h3>E-postalar</h3>
                <label class="check"><input type="checkbox" [ngModel]="!form.emailsMuted" (ngModelChange)="mute(!$event)" [disabled]="state.readOnly() || busy()">Bu öğrenci ve velileri kurumun e-postalarını alsın</label>
                @if (emails().length) { <p class="muted" style="margin: 0">Son e-posta {{ dateTime(emails()[0].processedAt!) }}: {{ emails()[0].subject ?? emails()[0].skipReason }}</p> }
              </section>
              @if (account(); as balance) {
                <section class="stack" style="gap: 6px">
                  <h3>Hesap</h3>
                  <p style="margin: 0">Bakiye <strong class="num">{{ money(balance.balance) }}</strong>, toplam tahsilat <span class="num">{{ money(balance.totalPayments) }}</span>.</p>
                </section>
              }
            }
          }
          <div class="dialog-actions">
            @if (form.id && state.isOwner() && !state.readOnly()) {
              <button type="button" class="btn-quiet" (click)="archive()">{{ form.isActive ? 'Arşive kaldır' : 'Yeniden etkinleştir' }}</button>
            }
            <button type="button" class="btn-secondary" (click)="closeCard()">Kapat</button>
            @if (state.isOwner() && !state.readOnly()) {
              <button type="button" class="btn" (click)="save()" [disabled]="!form.displayName.trim() || busy()">Kaydet</button>
            }
          </div>
        </section>
      </div>
    }

    @if (csvOpen()) {
      <div class="dialog-backdrop" (click)="csvOpen.set(false)">
        <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="csv-title" (click)="$event.stopPropagation()">
          <h2 id="csv-title">CSV ile toplu öğrenci ekleme</h2>
          <p class="muted" style="margin: 0">İlk satır başlık olsun: <strong>ad, e-posta, telefon</strong>. Yalnız "ad" zorunlu; başka sütunlar okunmaz.
            Excel'de "Farklı kaydet › CSV" seçin. Önce önizleme gösterilir; aynı kişiler iki kez eklenmez.</p>
          <div class="file-pick">
            <input id="csv-file" class="file-pick__input" type="file" accept=".csv,text/csv,text/plain" (change)="pickCsv($event)">
            <label for="csv-file" class="btn-secondary file-pick__button">Dosya seç</label>
            <span class="file-pick__name" aria-live="polite">{{ csvName() || 'Dosya seçilmedi' }}</span>
          </div>
          @if (csvPreview(); as preview) {
            <p>{{ preview.readyCount }} eklenecek, {{ preview.duplicateCount + preview.importedCount }} zaten var, {{ preview.invalidCount }} hatalı.</p>
            @if (preview.invalidCount) {
              <ul>@for (row of invalidRows(); track row.rowNumber) { <li>{{ row.rowNumber }}. satır: {{ row.error }}</li> }</ul>
            }
          }
          @if (cardError()) { <p class="alert" role="alert">{{ cardError() }}</p> }
          <div class="dialog-actions">
            <button type="button" class="btn-secondary" (click)="csvOpen.set(false)">Vazgeç</button>
            <button type="button" class="btn" (click)="commitCsv()" [disabled]="!csvPreview()?.readyCount || busy()">Öğrencileri ekle</button>
          </div>
        </section>
      </div>
    }
  `
})
export class StudentsSectionComponent {
  readonly state = inject(CoursePanelState);
  private readonly api = inject(CoursePanelApi);
  private readonly service = inject(CourseManagementService);
  readonly relationships = RELATIONSHIPS;
  readonly page = signal<PanelStudentPage | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  readonly busy = signal(false);
  readonly cardError = signal('');
  readonly editing = signal<{ id: string; displayName: string; email: string; phoneNumber: string; isActive: boolean; emailsMuted: boolean; groups: string } | null>(null);
  readonly guardians = signal<StudentGuardianLink[]>([]);
  readonly emails = signal<PanelSentEmail[]>([]);
  readonly account = signal<CourseStudentAccount | null>(null);
  readonly csvOpen = signal(false);
  readonly csvPreview = signal<StudentCsvImportPreview | null>(null);
  readonly csvName = signal('');
  readonly invalidRows = computed(() => (this.csvPreview()?.rows ?? []).filter(row => row.status === 'Invalid').slice(0, 20));
  private csvText = '';
  filter: Required<Pick<PanelStudentFilter, 'q' | 'courseId' | 'groupId' | 'teacherId' | 'status' | 'sort' | 'skip' | 'take'>> = {
    q: '', courseId: '', groupId: '', teacherId: '', status: 'active', sort: 'name', skip: 0, take: 25
  };
  overdueChoice: '' | 'yes' | 'no' = '';
  enrollGroupId = '';
  guardian = { displayName: '', email: '', phoneNumber: '', relationship: 2 };

  groupsForCourse() {
    return this.filter.courseId ? this.state.groups().filter(group => group.courseId === this.filter.courseId) : this.state.groups();
  }

  constructor() {
    const query = readHashQuery();
    this.filter.q = query.get('q') ?? '';
    this.filter.courseId = query.get('kurs') ?? '';
    this.filter.groupId = query.get('grup') ?? '';
    this.filter.teacherId = query.get('ogretmen') ?? '';
    this.filter.status = (query.get('durum') as 'active' | 'archived' | 'all') ?? 'active';
    this.filter.sort = (query.get('sira') as PanelStudentFilter['sort'] & string) ?? 'name';
    this.filter.skip = Number(query.get('bas') ?? 0) || 0;
    this.filter.take = [25, 50, 100].includes(Number(query.get('adet'))) ? Number(query.get('adet')) : 25;
    this.overdueChoice = (query.get('borc') as 'yes' | 'no') ?? '';
    effect(() => { this.state.id(); this.state.version(); untracked(() => void this.load()); });
  }

  hasFilters(): boolean {
    return !!(this.filter.q || this.filter.courseId || this.filter.groupId || this.filter.teacherId || this.overdueChoice || this.filter.status !== 'active');
  }

  async load(): Promise<void> {
    const id = this.state.id();
    if (!id) return;
    writeHashQuery('ogrenciler', { q: this.filter.q, kurs: this.filter.courseId, grup: this.filter.groupId, ogretmen: this.filter.teacherId,
      durum: this.filter.status === 'active' ? null : this.filter.status, sira: this.filter.sort === 'name' ? null : this.filter.sort,
      bas: this.filter.skip || null, adet: this.filter.take === 25 ? null : this.filter.take, borc: this.overdueChoice });
    this.error.set('');
    try {
      this.page.set(await this.api.searchStudents(id, { ...this.filter, overdue: this.overdueChoice === '' ? null : this.overdueChoice === 'yes' }));
    } catch (error) { this.error.set(problemText(error, 'Öğrenciler yüklenemedi.')); }
  }

  search(): void { this.filter.skip = 0; void this.load(); }
  clear(): void {
    this.filter = { ...this.filter, q: '', courseId: '', groupId: '', teacherId: '', status: 'active', skip: 0 };
    this.overdueChoice = '';
    void this.load();
  }
  move(direction: number): void { this.filter.skip = Math.max(0, this.filter.skip + direction * this.filter.take); void this.load(); }
  toggleSort(field: 'name' | 'created'): void {
    this.filter.sort = this.filter.sort === field ? `-${field}` as PanelStudentFilter['sort'] & string : field;
    this.search();
  }
  sortMark(field: string): string { return this.filter.sort === field ? '↑' : this.filter.sort === `-${field}` ? '↓' : ''; }
  groupNames(student: PanelStudentRow): string { return student.groups.map(group => group.name).join(', ') || '–'; }
  money(value: number): string { return formatMoney(value); }
  date(value: string): string { return formatDate(value); }
  dateTime(value: string): string { return formatDateTime(value); }
  relationLabel(value: string): string {
    return ({ Mother: 'annesi', Father: 'babası', LegalGuardian: 'yasal vasisi', Parent: 'velisi', Other: 'yakını' } as Record<string, string>)[value] ?? 'velisi';
  }

  openNew(): void {
    this.cardError.set('');
    this.editing.set({ id: '', displayName: '', email: '', phoneNumber: '', isActive: true, emailsMuted: false, groups: '' });
  }

  async openCard(student: PanelStudentRow): Promise<void> {
    this.cardError.set('');
    this.guardians.set([]); this.emails.set([]); this.account.set(null);
    this.editing.set({ id: student.id, displayName: student.displayName, email: student.email ?? '', phoneNumber: student.phoneNumber ?? '',
      isActive: student.isActive, emailsMuted: student.emailsMuted, groups: student.groups.map(group => `${group.courseName}, ${group.name}`).join('; ') });
    if (!this.state.isOwner()) return;
    const id = this.state.id();
    const [guardians, emails, account] = await Promise.all([
      this.service.getStudentGuardians(id, student.id).catch(() => []),
      this.api.sentEmails(id, 0, 1, student.id).catch(() => []),
      this.service.getStudentAccount(id, student.id).catch(() => null)
    ]);
    this.guardians.set(guardians); this.emails.set(emails); this.account.set(account);
  }

  closeCard(): void { this.editing.set(null); this.enrollGroupId = ''; this.guardian = { displayName: '', email: '', phoneNumber: '', relationship: 2 }; }

  private async act(work: () => Promise<void>, done: string): Promise<void> {
    this.busy.set(true);
    this.cardError.set('');
    try { await work(); if (done) this.notice.set(done); }
    catch (error) { this.cardError.set(problemText(error)); }
    finally { this.busy.set(false); }
  }

  async save(): Promise<void> {
    const form = this.editing();
    if (!form) return;
    await this.act(async () => {
      if (form.id) await this.service.updateStudent(this.state.id(), form.id, form.displayName.trim(), form.email, form.phoneNumber, form.isActive);
      else await this.service.addStudent(this.state.id(), form.displayName.trim(), form.email, form.phoneNumber);
      this.closeCard();
      await this.load();
    }, form.id ? 'Öğrenci bilgileri kaydedildi.' : 'Öğrenci eklendi. Kartından gruba ve veliye ekleyebilirsiniz.');
  }

  async archive(): Promise<void> {
    const form = this.editing();
    if (!form?.id) return;
    await this.act(async () => {
      await this.service.updateStudent(this.state.id(), form.id, form.displayName.trim(), form.email, form.phoneNumber, !form.isActive);
      this.closeCard();
      await this.load();
    }, form.isActive ? 'Öğrenci arşive kaldırıldı; grup kayıtları kapandı.' : 'Öğrenci yeniden etkinleştirildi. Gruplara yeniden ekleyebilirsiniz.');
  }

  async enroll(): Promise<void> {
    const form = this.editing();
    if (!form?.id || !this.enrollGroupId) return;
    await this.act(async () => {
      await this.service.enrollStudent(this.state.id(), this.enrollGroupId, form.id);
      const group = this.state.groups().find(item => item.id === this.enrollGroupId);
      this.editing.set({ ...form, groups: [form.groups, group ? `${group.courseName}, ${group.name}` : ''].filter(Boolean).join('; ') });
      this.enrollGroupId = '';
      await this.load();
    }, 'Öğrenci gruba eklendi.');
  }

  async addGuardian(): Promise<void> {
    const form = this.editing();
    if (!form?.id) return;
    await this.act(async () => {
      await this.service.linkGuardian(this.state.id(), form.id, crypto.randomUUID(), this.guardian.relationship,
        { displayName: this.guardian.displayName.trim(), email: this.guardian.email, phoneNumber: this.guardian.phoneNumber });
      this.guardians.set(await this.service.getStudentGuardians(this.state.id(), form.id));
      this.guardian = { displayName: '', email: '', phoneNumber: '', relationship: 2 };
    }, 'Veli eklendi.');
  }

  async unlink(guardian: StudentGuardianLink): Promise<void> {
    const form = this.editing();
    if (!form?.id) return;
    await this.act(async () => {
      await this.service.unlinkGuardian(this.state.id(), form.id, guardian.id);
      this.guardians.set(await this.service.getStudentGuardians(this.state.id(), form.id));
    }, 'Veli bağlantısı kaldırıldı.');
  }

  async mute(muted: boolean): Promise<void> {
    const form = this.editing();
    if (!form?.id) return;
    await this.act(async () => {
      await this.api.muteStudentEmails(this.state.id(), form.id, muted);
      this.editing.set({ ...form, emailsMuted: muted });
      await this.load();
    }, muted ? 'Bu öğrenci ve velilerine e-posta gönderilmeyecek.' : 'E-postalar yeniden açıldı.');
  }

  async pickCsv(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    this.csvPreview.set(null);
    this.cardError.set('');
    this.csvName.set(file?.name ?? '');
    if (!file) return;
    if (file.size > 1_000_000) { this.cardError.set('Dosya en çok 1 MB olabilir.'); return; }
    this.csvText = readCsvText(await file.arrayBuffer());
    // The same file can be chosen again after it is fixed.
    input.value = '';
    try { this.csvPreview.set(await this.service.previewStudentCsv(this.state.id(), this.csvText)); }
    catch (error) { this.cardError.set(problemText(error, 'Dosya okunamadı. İlk satırda ad, e-posta, telefon başlıkları olmalı.')); }
  }

  async commitCsv(): Promise<void> {
    const preview = this.csvPreview();
    if (!preview) return;
    await this.act(async () => {
      const result = await this.service.commitStudentCsv(this.state.id(), preview.previewId, this.csvText);
      this.csvOpen.set(false);
      this.csvPreview.set(null);
      this.notice.set(`${result.createdCount} öğrenci eklendi.`);
      await this.load();
    }, '');
  }
}

/** Excel in Turkish saves CSV as Windows-1254, not UTF-8; read UTF-8 when the bytes are valid UTF-8. */
export function readCsvText(bytes: ArrayBuffer): string {
  try { return new TextDecoder('utf-8', { fatal: true }).decode(bytes); }
  catch { return new TextDecoder('windows-1254').decode(bytes); }
}
